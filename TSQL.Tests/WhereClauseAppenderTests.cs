using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class WhereClauseAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        #region No Existing WHERE

        [Fact]
        public void AddCondition_NoExistingWhere_AddsWhereClause()
        {
            string sql = "SELECT * FROM Users";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal("SELECT * FROM Users WHERE Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_NoExistingWhere_WithOrCondition_DoesNotWrap()
        {
            string sql = "SELECT * FROM Users";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Status = 1 OR Status = 2");

            Assert.Equal("SELECT * FROM Users WHERE Status = 1 OR Status = 2", stmt.ToSource());
        }

        #endregion

        #region Existing WHERE

        [Fact]
        public void AddCondition_ExistingWhere_CombinesWithAnd()
        {
            string sql = "SELECT * FROM Users WHERE Status = 1";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal("SELECT * FROM Users WHERE Status = 1 AND Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_ExistingWhereWithOr_WrapsExistingInParentheses()
        {
            string sql = "SELECT * FROM Users WHERE Status = 1 OR Status = 2";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal("SELECT * FROM Users WHERE (Status = 1 OR Status = 2) AND Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_ExistingWhereSimple_NewConditionWithOr_WrapsNewInParentheses()
        {
            string sql = "SELECT * FROM Users WHERE Active = 1";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Status = 1 OR Status = 2");

            Assert.Equal("SELECT * FROM Users WHERE Active = 1 AND (Status = 1 OR Status = 2)", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_BothOr_WrapsBothInParentheses()
        {
            string sql = "SELECT * FROM Users WHERE A = 1 OR B = 2";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("C = 3 OR D = 4");

            Assert.Equal("SELECT * FROM Users WHERE (A = 1 OR B = 2) AND (C = 3 OR D = 4)", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_ExistingWhereWithAnd_DoesNotWrap()
        {
            string sql = "SELECT * FROM Users WHERE A = 1 AND B = 2";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("C = 3");

            Assert.Equal("SELECT * FROM Users WHERE A = 1 AND B = 2 AND C = 3", stmt.ToSource());
        }

        #endregion

        #region UNION / Set Operations

        [Fact]
        public void AddCondition_Union_AddsToBothSides()
        {
            string sql = "SELECT * FROM Users UNION SELECT * FROM Admins";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal("SELECT * FROM Users WHERE Active = 1 UNION SELECT * FROM Admins WHERE Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_UnionAll_AddsToBothSides()
        {
            string sql = "SELECT * FROM Users UNION ALL SELECT * FROM Admins";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal("SELECT * FROM Users WHERE Active = 1 UNION ALL SELECT * FROM Admins WHERE Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_UnionWithExistingWhere_CombinesCorrectly()
        {
            string sql = "SELECT * FROM Users WHERE Role = 'user' UNION SELECT * FROM Admins WHERE Role = 'admin'";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal(
                "SELECT * FROM Users WHERE Role = 'user' AND Active = 1 UNION SELECT * FROM Admins WHERE Role = 'admin' AND Active = 1",
                stmt.ToSource());
        }

        #endregion

        #region OutermostQuery Only (Default)

        [Fact]
        public void AddCondition_Default_DoesNotEnterSubqueries()
        {
            string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Active)";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("TenantId = 1");

            string result = stmt.ToSource();
            Assert.Contains("WHERE Id IN (SELECT UserId FROM Active)", result);
            Assert.Contains("TenantId = 1", result);
        }

        [Fact]
        public void AddCondition_Default_DoesNotEnterCtes()
        {
            string sql = "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1");

            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte WHERE Active = 1",
                stmt.ToSource());
        }

        #endregion

        #region WhereClauseTarget.All

        [Fact]
        public void AddCondition_All_EntersInSubquery()
        {
            string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Active)";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("TenantId = 1", WhereClauseTarget.All);

            Assert.Equal(
                "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Active WHERE TenantId = 1) AND TenantId = 1",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_All_EntersFromSubquery()
        {
            string sql = "SELECT * FROM (SELECT * FROM Users) AS sub";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.All);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users WHERE Active = 1) AS sub WHERE Active = 1",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_All_EntersCte()
        {
            string sql = "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.All);

            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users WHERE Active = 1) SELECT * FROM cte WHERE Active = 1",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_All_EntersExistsSubquery()
        {
            string sql = "SELECT * FROM Users WHERE EXISTS (SELECT 1 FROM Roles WHERE Roles.UserId = Users.Id)";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.All);

            Assert.Equal(
                "SELECT * FROM Users WHERE EXISTS (SELECT 1 FROM Roles WHERE Roles.UserId = Users.Id AND Active = 1) AND Active = 1",
                stmt.ToSource());
        }

        #endregion

        #region Individual Flags

        [Fact]
        public void AddCondition_OnlyFromSubqueries_AddsToFromButNotOuter()
        {
            string sql = "SELECT * FROM (SELECT * FROM Users) AS sub";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.FromSubqueries);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users WHERE Active = 1) AS sub",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_OnlyInSubqueries_AddsToInButNotOuter()
        {
            string sql = "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Active)";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("TenantId = 1", WhereClauseTarget.InSubqueries);

            Assert.Equal(
                "SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Active WHERE TenantId = 1)",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_OnlyExistsSubqueries_AddsToExistsButNotOuter()
        {
            string sql = "SELECT * FROM Users WHERE EXISTS (SELECT 1 FROM Roles WHERE Roles.UserId = Users.Id)";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.ExistsSubqueries);

            Assert.Equal(
                "SELECT * FROM Users WHERE EXISTS (SELECT 1 FROM Roles WHERE Roles.UserId = Users.Id AND Active = 1)",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_OnlyCtes_AddsToCteButNotOuter()
        {
            string sql = "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.Ctes);

            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users WHERE Active = 1) SELECT * FROM cte",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_CombinedFlags_OutermostAndCtes()
        {
            string sql = "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte WHERE Id IN (SELECT UserId FROM Active)";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.OutermostQuery | WhereClauseTarget.Ctes);

            string result = stmt.ToSource();
            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users WHERE Active = 1) SELECT * FROM cte WHERE Id IN (SELECT UserId FROM Active) AND Active = 1",
                result);
        }

        [Fact]
        public void AddCondition_None_DoesNothing()
        {
            string sql = "SELECT * FROM Users";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("Active = 1", WhereClauseTarget.None);

            Assert.Equal("SELECT * FROM Users", stmt.ToSource());
        }

        #endregion

        // When the condition itself contains subqueries (EXISTS, IN, quantifier) and the target
        // includes matching subquery flags, the walker must not re-enter HandleQueryExpression
        // for the freshly-added predicate's subqueries. These tests verify that the condition
        // is added exactly once and does not cause infinite recursion.
        #region Condition With Subqueries

        [Fact]
        public void ConditionWithExists_TargetAll_AddsOnceWithoutRecursion()
        {
            string sql = "SELECT * FROM T1";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("EXISTS (SELECT 1 FROM T2)", WhereClauseTarget.All);

            Assert.Equal("SELECT * FROM T1 WHERE EXISTS (SELECT 1 FROM T2)", stmt.ToSource());
        }

        [Fact]
        public void ConditionWithInSubquery_TargetAll_AddsOnceWithoutRecursion()
        {
            string sql = "SELECT * FROM T1";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("ID IN (SELECT ID FROM T2)", WhereClauseTarget.All);

            Assert.Equal("SELECT * FROM T1 WHERE ID IN (SELECT ID FROM T2)", stmt.ToSource());
        }

        [Fact]
        public void ConditionWithQuantifier_TargetAll_AddsOnceWithoutRecursion()
        {
            string sql = "SELECT * FROM T1";
            Stmt stmt = Parse(sql);

            stmt.AddCondition("PRICE > ALL (SELECT PRICE FROM T2)", WhereClauseTarget.All);

            Assert.Equal("SELECT * FROM T1 WHERE PRICE > ALL (SELECT PRICE FROM T2)", stmt.ToSource());
        }

        #endregion

        #region ParseSearchCondition

        [Fact]
        public void ParseSearchCondition_ParsesSimpleComparison()
        {
            var predicate = AST.Predicate.ParsePredicate("Active = 1");

            Assert.IsType<AST.Predicate.Comparison>(predicate);
            Assert.Equal("Active = 1", predicate.ToSource());
        }

        [Fact]
        public void ParseSearchCondition_ParsesCompoundCondition()
        {
            var predicate = AST.Predicate.ParsePredicate("A = 1 AND B = 2");

            Assert.IsType<AST.Predicate.And>(predicate);
            Assert.Equal("A = 1 AND B = 2", predicate.ToSource());
        }

        #endregion
    }
}
