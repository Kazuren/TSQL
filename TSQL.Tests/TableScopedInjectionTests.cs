using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class TableScopedInjectionTests
    {
        #region AddCondition(targetTable) — Simple SELECT

        [Fact]
        public void AddCondition_SimpleSelect_MatchingTable_AddsWhere()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddCondition("Active = 1", "Users");

            Assert.Equal("SELECT a FROM Users WHERE Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_SimpleSelect_NonMatchingTable_NoChange()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddCondition("Active = 1", "Orders");

            Assert.Equal("SELECT a FROM Users", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_SimpleSelect_ExistingWhere_CombinesWithAnd()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users WHERE Status = 1");

            stmt.AddCondition("Active = 1", "Users");

            Assert.Equal("SELECT a FROM Users WHERE Status = 1 AND Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddCondition_SimpleSelect_CaseInsensitiveTableMatch()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM users");

            stmt.AddCondition("Active = 1", "USERS");

            Assert.Equal("SELECT a FROM users WHERE Active = 1", stmt.ToSource());
        }

        #endregion

        #region AddCondition(targetTable) — FROM Subquery

        [Fact]
        public void AddCondition_FromSubquery_TargetsInnerOnly()
        {
            var stmt = Stmt.ParseSelect("SELECT * FROM (SELECT * FROM Users) AS sub");

            stmt.AddCondition("Active = 1", "Users");

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users WHERE Active = 1) AS sub",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_FromSubquery_TargetsOuterOnly()
        {
            var stmt = Stmt.ParseSelect("SELECT * FROM (SELECT * FROM Users) AS sub");

            stmt.AddCondition("x = 1", "sub");

            // sub is an alias for the subquery, not a real table — no match
            Assert.Equal("SELECT * FROM (SELECT * FROM Users) AS sub", stmt.ToSource());
        }

        #endregion

        #region AddCondition(targetTable) — CTE

        [Fact]
        public void AddCondition_Cte_TargetsTableInsideCte()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT * FROM Users) SELECT * FROM cte");

            stmt.AddCondition("Active = 1", "Users");

            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users WHERE Active = 1) SELECT * FROM cte",
                stmt.ToSource());
        }

        #endregion

        #region AddCondition(targetTable) — UNION

        [Fact]
        public void AddCondition_Union_BothSidesReferenceTable_AddsToBoth()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users UNION SELECT b FROM Users");

            stmt.AddCondition("Active = 1", "Users");

            Assert.Equal(
                "SELECT a FROM Users WHERE Active = 1 UNION SELECT b FROM Users WHERE Active = 1",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_Union_OnlyOneSideReferencesTable_AddsToThatSideOnly()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users UNION SELECT b FROM Admins");

            stmt.AddCondition("Active = 1", "Users");

            Assert.Equal(
                "SELECT a FROM Users WHERE Active = 1 UNION SELECT b FROM Admins",
                stmt.ToSource());
        }

        #endregion

        #region AddCondition(targetTable) — JOIN

        [Fact]
        public void AddCondition_JoinedTable_MatchesJoinTarget()
        {
            var stmt = Stmt.ParseSelect("SELECT u.Id FROM Orders o JOIN Users u ON o.UserId = u.Id");

            stmt.AddCondition("u.Active = 1", "Users");

            Assert.Equal(
                "SELECT u.Id FROM Orders o JOIN Users u ON o.UserId = u.Id WHERE u.Active = 1",
                stmt.ToSource());
        }

        #endregion

        #region AddSelectColumn(targetTable) — Simple SELECT

        [Fact]
        public void AddSelectColumn_SimpleSelect_MatchingTable_PrependsColumn()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddSelectColumn("b", "Users");

            Assert.Equal("SELECT b, a FROM Users", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_SimpleSelect_NonMatchingTable_NoChange()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddSelectColumn("b", "Orders");

            Assert.Equal("SELECT a FROM Users", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_SimpleSelect_QualifiedColumn()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users u");

            stmt.AddSelectColumn("u.Id AS UserId", "Users");

            Assert.Equal("SELECT u.Id AS UserId, a FROM Users u", stmt.ToSource());
        }

        #endregion

        #region AddSelectColumn(targetTable) — FROM Subquery

        [Fact]
        public void AddSelectColumn_FromSubquery_TargetsInnerOnly()
        {
            var stmt = Stmt.ParseSelect("SELECT * FROM (SELECT a FROM Users) AS sub");

            stmt.AddSelectColumn("b", "Users");

            Assert.Equal(
                "SELECT * FROM (SELECT b, a FROM Users) AS sub",
                stmt.ToSource());
        }

        #endregion

        #region AddSelectColumn(targetTable) — UNION

        [Fact]
        public void AddSelectColumn_Union_BothSidesReferenceTable_PrependsToBoth()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users UNION SELECT b FROM Users");

            stmt.AddSelectColumn("x", "Users");

            Assert.Equal(
                "SELECT x, a FROM Users UNION SELECT x, b FROM Users",
                stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_Union_OnlyOneSideReferencesTable_PrependsToThatSideOnly()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users UNION SELECT b FROM Admins");

            stmt.AddSelectColumn("x", "Users");

            Assert.Equal(
                "SELECT x, a FROM Users UNION SELECT b FROM Admins",
                stmt.ToSource());
        }

        #endregion

        #region AddSelectColumn(targetTable) — CTE

        [Fact]
        public void AddSelectColumn_Cte_TargetsTableInsideCte()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT a FROM Users) SELECT * FROM cte");

            stmt.AddSelectColumn("b", "Users");

            Assert.Equal(
                "WITH cte AS (SELECT b, a FROM Users) SELECT * FROM cte",
                stmt.ToSource());
        }

        #endregion

        #region AddInto — Simple SELECT

        [Fact]
        public void AddInto_SimpleSelect_AddsIntoClause()
        {
            var stmt = Stmt.ParseSelect("SELECT a, b FROM Users");

            stmt.AddInto("#tmp");

            Assert.Equal("SELECT a, b INTO #tmp FROM Users", stmt.ToSource());
        }

        [Fact]
        public void AddInto_SimpleSelect_SchemaQualifiedName()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddInto("dbo.Results");

            Assert.Equal("SELECT a INTO dbo.Results FROM Users", stmt.ToSource());
        }

        [Fact]
        public void AddInto_ReplacesExistingInto()
        {
            var stmt = Stmt.ParseSelect("SELECT a INTO #old FROM Users");

            stmt.AddInto("#new");

            Assert.Equal("SELECT a INTO #new FROM Users", stmt.ToSource());
        }

        #endregion

        #region AddInto — Chaining

        [Fact]
        public void AddInto_ReturnsSameInstance_ForChaining()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            var result = stmt.AddInto("#tmp");

            Assert.Same(stmt, result);
        }

        #endregion

        #region AddCondition(targetTable) — traverse/mutate scope

        [Fact]
        public void AddCondition_TraverseOutermostOnly_SkipsCte()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT * FROM Users) SELECT * FROM cte");

            stmt.AddCondition("Active = 1", "Users",
                traverse: QueryScope.OutermostQuery);

            // CTE not entered, outer has no Users table → no change
            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users) SELECT * FROM cte",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_TraverseAll_MutateOutermostOnly_TraversesCteButDoesNotMutateIt()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT * FROM Users) SELECT * FROM Users");

            stmt.AddCondition("Active = 1", "Users",
                traverse: QueryScope.All,
                mutate: QueryScope.OutermostQuery);

            // CTE is traversed but not mutated; outermost References Users → mutated
            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users) SELECT * FROM Users WHERE Active = 1",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_TraverseAll_MutateCteOnly()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT * FROM Users) SELECT * FROM Users");

            stmt.AddCondition("Active = 1", "Users",
                traverse: QueryScope.All,
                mutate: QueryScope.Ctes);

            // CTE mutated, outermost not
            Assert.Equal(
                "WITH cte AS (SELECT * FROM Users WHERE Active = 1) SELECT * FROM Users",
                stmt.ToSource());
        }

        [Fact]
        public void AddCondition_TraverseAll_MutateFromSubqueries()
        {
            var stmt = Stmt.ParseSelect("SELECT * FROM (SELECT * FROM Users) AS sub");

            stmt.AddCondition("Active = 1", "Users",
                traverse: QueryScope.All,
                mutate: QueryScope.FromSubqueries);

            // Inner FROM subquery mutated, outer not
            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users WHERE Active = 1) AS sub",
                stmt.ToSource());
        }

        #endregion

        #region AddSelectColumn(targetTable) — traverse/mutate scope

        [Fact]
        public void AddSelectColumn_TraverseOutermostOnly_SkipsCte()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT a FROM Users) SELECT * FROM cte");

            stmt.AddSelectColumn("b", "Users",
                traverse: QueryScope.OutermostQuery);

            // CTE not entered → no change
            Assert.Equal(
                "WITH cte AS (SELECT a FROM Users) SELECT * FROM cte",
                stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_TraverseAll_MutateOutermostOnly()
        {
            var stmt = Stmt.ParseSelect("WITH cte AS (SELECT a FROM Users) SELECT x FROM Users");

            stmt.AddSelectColumn("b", "Users",
                traverse: QueryScope.All,
                mutate: QueryScope.OutermostQuery);

            // CTE traversed but not mutated; outermost mutated
            Assert.Equal(
                "WITH cte AS (SELECT a FROM Users) SELECT b, x FROM Users",
                stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_TraverseAll_MutateSubqueries()
        {
            var stmt = Stmt.ParseSelect("SELECT * FROM (SELECT a FROM Users) AS sub");

            stmt.AddSelectColumn("b", "Users",
                traverse: QueryScope.All,
                mutate: QueryScope.AllSubqueries);

            // FROM subquery mutated, outer not
            Assert.Equal(
                "SELECT * FROM (SELECT b, a FROM Users) AS sub",
                stmt.ToSource());
        }

        #endregion

        #region Expr.ParseObjectIdentifier

        [Fact]
        public void ParseObjectIdentifier_SimpleName()
        {
            var id = Expr.ParseObjectIdentifier("Users");

            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Null(id.SchemaName);
            Assert.Null(id.DatabaseName);
            Assert.Null(id.ServerName);
            Assert.Equal("Users", id.ToSource());
        }

        [Fact]
        public void ParseObjectIdentifier_SchemaQualified()
        {
            var id = Expr.ParseObjectIdentifier("dbo.Users");

            Assert.Equal("dbo", id.SchemaName.Name);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Null(id.DatabaseName);
            Assert.Equal("dbo.Users", id.ToSource());
        }

        [Fact]
        public void ParseObjectIdentifier_DatabaseSchemaObject()
        {
            var id = Expr.ParseObjectIdentifier("mydb.dbo.Users");

            Assert.Equal("mydb", id.DatabaseName.Name);
            Assert.Equal("dbo", id.SchemaName.Name);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("mydb.dbo.Users", id.ToSource());
        }

        [Fact]
        public void ParseObjectIdentifier_FourPart()
        {
            var id = Expr.ParseObjectIdentifier("srv.mydb.dbo.Users");

            Assert.Equal("srv", id.ServerName.Name);
            Assert.Equal("mydb", id.DatabaseName.Name);
            Assert.Equal("dbo", id.SchemaName.Name);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("srv.mydb.dbo.Users", id.ToSource());
        }

        [Fact]
        public void ParseObjectIdentifier_DoubleDot_SkipsSchema()
        {
            var id = Expr.ParseObjectIdentifier("mydb..Users");

            Assert.Equal("mydb", id.DatabaseName.Name);
            Assert.Null(id.SchemaName);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("mydb..Users", id.ToSource());
        }

        [Fact]
        public void ParseObjectIdentifier_TempTable()
        {
            var id = Expr.ParseObjectIdentifier("#tmp");

            Assert.Equal("#tmp", id.ObjectName.Name);
            Assert.Equal("#tmp", id.ToSource());
        }

        [Fact]
        public void ParseObjectIdentifier_ViaObjectIdentifierParse()
        {
            // Expr.ObjectIdentifier.Parse delegates to Expr.ParseObjectIdentifier
            var id = Expr.ObjectIdentifier.Parse("dbo.Users");

            Assert.Equal("dbo", id.SchemaName.Name);
            Assert.Equal("Users", id.ObjectName.Name);
        }

        [Fact]
        public void AddInto_DoubleDotSyntax_RoundTrips()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddInto("mydb..Results");

            Assert.Equal("SELECT a INTO mydb..Results FROM Users", stmt.ToSource());
        }

        #endregion

        #region Expr.ParseColumnIdentifier

        [Fact]
        public void ParseColumnIdentifier_SimpleName()
        {
            var id = Expr.ParseColumnIdentifier("Name");

            Assert.Equal("Name", id.ColumnName.Name);
            Assert.Null(id.ObjectName);
            Assert.Null(id.SchemaName);
            Assert.Null(id.DatabaseName);
            Assert.Equal("Name", id.ToSource());
        }

        [Fact]
        public void ParseColumnIdentifier_ObjectQualified()
        {
            var id = Expr.ParseColumnIdentifier("Users.Name");

            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("Name", id.ColumnName.Name);
            Assert.Null(id.SchemaName);
            Assert.Null(id.DatabaseName);
            Assert.Equal("Users.Name", id.ToSource());
        }

        [Fact]
        public void ParseColumnIdentifier_SchemaObjectColumn()
        {
            var id = Expr.ParseColumnIdentifier("dbo.Users.Name");

            Assert.Equal("dbo", id.SchemaName.Name);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("Name", id.ColumnName.Name);
            Assert.Null(id.DatabaseName);
            Assert.Equal("dbo.Users.Name", id.ToSource());
        }

        [Fact]
        public void ParseColumnIdentifier_FourPart()
        {
            var id = Expr.ParseColumnIdentifier("mydb.dbo.Users.Name");

            Assert.Equal("mydb", id.DatabaseName.Name);
            Assert.Equal("dbo", id.SchemaName.Name);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("Name", id.ColumnName.Name);
            Assert.Equal("mydb.dbo.Users.Name", id.ToSource());
        }

        [Fact]
        public void ParseColumnIdentifier_DoubleDot_SkipsSchema()
        {
            var id = Expr.ParseColumnIdentifier("mydb..Users.Name");

            Assert.Equal("mydb", id.DatabaseName.Name);
            Assert.Null(id.SchemaName);
            Assert.Equal("Users", id.ObjectName.Name);
            Assert.Equal("Name", id.ColumnName.Name);
            Assert.Equal("mydb..Users.Name", id.ToSource());
        }

        [Fact]
        public void ParseColumnIdentifier_TempTable()
        {
            var id = Expr.ParseColumnIdentifier("#tmp.Id");

            Assert.Equal("#tmp", id.ObjectName.Name);
            Assert.Equal("Id", id.ColumnName.Name);
            Assert.Equal("#tmp.Id", id.ToSource());
        }

        [Fact]
        public void ParseColumnIdentifier_ViaColumnIdentifierParse()
        {
            var id = Expr.ColumnIdentifier.Parse("t.Id");

            Assert.Equal("t", id.ObjectName.Name);
            Assert.Equal("Id", id.ColumnName.Name);
        }

        #endregion

        #region Chaining — All Three Together

        [Fact]
        public void AllThree_ChainedOnSameStatement()
        {
            var stmt = Stmt.ParseSelect("SELECT a FROM Users");

            stmt.AddSelectColumn("b", "Users");
            stmt.AddCondition("Active = 1", "Users");
            stmt.AddInto("#Results");

            Assert.Equal("SELECT b, a INTO #Results FROM Users WHERE Active = 1", stmt.ToSource());
        }

        #endregion
    }
}
