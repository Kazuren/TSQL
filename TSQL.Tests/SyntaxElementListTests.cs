using static TSQL.Expr;

namespace TSQL.Tests
{
    public class SyntaxElementListTests
    {
        private static SelectColumn Column(string name)
        {
            return new SelectColumn(new ColumnIdentifier(new ColumnName(name)), null);
        }

        #region Insert(int, T)

        [Fact]
        public void Insert_IntoEmptyList_AddsItem()
        {
            SyntaxElementList<SelectItem> columns = new SyntaxElementList<SelectItem>();
            columns.Insert(0, Column("a"));

            Assert.Equal(1, columns.Count);
        }

        [Fact]
        public void Insert_AtBeginning_PrependsItem()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, c FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(0, Column("x"));

            Assert.Equal(4, selectExpr.Columns.Count);
            Assert.Equal("SELECT x, a, b, c FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_InMiddle_PlacesCorrectly()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, c FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(1, Column("x"));

            Assert.Equal(4, selectExpr.Columns.Count);
            Assert.Equal("SELECT a, x, b, c FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_AtEnd_AppendsItem()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(2, Column("x"));

            Assert.Equal(3, selectExpr.Columns.Count);
            Assert.Equal("SELECT a, b, x FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_IntoSingleItemList_ProducesCorrectOutput()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(0, Column("x"));

            Assert.Equal(2, selectExpr.Columns.Count);
            Assert.Equal("SELECT x, a FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_NegativeIndex_ClampsToZero()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(-5, Column("x"));

            Assert.Equal("SELECT x, a, b FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_IndexBeyondCount_ClampsToEnd()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(100, Column("x"));

            Assert.Equal("SELECT a, b, x FROM T", stmt.ToSource());
        }

        #endregion

        #region Insert(int, string)

        [Fact]
        public void Insert_String_ParsesAndInsertsColumn()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(0, "x");

            Assert.Equal(3, selectExpr.Columns.Count);
            Assert.Equal("SELECT x, a, b FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_String_QualifiedColumnWithAlias()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(0, "MSMSDS.MSDS_ID AS MYTABLEID");

            Assert.Equal(3, selectExpr.Columns.Count);
            Assert.Equal("SELECT MSMSDS.MSDS_ID AS MYTABLEID, a, b FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_String_FunctionExpression()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(1, "COUNT(*) AS cnt");

            Assert.Equal(2, selectExpr.Columns.Count);
            Assert.Equal("SELECT a, COUNT(*) AS cnt FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_String_Wildcard()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(0, "*");

            Assert.Equal(2, selectExpr.Columns.Count);
            Assert.Equal("SELECT *, a FROM T", stmt.ToSource());
        }

        [Fact]
        public void Insert_String_InvalidSql_ThrowsParseError()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.Throws<ParseError>(() => selectExpr.Columns.Insert(0, "FROM WHERE"));
        }

        [Fact]
        public void Insert_String_MultipleColumns_ThrowsParseError()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            Assert.Throws<ParseError>(() => selectExpr.Columns.Insert(0, "x, y"));
        }

        [Fact]
        public void Insert_String_PreservesDistinct()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT DISTINCT a, b FROM T");
            SelectExpression selectExpr = (SelectExpression)stmt.Query;

            selectExpr.Columns.Insert(0, "T.ID AS MYID");

            Assert.Equal("SELECT DISTINCT T.ID AS MYID, a, b FROM T", stmt.ToSource());
        }

        #endregion
    }
}
