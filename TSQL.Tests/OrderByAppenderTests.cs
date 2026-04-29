using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class OrderByAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        #region AddOrderBy - No Existing ORDER BY

        [Fact]
        public void AddOrderBy_NoExistingOrderBy_AddsOrderByClause()
        {
            string sql = "SELECT * FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name");

            Assert.Equal("SELECT * FROM Products ORDER BY Name", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_WithAscDesc_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name ASC, CreatedDate DESC");

            Assert.Equal("SELECT * FROM Products ORDER BY Name ASC, CreatedDate DESC", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_MultipleColumns_AddsAll()
        {
            string sql = "SELECT * FROM Products";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Category, Name, Price");

            Assert.Equal("SELECT * FROM Products ORDER BY Category, Name, Price", stmt.ToSource());
        }

        #endregion

        #region AddOrderBy - Existing ORDER BY

        [Fact]
        public void AddOrderBy_ExistingOrderBy_AppendsItems()
        {
            string sql = "SELECT * FROM Products ORDER BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name");

            Assert.Equal("SELECT * FROM Products ORDER BY Category, Name", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_ExistingOrderBy_AppendsMultipleItems()
        {
            string sql = "SELECT * FROM T ORDER BY A";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("B DESC, C ASC");

            Assert.Equal("SELECT * FROM T ORDER BY A, B DESC, C ASC", stmt.ToSource());
        }

        #endregion

        #region AddOrderBy - With OFFSET/FETCH

        [Fact]
        public void AddOrderBy_ExistingWithOffset_PreservesOffset()
        {
            string sql = "SELECT * FROM Products ORDER BY Id OFFSET 10 ROWS";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name");

            Assert.Equal("SELECT * FROM Products ORDER BY Id, Name OFFSET 10 ROWS", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_ExistingWithOffsetFetch_PreservesBoth()
        {
            string sql = "SELECT * FROM Products ORDER BY Id OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name");

            Assert.Equal("SELECT * FROM Products ORDER BY Id, Name OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", stmt.ToSource());
        }

        #endregion

        #region ReplaceOrderBy

        [Fact]
        public void ReplaceOrderBy_NoExistingOrderBy_AddsOrderBy()
        {
            string sql = "SELECT * FROM Products";
            Stmt stmt = Parse(sql);

            stmt.ReplaceOrderBy("Name DESC");

            Assert.Equal("SELECT * FROM Products ORDER BY Name DESC", stmt.ToSource());
        }

        [Fact]
        public void ReplaceOrderBy_ExistingOrderBy_ReplacesEntirely()
        {
            string sql = "SELECT * FROM Products ORDER BY Category, Region, Name";
            Stmt stmt = Parse(sql);

            stmt.ReplaceOrderBy("Id DESC");

            Assert.Equal("SELECT * FROM Products ORDER BY Id DESC", stmt.ToSource());
        }

        [Fact]
        public void ReplaceOrderBy_EmptyString_ClearsOrderBy()
        {
            string sql = "SELECT * FROM Products ORDER BY Name";
            Stmt stmt = Parse(sql);

            stmt.ReplaceOrderBy("");

            Assert.Equal("SELECT * FROM Products", stmt.ToSource());
        }

        #endregion

        #region UNION / Set Operations

        [Fact]
        public void AddOrderBy_Union_AddsToSetOperation()
        {
            string sql = "SELECT * FROM Products UNION SELECT * FROM Archive";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name");

            Assert.Equal(
                "SELECT * FROM Products UNION SELECT * FROM Archive ORDER BY Name",
                stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_UnionAll_AddsToSetOperation()
        {
            string sql = "SELECT * FROM Products UNION ALL SELECT * FROM Archive";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name DESC");

            Assert.Equal(
                "SELECT * FROM Products UNION ALL SELECT * FROM Archive ORDER BY Name DESC",
                stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_UnionWithExistingOrderBy_AppendsItems()
        {
            string sql = "SELECT * FROM Products UNION SELECT * FROM Archive ORDER BY Category";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name");

            Assert.Equal(
                "SELECT * FROM Products UNION SELECT * FROM Archive ORDER BY Category, Name",
                stmt.ToSource());
        }

        [Fact]
        public void ReplaceOrderBy_Union_ReplacesOnSetOperation()
        {
            string sql = "SELECT * FROM Products UNION SELECT * FROM Archive ORDER BY Category, Region";
            Stmt stmt = Parse(sql);

            stmt.ReplaceOrderBy("Name DESC");

            Assert.Equal(
                "SELECT * FROM Products UNION SELECT * FROM Archive ORDER BY Name DESC",
                stmt.ToSource());
        }

        #endregion

        #region QueryScope Control

        [Fact]
        public void AddOrderBy_OutermostQueryOnly_DoesNotEnterSubqueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Products) sub";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name", QueryScope.OutermostQuery);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Products) sub ORDER BY Name",
                stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_FromSubqueries_OnlyModifiesSubqueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Products) sub";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name", QueryScope.FromSubqueries);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Products ORDER BY Name) sub",
                stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_All_ModifiesAllQueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Products) sub WHERE EXISTS (SELECT 1 FROM Archive)";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Id", QueryScope.All);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Products ORDER BY Id) sub WHERE EXISTS (SELECT 1 FROM Archive ORDER BY Id) ORDER BY Id",
                stmt.ToSource());
        }

        #endregion

        #region Target Table Overload

        [Fact]
        public void AddOrderBy_TargetTable_OnlyModifiesMatchingSubqueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Products) p JOIN (SELECT * FROM Archive) a ON p.Id = a.Id";
            Stmt stmt = Parse(sql);

            stmt.AddOrderBy("Name", "Products");

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Products ORDER BY Name) p JOIN (SELECT * FROM Archive) a ON p.Id = a.Id",
                stmt.ToSource());
        }

        [Fact]
        public void ReplaceOrderBy_TargetTable_OnlyModifiesMatchingSubqueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Products ORDER BY Category) p JOIN (SELECT * FROM Archive ORDER BY Type) a ON p.Id = a.Id";
            Stmt stmt = Parse(sql);

            stmt.ReplaceOrderBy("Name", "Products");

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Products ORDER BY Name) p JOIN (SELECT * FROM Archive ORDER BY Type) a ON p.Id = a.Id",
                stmt.ToSource());
        }

        #endregion
    }
}
