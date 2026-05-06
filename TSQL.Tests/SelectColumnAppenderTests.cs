using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class SelectColumnAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        #region AddSelectColumn(columnSource) - Scope-based overload

        [Fact]
        public void AddSelectColumn_DefaultsToOutermostQuery()
        {
            string sql = "SELECT a, b FROM Users";
            Stmt stmt = Parse(sql);

            stmt.AddSelectColumn("'extra' AS extra_col");

            Assert.Equal("SELECT 'extra' AS extra_col, a, b FROM Users", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_WithExpression_ParsesCorrectly()
        {
            string sql = "SELECT name FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddSelectColumn("GETDATE() AS created_at");

            Assert.Equal("SELECT GETDATE() AS created_at, name FROM Products", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_Union_AddsToBothBranches()
        {
            string sql = "SELECT a FROM T1 UNION SELECT b FROM T2";
            Stmt stmt = Parse(sql);

            stmt.AddSelectColumn("'x' AS tag");

            Assert.Equal("SELECT 'x' AS tag, a FROM T1 UNION SELECT 'x' AS tag, b FROM T2", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_OutermostQueryOnly_DoesNotEnterSubqueries()
        {
            string sql = "SELECT * FROM (SELECT a FROM InnerTable) sub";
            Stmt stmt = Parse(sql);

            stmt.AddSelectColumn("'outer' AS level", QueryScope.OutermostQuery);

            Assert.Equal("SELECT 'outer' AS level, * FROM (SELECT a FROM InnerTable) sub", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_FromSubqueries_OnlyModifiesSubqueries()
        {
            string sql = "SELECT * FROM (SELECT a FROM InnerTable) sub";
            Stmt stmt = Parse(sql);

            stmt.AddSelectColumn("'inner' AS level", QueryScope.FromSubqueries);

            Assert.Equal("SELECT * FROM (SELECT 'inner' AS level, a FROM InnerTable) sub", stmt.ToSource());
        }

        [Fact]
        public void AddSelectColumn_All_ModifiesAllQueries()
        {
            string sql = "SELECT * FROM (SELECT a FROM InnerTable) sub";
            Stmt stmt = Parse(sql);

            stmt.AddSelectColumn("'both' AS level", QueryScope.All);

            Assert.Equal("SELECT 'both' AS level, * FROM (SELECT 'both' AS level, a FROM InnerTable) sub", stmt.ToSource());
        }

        #endregion
    }
}
