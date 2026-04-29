using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class HavingAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        #region No Existing HAVING

        [Fact]
        public void AddHaving_NoExistingHaving_AddsHavingClause()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 10");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 10", stmt.ToSource());
        }

        [Fact]
        public void AddHaving_NoExistingHaving_WithOrCondition_DoesNotWrap()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 10 OR SUM(Price) > 100");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 10 OR SUM(Price) > 100", stmt.ToSource());
        }

        #endregion

        #region Existing HAVING

        [Fact]
        public void AddHaving_ExistingHaving_CombinesWithAnd()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("SUM(Price) > 100");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5 AND SUM(Price) > 100", stmt.ToSource());
        }

        [Fact]
        public void AddHaving_ExistingHavingWithOr_WrapsExistingInParentheses()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5 OR COUNT(*) < 2";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("Active = 1");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING (COUNT(*) > 5 OR COUNT(*) < 2) AND Active = 1", stmt.ToSource());
        }

        [Fact]
        public void AddHaving_ExistingHavingSimple_NewConditionWithOr_WrapsNewInParentheses()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING Active = 1";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 10 OR SUM(Price) > 100");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING Active = 1 AND (COUNT(*) > 10 OR SUM(Price) > 100)", stmt.ToSource());
        }

        [Fact]
        public void AddHaving_BothOr_WrapsBothInParentheses()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING A = 1 OR B = 2";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("C = 3 OR D = 4");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING (A = 1 OR B = 2) AND (C = 3 OR D = 4)", stmt.ToSource());
        }

        [Fact]
        public void AddHaving_ExistingHavingWithAnd_DoesNotWrap()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING A = 1 AND B = 2";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("C = 3");

            Assert.Equal("SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING A = 1 AND B = 2 AND C = 3", stmt.ToSource());
        }

        #endregion

        #region UNION / Set Operations

        [Fact]
        public void AddHaving_Union_AddsToBothSides()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 5");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5 UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category HAVING COUNT(*) > 5",
                stmt.ToSource());
        }

        [Fact]
        public void AddHaving_UnionAll_AddsToBothSides()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION ALL SELECT Category, COUNT(*) FROM Archive GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 5");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 5 UNION ALL SELECT Category, COUNT(*) FROM Archive GROUP BY Category HAVING COUNT(*) > 5",
                stmt.ToSource());
        }

        [Fact]
        public void AddHaving_UnionWithExistingHaving_CombinesCorrectly()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING Active = 1 UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category HAVING Archived = 0";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 5");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING Active = 1 AND COUNT(*) > 5 UNION SELECT Category, COUNT(*) FROM Archive GROUP BY Category HAVING Archived = 0 AND COUNT(*) > 5",
                stmt.ToSource());
        }

        #endregion

        #region QueryScope Control

        [Fact]
        public void AddHaving_OutermostQueryOnly_DoesNotEnterSubqueries()
        {
            string sql = "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category) sub GROUP BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 5", QueryScope.OutermostQuery);

            Assert.Equal(
                "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category) sub GROUP BY Category HAVING COUNT(*) > 5",
                stmt.ToSource());
        }

        [Fact]
        public void AddHaving_FromSubqueries_OnlyModifiesSubqueries()
        {
            string sql = "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category) sub";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 5", QueryScope.FromSubqueries);

            Assert.Equal(
                "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category HAVING COUNT(*) > 5) sub",
                stmt.ToSource());
        }

        [Fact]
        public void AddHaving_All_ModifiesAllQueries()
        {
            string sql = "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category) sub WHERE EXISTS (SELECT 1 FROM Archive GROUP BY Type)";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("1 = 1", QueryScope.All);

            Assert.Equal(
                "SELECT * FROM (SELECT Category, COUNT(*) AS Cnt FROM Products GROUP BY Category HAVING 1 = 1) sub WHERE EXISTS (SELECT 1 FROM Archive GROUP BY Type HAVING 1 = 1) HAVING 1 = 1",
                stmt.ToSource());
        }

        #endregion

        #region Target Table Overload

        [Fact]
        public void AddHaving_TargetTable_OnlyModifiesMatchingQueries()
        {
            string sql = "SELECT Category, COUNT(*) FROM Products GROUP BY Category UNION SELECT Type, COUNT(*) FROM Archive GROUP BY Type";
            Stmt stmt = Parse(sql);

            stmt.AddHaving("COUNT(*) > 10", "Products");

            Assert.Equal(
                "SELECT Category, COUNT(*) FROM Products GROUP BY Category HAVING COUNT(*) > 10 UNION SELECT Type, COUNT(*) FROM Archive GROUP BY Type",
                stmt.ToSource());
        }

        #endregion
    }
}
