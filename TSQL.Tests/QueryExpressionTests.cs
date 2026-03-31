using System.Linq;
using static TSQL.Expr;

namespace TSQL.Tests
{
    public class QueryExpressionTests
    {
        #region Any<T> / OfType<T>

        [Fact]
        public void Any_Wildcard_ReturnsTrueWhenPresent()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT *, a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.True(selectExpr.Columns.Any<Wildcard>());
        }

        [Fact]
        public void Any_Wildcard_ReturnsFalseWhenAbsent()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.False(selectExpr.Columns.Any<Wildcard>());
        }

        [Fact]
        public void Any_QualifiedWildcard_ReturnsTrueForTableDotStar()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT T.* FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.True(selectExpr.Columns.Any<QualifiedWildcard>());
        }

        [Fact]
        public void OfType_SelectColumn_ReturnsOnlyColumns()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, c FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.Equal(3, selectExpr.Columns.OfType<SelectColumn>().Count());
        }

        [Fact]
        public void OfType_Wildcard_ReturnsEmptyWhenNoWildcards()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.Empty(selectExpr.Columns.OfType<Wildcard>());
        }

        #endregion

        #region Columns on QueryExpression (read-only interface)

        [Fact]
        public void QueryExpression_Columns_ReturnsReadOnlyInterface()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            QueryExpression queryExpr = stmt.Query;

            IReadOnlySyntaxElementList<SelectItem> columns = queryExpr.Columns;
            Assert.Equal(2, columns.Count);
        }

        [Fact]
        public void QueryExpression_Columns_SupportsAnyAndOfType()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT *, a FROM T");
            QueryExpression queryExpr = stmt.Query;

            Assert.True(queryExpr.Columns.Any<Wildcard>());
            Assert.Equal(1, queryExpr.Columns.OfType<SelectColumn>().Count());
        }

        [Fact]
        public void QueryExpression_Columns_SupportsIndexer()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            QueryExpression queryExpr = stmt.Query;

            Assert.IsType<SelectColumn>(queryExpr.Columns[0]);
            Assert.IsType<SelectColumn>(queryExpr.Columns[1]);
        }

        #endregion

        #region Columns on SetOperation (flattened)

        [Fact]
        public void SetOperation_Columns_ReturnsMergedSnapshot()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T1 UNION SELECT c FROM T2");
            QueryExpression queryExpr = stmt.Query;

            Assert.Equal(3, queryExpr.Columns.Count);
        }

        [Fact]
        public void SetOperation_Columns_DetectsWildcardInEitherSide()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT * FROM T2");
            QueryExpression queryExpr = stmt.Query;

            Assert.True(queryExpr.Columns.Any<Wildcard>());
        }

        [Fact]
        public void SetOperation_Columns_NoWildcardWhenAbsent()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2");
            QueryExpression queryExpr = stmt.Query;

            Assert.False(queryExpr.Columns.Any<Wildcard>());
        }

        #endregion

        #region PrependColumn

        [Fact]
        public void PrependColumn_SimpleSelect_PrependsToColumnList()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");

            stmt.Query.PrependColumn("x");

            Assert.Equal("SELECT x, a FROM T", stmt.ToSource());
        }

        [Fact]
        public void PrependColumn_SimpleSelect_PreservesDistinct()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT DISTINCT a FROM T");

            stmt.Query.PrependColumn("x");

            Assert.Equal("SELECT DISTINCT x, a FROM T", stmt.ToSource());
        }

        [Fact]
        public void PrependColumn_Union_PrependsToBothSides()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2");

            stmt.Query.PrependColumn("x");

            Assert.Equal("SELECT x, a FROM T1 UNION SELECT x, b FROM T2", stmt.ToSource());
        }

        [Fact]
        public void PrependColumn_UnionAll_PrependsToBothSides()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION ALL SELECT b FROM T2");

            stmt.Query.PrependColumn("x");

            Assert.Equal("SELECT x, a FROM T1 UNION ALL SELECT x, b FROM T2", stmt.ToSource());
        }

        [Fact]
        public void PrependColumn_QualifiedColumn_PrependsCorrectly()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");

            stmt.Query.PrependColumn("T.ID MYTABLEID");

            Assert.Equal("SELECT T.ID MYTABLEID, a FROM T", stmt.ToSource());
        }

        #endregion

        #region ContainsWildcard

        [Fact]
        public void ContainsWildcard_ReturnsTrueForStar()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT * FROM T");
            Assert.True(stmt.Query.ContainsWildcard());
        }

        [Fact]
        public void ContainsWildcard_ReturnsTrueForTableDotStar()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT T.* FROM T");
            Assert.True(stmt.Query.ContainsWildcard());
        }

        [Fact]
        public void ContainsWildcard_ReturnsFalseForNamedColumns()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            Assert.False(stmt.Query.ContainsWildcard());
        }

        [Fact]
        public void ContainsWildcard_SetOperation_DetectsInEitherSide()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT * FROM T2");
            Assert.True(stmt.Query.ContainsWildcard());
        }

        #endregion

        #region ContainsFunctionCall

        [Fact]
        public void ContainsFunctionCall_ReturnsTrueForCount()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT COUNT(*) FROM T");
            Assert.True(stmt.Query.ContainsFunctionCall("COUNT"));
        }

        [Fact]
        public void ContainsFunctionCall_IsCaseInsensitive()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT count(*) FROM T");
            Assert.True(stmt.Query.ContainsFunctionCall("COUNT"));
        }

        [Fact]
        public void ContainsFunctionCall_ReturnsFalseWhenAbsent()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            Assert.False(stmt.Query.ContainsFunctionCall("COUNT"));
        }

        #endregion

        #region ContainsColumnReference

        [Fact]
        public void ContainsColumnReference_ReturnsTrueWhenPresent()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT T.FILENAME FROM T");
            Assert.True(stmt.Query.ContainsColumnReference("T", "FILENAME"));
        }

        [Fact]
        public void ContainsColumnReference_IsCaseInsensitive()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT MSDSIMAGEFILE.FILENAME FROM T");
            Assert.True(stmt.Query.ContainsColumnReference("msdsimagefile", "filename"));
        }

        [Fact]
        public void ContainsColumnReference_ReturnsFalseWhenAbsent()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            Assert.False(stmt.Query.ContainsColumnReference("T", "FILENAME"));
        }

        #endregion

        #region SelectColumn.IsFunctionCall / IsColumnReference

        [Fact]
        public void SelectColumn_IsFunctionCall_ReturnsTrueForMatchingFunction()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT SUM(a) FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;
            SelectColumn col = (SelectColumn)selectExpr.Columns[0];

            Assert.True(col.IsFunctionCall("SUM"));
            Assert.False(col.IsFunctionCall("COUNT"));
        }

        [Fact]
        public void SelectColumn_IsColumnReference_ReturnsTrueForMatchingColumn()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT T.ID FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;
            SelectColumn col = (SelectColumn)selectExpr.Columns[0];

            Assert.True(col.IsColumnReference("T", "ID"));
            Assert.False(col.IsColumnReference("T", "NAME"));
        }

        #endregion
    }
}
