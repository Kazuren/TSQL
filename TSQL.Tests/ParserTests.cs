namespace TSQL.Tests
{
    public class ParserTests
    {
        #region Helper Methods
        private static Stmt.Select ParseSelect(string source)
        {
            Scanner scanner = new Scanner(source);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            Assert.IsType<Stmt.Select>(stmt);
            return (Stmt.Select)stmt;
        }
        #endregion

        #region Existing Tests
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

            SelectItem item = select.SelectExpression.Columns[0];
            Assert.IsType<SelectColumn>(item);
            SelectColumn column = (SelectColumn)item;
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

        [Fact]
        public void ParseIdentifiers_ParsesCorrectly()
        {
            // Arrange
            string source = "SELECT *, o.*, d..o.*, d.s.o.*, d.s.o.a, d..o.a, o.a, a FROM T";
            Scanner scanner = new Scanner(source);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.Equal(source, stmt.ToSource());
        }
        #endregion

        #region AST Structure Tests - Select Columns

        [Fact]
        public void Parse_SingleColumn_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T");

            // Assert
            Assert.Single(select.SelectExpression.Columns);
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("a", columnId.ColumnName.Name);
            Assert.Null(item.Alias);
        }

        [Fact]
        public void Parse_Variable_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT @P0 FROM T");

            // Assert
            Assert.Single(select.SelectExpression.Columns);
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var columnVariable = Assert.IsType<Expr.Variable>(item.Expression);
            Assert.Equal("@P0", columnVariable.Name);
            Assert.Null(item.Alias);
        }

        [Fact]
        public void Parse_MultipleColumns_HasCorrectCount()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a, b, c FROM T");

            // Assert
            Assert.Equal(3, select.SelectExpression.Columns.Count);
            Assert.All(select.SelectExpression.Columns, item => Assert.IsType<SelectColumn>(item));
        }

        [Fact]
        public void Parse_Wildcard_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT * FROM T");

            // Assert
            Assert.Single(select.SelectExpression.Columns);
            Assert.IsType<Expr.Wildcard>(select.SelectExpression.Columns[0]);
        }

        [Fact]
        public void Parse_QualifiedWildcard_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT t.* FROM T");

            // Assert
            Assert.Single(select.SelectExpression.Columns);
            var wildcard = Assert.IsType<Expr.QualifiedWildcard>(select.SelectExpression.Columns[0]);
            Assert.NotNull(wildcard.ObjectName);
            Assert.Equal("t", wildcard.ObjectName.Name);
        }

        [Fact]
        public void Parse_FullyQualifiedColumn_HasAllParts()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT d.s.o.c FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);

            Assert.NotNull(columnId.DatabaseName);
            Assert.Equal("d", columnId.DatabaseName.Name);

            Assert.NotNull(columnId.SchemaName);
            Assert.Equal("s", columnId.SchemaName.Name);

            Assert.NotNull(columnId.ObjectName);
            Assert.Equal("o", columnId.ObjectName.Name);

            Assert.NotNull(columnId.ColumnName);
            Assert.Equal("c", columnId.ColumnName.Name);
        }

        #endregion

        #region AST Structure Tests - Aliases

        [Fact]
        public void Parse_SuffixAliasWithAs_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a AS alias FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("alias", item.Alias.Name.Lexeme);
        }

        [Fact]
        public void Parse_SuffixAliasWithoutAs_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a alias FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("alias", item.Alias.Name.Lexeme);
        }

        [Fact]
        public void Parse_PrefixAlias_HasCorrectAliasType()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT alias = a FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.IsType<PrefixAlias>(item.Alias);
            Assert.Equal("alias", item.Alias.Name.Lexeme);
        }

        #endregion

        #region AST Structure Tests - Expressions

        [Fact]
        public void Parse_BinaryAddition_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a + b FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var binary = Assert.IsType<Expr.Binary>(item.Expression);

            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);
            Assert.Equal("+", binary.Operator.Lexeme);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_BinaryMultiplication_HasHigherPrecedenceThanAddition()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a + b * c FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var binary = Assert.IsType<Expr.Binary>(item.Expression);

            // a + (b * c) - multiplication should be on the right
            Assert.Equal("+", binary.Operator.Lexeme);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);

            var rightBinary = Assert.IsType<Expr.Binary>(binary.Right);
            Assert.Equal("*", rightBinary.Operator.Lexeme);
        }

        [Fact]
        public void Parse_UnaryMinus_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT -a FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var unary = Assert.IsType<Expr.Unary>(item.Expression);
            Assert.Equal("-", unary.Operator.Lexeme);
            Assert.IsType<Expr.ColumnIdentifier>(unary.Right);
        }

        [Fact]
        public void Parse_GroupedExpression_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT (a + b) * c FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var binary = Assert.IsType<Expr.Binary>(item.Expression);

            // (a + b) * c - grouped addition should be on the left
            Assert.Equal("*", binary.Operator.Lexeme);

            var grouping = Assert.IsType<Expr.Grouping>(binary.Left);
            var innerBinary = Assert.IsType<Expr.Binary>(grouping.Expression);
            Assert.Equal("+", innerBinary.Operator.Lexeme);
        }

        [Fact]
        public void Parse_LiteralNumber_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT 42 FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var literal = Assert.IsType<Expr.Literal>(item.Expression);
            Assert.Equal(42, literal.Value);
        }

        [Fact]
        public void Parse_LiteralString_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT 'hello' FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var literal = Assert.IsType<Expr.Literal>(item.Expression);
            Assert.Contains("hello", (string)literal.Value);
        }

        #endregion

        #region AST Structure Tests - Function Calls

        [Fact]
        public void Parse_FunctionCallNoArgs_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT GETDATE() FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var func = Assert.IsType<Expr.FunctionCall>(item.Expression);
            Assert.Equal("GETDATE", func.Callee.ObjectName.Name);
            Assert.Empty(func.Arguments);
        }

        [Fact]
        public void Parse_FunctionCallWithArgs_HasCorrectArgumentCount()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT COALESCE(a, b, c) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var func = Assert.IsType<Expr.FunctionCall>(item.Expression);
            Assert.Equal("COALESCE", func.Callee.ObjectName.Name);
            Assert.Equal(3, func.Arguments.Count);
        }

        #endregion

        #region AST Structure Tests - FROM Clause

        [Fact]
        public void Parse_SimpleFrom_HasTableSource()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM MyTable");

            // Assert
            Assert.NotNull(select.SelectExpression.From);
            Assert.NotNull(select.SelectExpression.From.TableSource);
        }

        [Fact]
        public void Parse_FromWithAlias_HasAliasSet()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM MyTable AS t");

            // Assert
            Assert.NotNull(select.SelectExpression.From);
            Assert.NotNull(select.SelectExpression.From.Alias);
            Assert.Equal("t", select.SelectExpression.From.Alias.Name.Lexeme);
        }

        #endregion

        #region Round-Trip Theory Tests

        [Theory]
        [InlineData("SELECT a FROM T")]
        [InlineData("SELECT a, b, c FROM T")]
        [InlineData("SELECT * FROM T")]
        [InlineData("SELECT t.* FROM T")]
        [InlineData("SELECT a + b FROM T")]
        [InlineData("SELECT a * b + c FROM T")]
        [InlineData("SELECT (a + b) * c FROM T")]
        [InlineData("SELECT -a FROM T")]
        [InlineData("SELECT a AS alias FROM T")]
        [InlineData("SELECT alias = a FROM T")]
        [InlineData("SELECT GETDATE() FROM T")]
        [InlineData("SELECT COALESCE(a, b) FROM T")]
        [InlineData("SELECT @P0 FROM T")]

        public void Parse_ValidSql_RoundTripsCorrectly(string source)
        {
            // Arrange
            Scanner scanner = new Scanner(source);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act
            Stmt stmt = parser.Parse();

            // Assert
            Assert.Equal(source, stmt.ToSource());
        }

        #endregion
    }
}
