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

            Assert.Equal(
                "SELECT * FROM Users WHERE Active = 1; SELECT * FROM Orders WHERE Active = 1",
                script.ToSource());
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

            // @Status collides with stmt1's existing @Status, so renamed to @Status_1
            Assert.Equal(
                "SELECT * FROM Users WHERE Id = @Status AND Status = @Status_1; SELECT * FROM Orders WHERE Status = @Status_1",
                script.ToSource());
            Assert.Single(parameters);
            Assert.Equal(1, parameters["@Status_1"]);
        }

        [Fact]
        public void AddCondition_WithParams_NoCollision()
        {
            Script script = Script.Parse("SELECT * FROM Users; SELECT * FROM Orders");

            script.AddCondition("TenantId = @TenantId",
                new object[] { ("@TenantId", 42) },
                out IReadOnlyDictionary<string, object> parameters);

            Assert.Equal(
                "SELECT * FROM Users WHERE TenantId = @TenantId; SELECT * FROM Orders WHERE TenantId = @TenantId",
                script.ToSource());
            Assert.Equal(42, parameters["@TenantId"]);
        }

        [Fact]
        public void AddCondition_SkipsNonSelectStatements()
        {
            Script script = Script.Parse("DROP TABLE #Temp; SELECT * FROM Users");

            script.AddCondition("Active = 1");

            Assert.Equal(
                "DROP TABLE #Temp; SELECT * FROM Users WHERE Active = 1",
                script.ToSource());
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
            Assert.Equal(
                "SELECT * FROM Users WHERE Name = @P0; SELECT * FROM Orders WHERE Total = @P1",
                script.ToSource());
            Assert.Equal(2, parameters.Count);
            Assert.Equal("Alice", parameters["@P0"]);
            Assert.Equal(100, parameters["@P1"]);
        }

        [Fact]
        public void Parameterize_SkipsExistingVariableNames()
        {
            Script script = Script.Parse(
                "SELECT * FROM Users WHERE Id = @P0; SELECT * FROM Orders WHERE Total = 99");

            script.Parameterize(out IReadOnlyDictionary<string, object> parameters);

            // @P0 already exists in stmt1, so the literal 99 should get @P1 (not @P0)
            Assert.Equal(
                "SELECT * FROM Users WHERE Id = @P0; SELECT * FROM Orders WHERE Total = @P1",
                script.ToSource());
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

            // Both tables have TenantId, so both get the condition prefixed with table name
            Assert.Equal(
                "SELECT * FROM Users WHERE Users.TenantId = 1; SELECT * FROM Orders WHERE Orders.TenantId = 1",
                script.ToSource());
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

            // @TenantId collides with stmt1's existing @TenantId, so renamed to @TenantId_1
            Assert.Equal(
                "SELECT * FROM Users WHERE Id = @TenantId AND Users.TenantId = @TenantId_1; SELECT * FROM Orders WHERE Orders.TenantId = @TenantId_1",
                script.ToSource());
            Assert.Single(parameters);
            Assert.Equal(5, parameters["@TenantId_1"]);
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
            Assert.Single(refs.Joins);
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

            // Each statement produces a SELECT INTO + modified query (2 per input = 4 total)
            Assert.Equal(4, result.Statements.Count);
            Assert.Equal(
                "SELECT * INTO #Users FROM Users;\nSELECT * FROM #Users;\nSELECT * INTO #Users FROM Users;\nSELECT * FROM #Users",
                result.ToSource());
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
