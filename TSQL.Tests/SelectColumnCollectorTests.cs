using TSQL.AST;
using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class SelectColumnCollectorTests
    {
        #region Simple Columns

        [Fact]
        public void CollectSelectColumns_SimpleColumn_ReturnsColumnIdentifier()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Expr.ColumnIdentifier id = Assert.IsType<Expr.ColumnIdentifier>(col.Expression);
            Assert.Equal("a", id.ColumnName.Name);
            Assert.Null(col.Alias);
        }

        [Fact]
        public void CollectSelectColumns_QualifiedColumn_HasObjectName()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT T.a FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Expr.ColumnIdentifier id = Assert.IsType<Expr.ColumnIdentifier>(col.Expression);
            Assert.Equal("a", id.ColumnName.Name);
            Assert.Equal("T", id.ObjectName.Name);
        }

        [Fact]
        public void CollectSelectColumns_MultipleColumns_PreservesOrder()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, c FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            Assert.Equal(3, columns.Count);
            List<string> names = columns
                .OfType<SelectColumn>()
                .Select(c => ((Expr.ColumnIdentifier)c.Expression).ColumnName.Name)
                .ToList();
            Assert.Equal(new[] { "a", "b", "c" }, names);
        }

        #endregion

        #region Aliases

        [Fact]
        public void CollectSelectColumns_AliasWithAs_HasAlias()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a AS MyAlias FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.NotNull(col.Alias);
            Assert.Equal("MyAlias", col.Alias.Name);
        }

        [Fact]
        public void CollectSelectColumns_AliasWithSpace_HasAlias()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a MyAlias FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.NotNull(col.Alias);
            Assert.Equal("MyAlias", col.Alias.Name);
        }

        [Fact]
        public void CollectSelectColumns_PrefixAlias_HasAlias()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT MyAlias = a FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.NotNull(col.Alias);
            Assert.IsType<PrefixAlias>(col.Alias);
            Assert.Equal("MyAlias", col.Alias.Name);
        }

        [Fact]
        public void CollectSelectColumns_StringLiteralAlias_StripsQuotes()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a AS 'My Label' FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.Equal("My Label", col.Alias.Name);
        }

        [Fact]
        public void CollectSelectColumns_NoAlias_AliasIsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.Null(col.Alias);
        }

        #endregion

        #region Complex Expressions

        [Fact]
        public void CollectSelectColumns_FunctionCall_ReturnsFunctionExpression()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT ISNULL(a, '') AS col FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.Equal("col", col.Alias.Name);
        }

        [Fact]
        public void CollectSelectColumns_CaseExpression_ReturnsCaseExpression()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "SELECT CASE WHEN x = 1 THEN 'Y' ELSE 'N' END AS Status FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.IsType<Expr.SearchedCase>(col.Expression);
            Assert.Equal("Status", col.Alias.Name);
        }

        [Fact]
        public void CollectSelectColumns_SubqueryExpression_ReturnsSubquery()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "SELECT (SELECT TOP 1 x FROM Y) AS Sub FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            SelectColumn col = Assert.Single(columns.OfType<SelectColumn>());
            Assert.IsType<Expr.Subquery>(col.Expression);
            Assert.Equal("Sub", col.Alias.Name);
        }

        #endregion

        #region Wildcards

        [Fact]
        public void CollectSelectColumns_Wildcard_ReturnsWildcard()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT * FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            Assert.Single(columns);
            Assert.IsType<Expr.Wildcard>(columns[0]);
        }

        [Fact]
        public void CollectSelectColumns_QualifiedWildcard_ReturnsQualifiedWildcard()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT T.* FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            Assert.Single(columns);
            Assert.IsType<Expr.QualifiedWildcard>(columns[0]);
        }

        [Fact]
        public void CollectSelectColumns_MixedColumnsAndWildcard_PreservesAll()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, T.*, b FROM T");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            Assert.Equal(3, columns.Count);
            Assert.IsType<SelectColumn>(columns[0]);
            Assert.IsType<Expr.QualifiedWildcard>(columns[1]);
            Assert.IsType<SelectColumn>(columns[2]);
        }

        #endregion

        #region Scope

        [Fact]
        public void CollectSelectColumns_DefaultScope_CollectsOutermostOnly()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "SELECT a FROM T WHERE x IN (SELECT id FROM S)");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            Assert.Single(columns);
            SelectColumn col = Assert.IsType<SelectColumn>(columns[0]);
            Assert.Equal("a", ((Expr.ColumnIdentifier)col.Expression).ColumnName.Name);
        }

        [Fact]
        public void CollectSelectColumns_AllScope_CollectsFromSubqueries()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "SELECT a FROM T WHERE x IN (SELECT id FROM S)");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns(
                scope: QueryScope.All);

            Assert.Equal(2, columns.Count);
        }

        [Fact]
        public void CollectSelectColumns_CtesScope_CollectsFromCtes()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "WITH cte AS (SELECT id FROM S) SELECT a FROM cte");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns(
                scope: QueryScope.Ctes);

            Assert.Single(columns);
            SelectColumn col = Assert.IsType<SelectColumn>(columns[0]);
            Assert.Equal("id", ((Expr.ColumnIdentifier)col.Expression).ColumnName.Name);
        }

        [Fact]
        public void CollectSelectColumns_UnionQuery_CollectsFromFirstOperand()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "SELECT a, b FROM T1 UNION SELECT c, d FROM T2");
            IReadOnlyList<SelectItem> columns = stmt.CollectSelectColumns();

            Assert.Equal(2, columns.Count);
            List<string> names = columns
                .OfType<SelectColumn>()
                .Select(c => ((Expr.ColumnIdentifier)c.Expression).ColumnName.Name)
                .ToList();
            Assert.Equal(new[] { "a", "b" }, names);
        }

        #endregion
    }
}
