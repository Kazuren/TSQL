using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class GroupByAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        #region AddGroupBy - No Existing GROUP BY

        [Fact]
        public void AddGroupBy_NoExistingGroupBy_AddsGroupByClause()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category", stmt.ToSource());
        }

        [Fact]
        public void AddGroupBy_MultipleColumns_AddsAll()
        {
            string sql = "SELECT Category, Region, COUNT(*) FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category, Region");

            Assert.Equal("SELECT Category, Region, COUNT(*) FROM Products GROUP BY Category, Region", stmt.ToSource());
        }

        #endregion

        #region AddGroupBy - Existing GROUP BY

        [Fact]
        public void AddGroupBy_ExistingGroupBy_AppendsItems()
        {
            string sql = "SELECT Category, Region, COUNT(*) FROM Products GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Region");

            Assert.Equal("SELECT Category, Region, COUNT(*) FROM Products GROUP BY Category, Region", stmt.ToSource());
        }

        [Fact]
        public void AddGroupBy_ExistingGroupBy_AppendsMultipleItems()
        {
            string sql = "SELECT A, B, C FROM T GROUP BY A";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("B, C");

            Assert.Equal("SELECT A, B, C FROM T GROUP BY A, B, C", stmt.ToSource());
        }

        #endregion

        #region AddGroupBy - Advanced Syntax

        [Fact]
        public void AddGroupBy_WithRollup_ParsesCorrectly()
        {
            string sql = "SELECT Category, Region, SUM(Sales) FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("ROLLUP(Category, Region)");

            Assert.Equal("SELECT Category, Region, SUM(Sales) FROM Products GROUP BY ROLLUP(Category, Region)", stmt.ToSource());
        }

        [Fact]
        public void AddGroupBy_WithCube_ParsesCorrectly()
        {
            string sql = "SELECT Category, Region, SUM(Sales) FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("CUBE(Category, Region)");

            Assert.Equal("SELECT Category, Region, SUM(Sales) FROM Products GROUP BY CUBE(Category, Region)", stmt.ToSource());
        }

        [Fact]
        public void AddGroupBy_WithGroupingSets_ParsesCorrectly()
        {
            string sql = "SELECT Category, Region, SUM(Sales) FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("GROUPING SETS((Category), (Region), ())");

            Assert.Equal("SELECT Category, Region, SUM(Sales) FROM Products GROUP BY GROUPING SETS((Category), (Region), ())", stmt.ToSource());
        }

        #endregion

        #region ReplaceGroupBy

        [Fact]
        public void ReplaceGroupBy_NoExistingGroupBy_AddsGroupBy()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products";
            Stmt stmt = Parse(sql);

            stmt.ReplaceGroupBy("Category");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category", stmt.ToSource());
        }

        [Fact]
        public void ReplaceGroupBy_ExistingGroupBy_ReplacesEntirely()
        {
            string sql = "SELECT Category, Region, COUNT(*) FROM Products GROUP BY Category, Region";
            Stmt stmt = Parse(sql);

            stmt.ReplaceGroupBy("Category");

            Assert.Equal("SELECT Category, Region, COUNT(*) FROM Products GROUP BY Category", stmt.ToSource());
        }

        [Fact]
        public void ReplaceGroupBy_EmptyString_ClearsGroupBy()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.ReplaceGroupBy("");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products", stmt.ToSource());
        }

        #endregion

        #region UNION / Set Operations

        [Fact]
        public void AddGroupBy_Union_AddsToBothSides()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products UNION SELECT Category, COUNT(*) FROM Archive";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category",
                stmt.ToSource());
        }

        [Fact]
        public void ReplaceGroupBy_Union_ReplacesOnBothSides()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category, Region UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category, Type";
            Stmt stmt = Parse(sql);

            stmt.ReplaceGroupBy("Category");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category",
                stmt.ToSource());
        }

        #endregion

        #region QueryScope Control

        [Fact]
        public void AddGroupBy_OutermostQueryOnly_DoesNotEnterSubqueries()
        {
            string sql = "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products) sub";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category", QueryScope.OutermostQuery);

            Assert.Equal(
                "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products) sub GROUP BY Category",
                stmt.ToSource());
        }

        [Fact]
        public void AddGroupBy_FromSubqueries_OnlyModifiesSubqueries()
        {
            string sql = "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products) sub";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category", QueryScope.FromSubqueries);

            Assert.Equal(
                "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category) sub",
                stmt.ToSource());
        }

        [Fact]
        public void AddGroupBy_All_ModifiesAllQueries()
        {
            string sql = "SELECT * FROM (SELECT Category, COUNT(*) FROM Products) sub WHERE EXISTS (SELECT 1 FROM Archive)";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category", QueryScope.All);

            Assert.Equal(
                "SELECT * FROM (SELECT Category, COUNT(*) FROM Products GROUP BY Category) sub WHERE EXISTS (SELECT 1 FROM Archive GROUP BY Category) GROUP BY Category",
                stmt.ToSource());
        }

        #endregion

        #region Target Table Overload

        [Fact]
        public void AddGroupBy_TargetTable_OnlyModifiesMatchingQueries()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products UNION SELECT Type, COUNT(*) FROM Archive";
            Stmt stmt = Parse(sql);

            stmt.AddGroupBy("Category", "Products");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION SELECT Type, COUNT(*) FROM Archive",
                stmt.ToSource());
        }

        [Fact]
        public void ReplaceGroupBy_TargetTable_OnlyModifiesMatchingQueries()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category, Region UNION SELECT Type, COUNT(*) FROM Archive GROUP BY Type";
            Stmt stmt = Parse(sql);

            stmt.ReplaceGroupBy("Category", "Products");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION SELECT Type, COUNT(*) FROM Archive GROUP BY Type",
                stmt.ToSource());
        }

        #endregion
    }
}
