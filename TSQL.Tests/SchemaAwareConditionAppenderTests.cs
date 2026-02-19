using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class SchemaAwareConditionAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        /// <summary>
        /// Creates a ColumnExistenceChecker that checks against the provided table-column mapping.
        /// </summary>
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

        private static Dictionary<string, HashSet<string>> Schema(params (string table, string[] columns)[] tables)
        {
            Dictionary<string, HashSet<string>> result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach ((string? table, string[]? columns) in tables)
            {
                result[table] = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
            }
            return result;
        }


        #region Single Table

        [Fact]
        public void SingleTable_ColumnExists_AddsConditionWithPrefix()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0 OR I_ID = 1", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.I_ID = 0 OR T1.I_ID = 1", stmt.ToSource());
        }

        [Fact]
        public void SingleTable_ColumnDoesNotExist_NoConditionAdded()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "OTHER_COL" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1", stmt.ToSource());
        }

        [Fact]
        public void SingleTable_WithAlias_UseAliasAsPrefix()
        {
            Stmt stmt = Parse("SELECT * FROM T1 AS A");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 AS A WHERE A.I_ID = 0", stmt.ToSource());
        }

        #endregion

        #region Multi-Table JOIN

        [Fact]
        public void Join_BothTablesHaveColumn_BothGetConditions()
        {
            Stmt stmt = Parse("SELECT T1.ID, T2.ID FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "T1_ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0 OR I_ID = 1", CreateChecker(schema));

            Assert.Equal(
                "SELECT T1.ID, T2.ID FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE (T1.I_ID = 0 OR T1.I_ID = 1) AND (T2.I_ID = 0 OR T2.I_ID = 1)",
                stmt.ToSource());
        }

        [Fact]
        public void Join_OnlyOneTableHasColumn_OnlyThatTableGetsCondition()
        {
            Stmt stmt = Parse("SELECT T1.ID, T2.ID FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "T1_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0 OR I_ID = 1", CreateChecker(schema));

            Assert.Equal(
                "SELECT T1.ID, T2.ID FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE T1.I_ID = 0 OR T1.I_ID = 1",
                stmt.ToSource());
        }

        [Fact]
        public void Join_NeitherTableHasColumn_NoConditionAdded()
        {
            Stmt stmt = Parse("SELECT T1.ID, T2.ID FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID" }),
                ("T2", new[] { "ID", "T1_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT T1.ID, T2.ID FROM T1 JOIN T2 ON T1.ID = T2.T1_ID",
                stmt.ToSource());
        }

        [Fact]
        public void Join_WithAliases_UsesAliasesAsPrefix()
        {
            Stmt stmt = Parse("SELECT A.ID, B.ID FROM T1 AS A JOIN T2 AS B ON A.ID = B.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "T1_ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 5", CreateChecker(schema));

            Assert.Equal(
                "SELECT A.ID, B.ID FROM T1 AS A JOIN T2 AS B ON A.ID = B.T1_ID WHERE A.I_ID = 5 AND B.I_ID = 5",
                stmt.ToSource());
        }

        #endregion

        #region Already-Prefixed Condition

        [Fact]
        public void AlreadyPrefixed_FallsBackToRegularAddCondition()
        {
            Stmt stmt = Parse("SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "T1_ID", "I_ID" }));

            // Condition is already prefixed with T1 - should fall back to regular AddCondition
            stmt.AddSchemaAwareCondition("T1.I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE T1.I_ID = 0",
                stmt.ToSource());
        }

        #endregion

        #region Subquery in FROM

        [Fact]
        public void FromSubquery_ConditionAddedToSubqueryTables()
        {
            Stmt stmt = Parse("SELECT * FROM (SELECT * FROM T1) AS Sub1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM T1 WHERE T1.I_ID = 0) AS Sub1",
                stmt.ToSource());
        }

        #endregion

        #region Subquery in IN

        [Fact]
        public void InSubquery_ConditionAddedPerTargetFlags()
        {
            Stmt stmt = Parse("SELECT * FROM T1 WHERE T1.ID IN (SELECT T2.ID FROM T2)");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 WHERE T1.ID IN (SELECT T2.ID FROM T2 WHERE T2.I_ID = 0) AND T1.I_ID = 0",
                stmt.ToSource());
        }

        #endregion

        #region EXISTS Subquery

        [Fact]
        public void ExistsSubquery_ConditionAddedToSubqueryTables()
        {
            Stmt stmt = Parse("SELECT * FROM T1 WHERE EXISTS (SELECT 1 FROM T2 WHERE T2.ID = T1.ID)");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 WHERE EXISTS (SELECT 1 FROM T2 WHERE T2.ID = T1.ID AND T2.I_ID = 0) AND T1.I_ID = 0",
                stmt.ToSource());
        }

        #endregion

        #region CTE Query

        [Fact]
        public void Cte_ConditionAddedToCteAndOuterQuery()
        {
            Stmt stmt = Parse("WITH CTE AS (SELECT * FROM T1) SELECT * FROM CTE JOIN T2 ON CTE.ID = T2.ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            // CTE references T1 (has I_ID), outer query references CTE (not a physical table) and T2 (has I_ID)
            Assert.Equal(
                "WITH CTE AS (SELECT * FROM T1 WHERE T1.I_ID = 0) SELECT * FROM CTE JOIN T2 ON CTE.ID = T2.ID WHERE T2.I_ID = 0",
                stmt.ToSource());
        }

        #endregion

        #region WhereClauseTarget Flags

        [Fact]
        public void OutermostQueryOnly_OnlyOuterQueryGetsCondition()
        {
            Stmt stmt = Parse("SELECT * FROM T1 WHERE T1.ID IN (SELECT T2.ID FROM T2)");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema),
                target: WhereClauseTarget.OutermostQuery);

            Assert.Equal(
                "SELECT * FROM T1 WHERE T1.ID IN (SELECT T2.ID FROM T2) AND T1.I_ID = 0",
                stmt.ToSource());
        }

        #endregion

        #region UNION / SET Operations

        [Fact]
        public void Union_BothSidesGetConditions()
        {
            Stmt stmt = Parse("SELECT * FROM T1 UNION ALL SELECT * FROM T2");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "I_ID" }),
                ("T2", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 WHERE T1.I_ID = 0 UNION ALL SELECT * FROM T2 WHERE T2.I_ID = 0",
                stmt.ToSource());
        }

        [Fact]
        public void Union_OnlyOneSideHasColumn()
        {
            Stmt stmt = Parse("SELECT * FROM T1 UNION ALL SELECT * FROM T2");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "I_ID" }),
                ("T2", new[] { "OTHER" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 WHERE T1.I_ID = 0 UNION ALL SELECT * FROM T2",
                stmt.ToSource());
        }

        #endregion

        #region Mixed Prefixed and Unprefixed Columns

        [Fact]
        public void MixedPrefix_BothTablesHaveUnprefixed_PrefixedAddedOnceUnprefixedPerTable()
        {
            // T1.I_ID is already prefixed, STATUS is unprefixed.
            // T1.I_ID = 0 added once; STATUS = 1 added per matching table.
            Stmt stmt = Parse("SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID", "STATUS" }),
                ("T2", new[] { "ID", "T1_ID", "STATUS" }));

            stmt.AddSchemaAwareCondition("T1.I_ID = 0 AND STATUS = 1", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE T1.I_ID = 0 AND T1.STATUS = 1 AND T2.STATUS = 1",
                stmt.ToSource());
        }

        [Fact]
        public void MixedPrefix_OnlyOneTableHasUnprefixedColumn_PrefixedAndOneTableUnprefixed()
        {
            // T1.I_ID is already prefixed, STATUS is unprefixed.
            // Only T1 has STATUS. T1.I_ID = 0 added once, STATUS = 1 added for T1 only.
            Stmt stmt = Parse("SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID", "STATUS" }),
                ("T2", new[] { "ID", "T1_ID" }));

            stmt.AddSchemaAwareCondition("T1.I_ID = 0 AND STATUS = 1", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE T1.I_ID = 0 AND T1.STATUS = 1",
                stmt.ToSource());
        }

        [Fact]
        public void MixedPrefix_NeitherTableHasUnprefixedColumn_PrefixedPartsStillAdded()
        {
            // T1.I_ID is already prefixed, STATUS is unprefixed.
            // Neither table has STATUS, but T1.I_ID = 0 should still be added.
            Stmt stmt = Parse("SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "T1_ID" }));

            stmt.AddSchemaAwareCondition("T1.I_ID = 0 AND STATUS = 1", CreateChecker(schema));

            Assert.Equal(
                "SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE T1.I_ID = 0",
                stmt.ToSource());
        }

        #endregion

        #region Multiple Columns in Condition

        [Fact]
        public void MultipleColumnsInCondition_AllMustExist()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            // Table only has I_ID, not STATUS
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0 AND STATUS = 1", CreateChecker(schema));

            // Both I_ID and STATUS must exist - since STATUS doesn't, no condition added
            Assert.Equal("SELECT * FROM T1", stmt.ToSource());
        }

        [Fact]
        public void MultipleColumnsInCondition_AllExist_ConditionAdded()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID", "STATUS" }));

            stmt.AddSchemaAwareCondition("I_ID = 0 AND STATUS = 1", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.I_ID = 0 AND T1.STATUS = 1", stmt.ToSource());
        }

        #endregion

        #region Existing WHERE Clause

        [Fact]
        public void ExistingWhere_ConditionCombinedWithAnd()
        {
            Stmt stmt = Parse("SELECT * FROM T1 WHERE T1.NAME = 'Test'");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "NAME", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.NAME = 'Test' AND T1.I_ID = 0", stmt.ToSource());
        }

        #endregion

        #region Cross Join

        [Fact]
        public void CrossJoin_BothTablesGetConditions()
        {
            Stmt stmt = Parse("SELECT * FROM T1 CROSS JOIN T2");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "I_ID" }),
                ("T2", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 CROSS JOIN T2 WHERE T1.I_ID = 0 AND T2.I_ID = 0", stmt.ToSource());
        }

        #endregion

        #region Empty/Whitespace Condition

        [Fact]
        public void EmptyCondition_NoChange()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1", stmt.ToSource());
        }

        [Fact]
        public void WhitespaceCondition_NoChange()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("   ", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1", stmt.ToSource());
        }

        #endregion

        #region Predicate Type Coverage

        [Fact]
        public void Like_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "NAME" }));

            stmt.AddSchemaAwareCondition("NAME LIKE '%test%'", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.NAME LIKE '%test%'", stmt.ToSource());
        }

        [Fact]
        public void Between_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "AGE" }));

            stmt.AddSchemaAwareCondition("AGE BETWEEN 18 AND 65", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.AGE BETWEEN 18 AND 65", stmt.ToSource());
        }

        [Fact]
        public void IsNull_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS" }));

            stmt.AddSchemaAwareCondition("STATUS IS NULL", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.STATUS IS NULL", stmt.ToSource());
        }

        [Fact]
        public void Freetext_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "DESCRIPTION" }));

            stmt.AddSchemaAwareCondition("FREETEXT(DESCRIPTION, 'search')", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE FREETEXT(T1.DESCRIPTION, 'search')", stmt.ToSource());
        }

        [Fact]
        public void InValueList_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS" }));

            stmt.AddSchemaAwareCondition("STATUS IN (1, 2, 3)", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.STATUS IN (1, 2, 3)", stmt.ToSource());
        }

        [Fact]
        public void Quantifier_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "PRICE" }));

            stmt.AddSchemaAwareCondition("PRICE > ALL (SELECT 1)", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.PRICE > ALL (SELECT 1)", stmt.ToSource());
        }

        [Fact]
        public void Exists_FallsBackToRegularAddCondition()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "ID" }));

            // EXISTS has no unprefixed columns — falls back to regular AddCondition
            stmt.AddSchemaAwareCondition("EXISTS (SELECT 1 FROM T2)", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE EXISTS (SELECT 1 FROM T2)", stmt.ToSource());
        }

        [Fact]
        public void PredicateGrouping_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS" }));

            stmt.AddSchemaAwareCondition("(STATUS = 1)", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE (T1.STATUS = 1)", stmt.ToSource());
        }

        [Fact]
        public void And_AllColumnsGetPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS", "AGE" }));

            stmt.AddSchemaAwareCondition("STATUS = 1 AND AGE > 18", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.STATUS = 1 AND T1.AGE > 18", stmt.ToSource());
        }

        [Fact]
        public void Or_AllColumnsGetPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS", "AGE" }));

            stmt.AddSchemaAwareCondition("STATUS = 1 OR AGE > 18", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.STATUS = 1 OR T1.AGE > 18", stmt.ToSource());
        }

        [Fact]
        public void Not_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS" }));

            stmt.AddSchemaAwareCondition("NOT STATUS = 1", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE NOT T1.STATUS = 1", stmt.ToSource());
        }

        [Fact]
        public void Contains_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "DESCRIPTION" }));

            stmt.AddSchemaAwareCondition("CONTAINS(DESCRIPTION, 'search')", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE CONTAINS(T1.DESCRIPTION, 'search')", stmt.ToSource());
        }

        [Fact]
        public void InSubquery_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "STATUS" }));

            stmt.AddSchemaAwareCondition("STATUS IN (SELECT STATUS FROM T2)", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.STATUS IN (SELECT STATUS FROM T2)", stmt.ToSource());
        }

        [Fact]
        public void LikeWithEscape_ColumnGetsPrefixed()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "NAME" }));

            stmt.AddSchemaAwareCondition("NAME LIKE '%[_]test' ESCAPE '['", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.NAME LIKE '%[_]test' ESCAPE '['", stmt.ToSource());
        }

        #endregion

        #region Parameterized Overload

        [Fact]
        public void Parameterized_BasicSubstitution()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = @P0",
                new object[] { 42 }, CreateChecker(schema), out IReadOnlyDictionary<string, object>? parameters);

            Assert.Equal("SELECT * FROM T1 WHERE T1.I_ID = @P0", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(42, parameters["@P0"]);
        }

        [Fact]
        public void Parameterized_NamedValues()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = @TenantId",
                new object[] { ("@TenantId", 99) }, CreateChecker(schema), out IReadOnlyDictionary<string, object>? parameters);

            Assert.Equal("SELECT * FROM T1 WHERE T1.I_ID = @TenantId", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(99, parameters["@TenantId"]);
        }

        [Fact]
        public void Parameterized_CollisionWithExistingVariable()
        {
            Stmt stmt = Parse("SELECT * FROM T1 WHERE T1.NAME = @P0");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "NAME", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = @P0",
                new object[] { 7 }, CreateChecker(schema), out IReadOnlyDictionary<string, object>? parameters);

            Assert.Equal("SELECT * FROM T1 WHERE T1.NAME = @P0 AND T1.I_ID = @P0_1", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(7, parameters["@P0_1"]);
        }

        [Fact]
        public void Parameterized_WithColumnPrefixing_JoinBothTablesMatch()
        {
            Stmt stmt = Parse("SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID");
            Dictionary<string, HashSet<string>> schema = Schema(
                ("T1", new[] { "ID", "I_ID" }),
                ("T2", new[] { "ID", "T1_ID", "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = @Filter",
                new object[] { ("@Filter", 5) }, CreateChecker(schema), out IReadOnlyDictionary<string, object>? parameters);

            Assert.Equal(
                "SELECT * FROM T1 JOIN T2 ON T1.ID = T2.T1_ID WHERE T1.I_ID = @Filter AND T2.I_ID = @Filter",
                stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(5, parameters["@Filter"]);
        }

        [Fact]
        public void Parameterized_Chaining_MultipleCallsWithParameters()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID", "STATUS" }));

            stmt.AddSchemaAwareCondition("I_ID = @P0",
                new object[] { 1 }, CreateChecker(schema), out IReadOnlyDictionary<string, object>? p1);
            stmt.AddSchemaAwareCondition("STATUS = @P0",
                new object[] { 2 }, CreateChecker(schema), out IReadOnlyDictionary<string, object>? p2);

            Assert.Equal("SELECT * FROM T1 WHERE T1.I_ID = @P0 AND T1.STATUS = @P0_1", stmt.ToSource());
            Assert.Single(p1);
            Assert.Equal(1, p1["@P0"]);
            Assert.Single(p2);
            Assert.Equal(2, p2["@P0_1"]);
        }

        #endregion

        #region Bracketed Table Names

        [Fact]
        public void BracketedTableName_MatchesSchemaCallback_AddsCondition()
        {
            Stmt stmt = Parse("SELECT * FROM [FROM]");
            Dictionary<string, HashSet<string>> schema = Schema(("FROM", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM [FROM] WHERE [FROM].I_ID = 0", stmt.ToSource());
        }

        [Fact]
        public void BracketedTableName_WithAlias_UsesAliasAsPrefix()
        {
            Stmt stmt = Parse("SELECT * FROM [T1] AS [A]");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM [T1] AS [A] WHERE [A].I_ID = 0", stmt.ToSource());
        }

        [Fact]
        public void UnbracketedTableName_PrefixHasNoBrackets()
        {
            Stmt stmt = Parse("SELECT * FROM T1");
            Dictionary<string, HashSet<string>> schema = Schema(("T1", new[] { "I_ID" }));

            stmt.AddSchemaAwareCondition("I_ID = 0", CreateChecker(schema));

            Assert.Equal("SELECT * FROM T1 WHERE T1.I_ID = 0", stmt.ToSource());
        }

        #endregion
    }
}
