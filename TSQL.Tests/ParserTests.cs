namespace TSQL.Tests
{
    public class ParserTests
    {
        [Fact]
        public void TestParser()
        {
            // Arrange
            Scanner scanner = new Scanner("  /*te st*/ SELECT a, b AS bAlias FROM T");
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            //Assert.IsType<Stmt.Select>(stmt);
            //Stmt.Select select = (Stmt.Select)stmt;

            //Assert.IsType<TableReference>(select.From.TableSource);
            //TableReference tableReference = (TableReference)select.From.TableSource;
            //Assert.Equal("T", tableReference.TableName);

            //Assert.Single(select.Columns);
            //Assert.IsType<Expr.Column>(select.Columns[0].Expression);
            //Expr.Column columnExpr = (Expr.Column)select.Columns[0].Expression;

            //Assert.Equal("*", columnExpr.ColumnName);
        }

        [Fact]
        public void ParsePrefixAlias_SimpleColumn_ParsesCorrectly()
        {
            // Arrange
            Scanner scanner = new Scanner("SELECT bAlias = b FROM T");
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.IsType<Stmt.Select>(stmt);
            Stmt.Select select = (Stmt.Select)stmt;
            Assert.Single(select.SelectExpression.Columns);

            SelectColumn column = select.SelectExpression.Columns[0];
            Assert.NotNull(column.Alias);
            Assert.Equal("bAlias", column.Alias.Name.Lexeme);
        }

        [Fact]
        public void ParsePrefixAlias_RoundTripsCorrectly()
        {
            // Arrange
            string source = "SELECT bAlias = b FROM T";
            Scanner scanner = new Scanner(source);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseMixedAliasStyles_RoundTripsCorrectly()
        {
            // Arrange
            string source = "SELECT a, bAlias = b, c AS cAlias FROM T";
            Scanner scanner = new Scanner(source);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParsePrefixAlias_WithExpression_RoundTripsCorrectly()
        {
            // Arrange
            string source = "SELECT total = a + b FROM T";
            Scanner scanner = new Scanner(source);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.Equal(source, stmt.ToSource());
        }
    }
}
