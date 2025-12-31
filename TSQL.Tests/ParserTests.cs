namespace TSQL.Tests
{
    public class ParserTests
    {
        [Fact]
        public void TestParser()
        {
            // Arrange
            Scanner scanner = new Scanner("SELECT * FROM T");
            List<Token> tokens = scanner.ScanTokens();

            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.IsType<Stmt.Select>(stmt);
            Stmt.Select select = (Stmt.Select)stmt;

            Assert.Equal("T", select.From.TableName);

            Assert.Single(select.Columns);
            Assert.IsType<Expr.Column>(select.Columns[0].Expression);
            Expr.Column columnExpr = (Expr.Column)select.Columns[0].Expression;

            Assert.Equal("*", columnExpr.ColumnName);
        }
    }
}
