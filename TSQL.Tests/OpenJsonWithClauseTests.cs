namespace TSQL.Tests
{
    public class OpenJsonWithClauseTests
    {
        private static string RoundTrip(string sql) => Stmt.Parse(sql).ToSource();

        [Theory]
        [InlineData("SELECT v FROM OPENJSON(@p) WITH (v INT '$')")]
        [InlineData("SELECT v FROM OPENJSON(@p) WITH (v NVARCHAR(4000) '$')")]
        [InlineData("SELECT v FROM OPENJSON(@p) WITH (v DECIMAL(38,10) '$')")]
        [InlineData("SELECT a, b FROM OPENJSON(@p) WITH (a INT '$.id', b NVARCHAR(50) '$.name')")]
        [InlineData("SELECT a FROM OPENJSON(@p) WITH (a NVARCHAR(MAX) '$.child' AS JSON)")]
        [InlineData("SELECT v FROM OPENJSON(@p, '$.rows') WITH (v INT '$')")]
        [InlineData("SELECT v FROM OPENJSON(@p) WITH (v INT)")]
        public void OpenJsonWithClause_RoundTrips(string sql)
        {
            Assert.Equal(sql, RoundTrip(sql));
        }

        [Fact]
        public void OpenJsonWithClause_ExposesColumnMetadata()
        {
            Stmt.Select stmt = Stmt.ParseSelect(
                "SELECT a FROM OPENJSON(@p) WITH (a INT '$.id' AS JSON)");
            SelectExpression select = (SelectExpression)stmt.Query;
            RowsetFunctionReference rowset =
                Assert.IsType<RowsetFunctionReference>(select.From.TableSources[0]);

            Assert.NotNull(rowset.WithClause);
            RowsetColumnDef col = Assert.Single(rowset.WithClause.Columns);
            Assert.Equal("a", col.Name);
            Assert.Equal("$.id", col.ColumnPath);
            Assert.True(col.AsJson);
        }

        [Fact]
        public void OpenJsonWithoutWithClause_StillParses()
        {
            Assert.Equal(
                "SELECT [value] FROM OPENJSON(@p)",
                RoundTrip("SELECT [value] FROM OPENJSON(@p)"));
        }

        [Fact]
        public void TableHint_StillParsesAsHint_NotSchemaDeclaration()
        {
            Assert.Equal(
                "SELECT * FROM T WITH (NOLOCK)",
                RoundTrip("SELECT * FROM T WITH (NOLOCK)"));
        }
    }
}
