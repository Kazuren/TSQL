using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class ScriptExtensionTests
    {
        // #####################################################################
        // ########################## AddCondition ############################
        // #####################################################################

        [Fact]
        public void AddCondition_AppliesConditionToAllStatements()
        {
            Script script = Script.Parse("SELECT * FROM Users; SELECT * FROM Orders");

            script.AddCondition("Active = 1");

            string result = script.ToSource();
            Assert.Contains("SELECT * FROM Users WHERE Active = 1", result);
            Assert.Contains("SELECT * FROM Orders WHERE Active = 1", result);
        }

        [Fact]
        public void AddCondition_WithParams_ResolvesAcrossAllStatements()
        {
            // Both statements use @Status — collision detection should consider both
            Script script = Script.Parse(
                "SELECT * FROM Users WHERE Id = @Status; SELECT * FROM Orders");

            script.AddCondition("Status = @Status",
                new object[] { ("@Status", 1) },
                out IReadOnlyDictionary<string, object> parameters);

            string result = script.ToSource();
            // The condition variable should be renamed due to collision with stmt1's @Status
            Assert.Single(parameters);
            string paramName = parameters.Keys.First();
            Assert.Equal(1, parameters[paramName]);
            // The renamed variable should appear in both statements' conditions
            Assert.Contains("Status = " + paramName, result);
        }

        [Fact]
        public void AddCondition_WithParams_NoCollision()
        {
            Script script = Script.Parse("SELECT * FROM Users; SELECT * FROM Orders");

            script.AddCondition("TenantId = @TenantId",
                new object[] { ("@TenantId", 42) },
                out IReadOnlyDictionary<string, object> parameters);

            string result = script.ToSource();
            Assert.Equal(42, parameters["@TenantId"]);
            Assert.Contains("Users WHERE TenantId = @TenantId", result);
            Assert.Contains("Orders WHERE TenantId = @TenantId", result);
        }

        [Fact]
        public void AddCondition_SkipsNonSelectStatements()
        {
            Script script = Script.Parse("DROP TABLE #Temp; SELECT * FROM Users");

            script.AddCondition("Active = 1");

            string result = script.ToSource();
            Assert.Contains("DROP TABLE #Temp", result);
            Assert.Contains("SELECT * FROM Users WHERE Active = 1", result);
        }

        [Fact]
        public void AddCondition_RoundTripsCorrectly()
        {
            Script script = Script.Parse("SELECT * FROM Users; SELECT * FROM Orders");
            script.AddCondition("Active = 1");

            // Re-parse and verify structure
            Script reparsed = Script.Parse(script.ToSource());
            Assert.Equal(2, reparsed.Statements.Count);
        }

        // #####################################################################
        // ########################## Parameterize ############################
        // #####################################################################

        [Fact]
        public void Parameterize_SharedCounterAcrossStatements()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users WHERE Name = 'Alice'; SELECT * FROM Orders WHERE Total = 100");

            script.Parameterize(out IReadOnlyDictionary<string, object> parameters);

            // Should get @P0 for 'Alice' and @P1 for 100 — not @P0 for both
            Assert.Equal(2, parameters.Count);
            Assert.Equal("Alice", parameters["@P0"]);
            Assert.Equal(100, parameters["@P1"]);

            string result = script.ToSource();
            Assert.Contains("Name = @P0", result);
            Assert.Contains("Total = @P1", result);
        }

        [Fact]
        public void Parameterize_SkipsExistingVariableNames()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users WHERE Id = @P0; SELECT * FROM Orders WHERE Total = 99");

            script.Parameterize(out IReadOnlyDictionary<string, object> parameters);

            // @P0 already exists in stmt1, so the literal 99 should get @P1 (not @P0)
            Assert.Single(parameters);
            Assert.Equal(99, parameters["@P1"]);
        }

        // #####################################################################
        // #################### AddSchemaAwareCondition #######################
        // #####################################################################

        [Fact]
        public void AddSchemaAwareCondition_AppliesPerStatement()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users; SELECT * FROM Orders");
            ColumnExistenceChecker checker = CreateChecker(
                Schema(
                    ("Users", new[] { "TenantId" }),
                    ("Orders", new[] { "TenantId" })
                ));

            script.AddSchemaAwareCondition("TenantId = 1", checker);

            string result = script.ToSource();
            Assert.Contains("Users", result);
            Assert.Contains("Orders", result);
            // Both tables have TenantId, so both should get the condition
            Assert.Contains("TenantId = 1", result);
        }

        [Fact]
        public void AddSchemaAwareCondition_WithParams_ResolvesAcrossStatements()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users WHERE Id = @TenantId; SELECT * FROM Orders");
            ColumnExistenceChecker checker = CreateChecker(
                Schema(
                    ("Users", new[] { "TenantId" }),
                    ("Orders", new[] { "TenantId" })
                ));

            script.AddSchemaAwareCondition("TenantId = @TenantId",
                new object[] { ("@TenantId", 5) },
                checker,
                out IReadOnlyDictionary<string, object> parameters);

            // @TenantId collides with stmt1's existing @TenantId
            Assert.Single(parameters);
            string paramName = parameters.Keys.First();
            Assert.Equal(5, parameters[paramName]);
        }

        // #####################################################################
        // ########################### Collectors #############################
        // #####################################################################

        [Fact]
        public void CollectTableReferences_MergesAcrossStatements()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users; SELECT * FROM Orders JOIN Products ON Orders.ProductId = Products.Id");

            TableReferences refs = script.CollectTableReferences();

            // Users, Orders, Products
            Assert.Equal(3, refs.Tables.Count);
            Assert.Equal(1, refs.Joins.Count);
        }

        [Fact]
        public void CollectColumnReferences_MergesAcrossStatements()
        {
            Script script = Script.Parse(
                "SELECT Name FROM Users; SELECT Total FROM Orders");

            IReadOnlyList<Expr.ColumnIdentifier> columns = script.CollectColumnReferences();

            Assert.Equal(2, columns.Count);
            Assert.Contains(columns, c => c.ColumnName.Name == "Name");
            Assert.Contains(columns, c => c.ColumnName.Name == "Total");
        }

        // #####################################################################
        // ##################### ReplaceWithTempTables ########################
        // #####################################################################

        [Fact]
        public void ReplaceWithTempTables_FlattensScripts()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users; SELECT * FROM Users");

            Script result = script.ReplaceWithTempTables("Users");

            // Each statement produces a SELECT INTO + modified query
            // The flattened script should have all of them
            Assert.True(result.Statements.Count >= 2);
            string source = result.ToSource();
            Assert.Contains("#Users", source);
        }

        // #####################################################################
        // ############################ Helpers ###############################
        // #####################################################################

        private static ColumnExistenceChecker CreateChecker(Dictionary<string, HashSet<string>> schema)
        {
            return (tableName, columnNames) =>
            {
                if (!schema.TryGetValue(tableName, out HashSet<string>? columns))
                {
                    return false;
                }
                foreach (string col in columnNames)
                {
                    if (!columns.Contains(col))
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        private static Dictionary<string, HashSet<string>> Schema(
            params (string table, string[] columns)[] tables)
        {
            Dictionary<string, HashSet<string>> result =
                new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach ((string table, string[] columns) in tables)
            {
                result[table] = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
            }
            return result;
        }
    }
}
