using static TSQL.Expr;

namespace TSQL.Tests
{
    public class AddOrderByOverloadTests
    {
        #region AddOrderBy(Expr, SortDirection)

        [Fact]
        public void AddOrderBy_Expr_AppendsOrderByExpression()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectColumn first = (SelectColumn)stmt.Query.Columns[0];

            stmt.Query.AddOrderBy(first.Expression);

            Assert.Equal("SELECT a, b FROM T ORDER BY a", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_ExprDescending_RendersDescKeyword()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectColumn first = (SelectColumn)stmt.Query.Columns[0];

            stmt.Query.AddOrderBy(first.Expression, SortDirection.Descending);

            Assert.Equal("SELECT a, b FROM T ORDER BY a DESC", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_ComplexExpr_AppendsFullExpression()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT ISNULL(a, '') + b FROM T");
            SelectColumn first = (SelectColumn)stmt.Query.Columns[0];

            stmt.Query.AddOrderBy(first.Expression);

            Assert.Equal("SELECT ISNULL(a, '') + b FROM T ORDER BY ISNULL(a, '') + b", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_SameTreeExpr_DoesNotCorruptSelectList()
        {
            // The expression comes from the statement's own SELECT list. If AddOrderBy
            // aliased the node instead of cloning it, the trivia rewrite it performs on
            // the ORDER BY item would eat the three spaces after SELECT.
            Stmt.Select stmt = Stmt.ParseSelect("SELECT   a, b FROM T");
            SelectColumn first = (SelectColumn)stmt.Query.Columns[0];

            stmt.Query.AddOrderBy(first.Expression);

            Assert.Equal("SELECT   a, b FROM T ORDER BY a", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_Expr_AppendsToExistingOrderBy()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T ORDER BY b DESC");
            SelectColumn first = (SelectColumn)stmt.Query.Columns[0];

            stmt.Query.AddOrderBy(first.Expression);

            Assert.Equal("SELECT a, b FROM T ORDER BY b DESC, a", stmt.ToSource());
        }

        #endregion

        #region AddOrderBy(string)

        [Fact]
        public void AddOrderBy_String_ParsesMultipleItems()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");

            stmt.Query.AddOrderBy("a DESC, b");

            Assert.Equal("SELECT a, b FROM T ORDER BY a DESC, b", stmt.ToSource());
        }

        #endregion

        #region AddOrderBy(OrderByItem...)

        [Fact]
        public void AddOrderBy_ParamsItems_AppendsAllWithSeparators()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");

            stmt.Query.AddOrderBy(
                new OrderByItem(ColumnIdentifier.Parse("a"), SortDirection.Descending),
                new OrderByItem(ColumnIdentifier.Parse("b")));

            Assert.Equal("SELECT a, b FROM T ORDER BY a DESC, b", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_EnumerableItems_AppendsAll()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            List<OrderByItem> items = new List<OrderByItem>
            {
                new OrderByItem(ColumnIdentifier.Parse("b")),
                new OrderByItem(ColumnIdentifier.Parse("a"))
            };

            stmt.Query.AddOrderBy(items);

            Assert.Equal("SELECT a, b FROM T ORDER BY b, a", stmt.ToSource());
        }

        [Fact]
        public void AddOrderBy_SameItemAddedToTwoStatements_KeepsThemIndependent()
        {
            OrderByItem item = new OrderByItem(ColumnIdentifier.Parse("a"));
            Stmt.Select stmt1 = Stmt.ParseSelect("SELECT a FROM T1");
            Stmt.Select stmt2 = Stmt.ParseSelect("SELECT a FROM T2");

            stmt1.Query.AddOrderBy(item);
            stmt2.Query.AddOrderBy(item);
            stmt1.NormalizeWhitespace();

            Assert.Equal("SELECT a FROM T1 ORDER BY a", stmt1.ToSource());
            Assert.Equal("SELECT a FROM T2 ORDER BY a", stmt2.ToSource());
        }

        #endregion

        #region OrderByItem constructor

        [Fact]
        public void OrderByItem_Ctor_DefaultsToAscendingWithoutKeyword()
        {
            OrderByItem item = new OrderByItem(ColumnIdentifier.Parse("x"));

            Assert.Equal(SortDirection.Ascending, item.Direction);
            Assert.Equal("x", item.ToSource());
        }

        [Fact]
        public void OrderByItem_Ctor_RendersDescendingKeyword()
        {
            OrderByItem item = new OrderByItem(ColumnIdentifier.Parse("x"), SortDirection.Descending);

            Assert.Equal(SortDirection.Descending, item.Direction);
            Assert.Equal("x DESC", item.ToSource());
        }

        #endregion
    }
}
