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

        #region Window Function Tests

        // === Basic Ranking Functions ===

        [Fact]
        public void Parse_RowNumber_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT ROW_NUMBER() OVER (ORDER BY id) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("ROW_NUMBER", windowFunc.Function.Callee.ObjectName.Name);
            Assert.NotNull(windowFunc.Over);
            Assert.NotNull(windowFunc.Over.OrderBy);
            Assert.Single(windowFunc.Over.OrderBy);
        }

        [Fact]
        public void Parse_RankWithPartition_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT RANK() OVER (PARTITION BY dept ORDER BY salary DESC) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("RANK", windowFunc.Function.Callee.ObjectName.Name);
            Assert.NotNull(windowFunc.Over.PartitionBy);
            Assert.Single(windowFunc.Over.PartitionBy);
            Assert.NotNull(windowFunc.Over.OrderBy);
            Assert.Single(windowFunc.Over.OrderBy);
            Assert.True(windowFunc.Over.OrderBy[0].Descending);
        }

        [Fact]
        public void Parse_DenseRank_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT DENSE_RANK() OVER (ORDER BY score) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("DENSE_RANK", windowFunc.Function.Callee.ObjectName.Name);
        }

        [Fact]
        public void Parse_Ntile_HasCorrectArgument()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT NTILE(4) OVER (ORDER BY val) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("NTILE", windowFunc.Function.Callee.ObjectName.Name);
            Assert.Single(windowFunc.Function.Arguments);
            var arg = Assert.IsType<Expr.Literal>(windowFunc.Function.Arguments[0]);
            Assert.Equal(4, arg.Value);
        }

        // === Aggregates with OVER ===

        [Fact]
        public void Parse_SumWithOver_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(amount) OVER (PARTITION BY customer_id) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("SUM", windowFunc.Function.Callee.ObjectName.Name);
            Assert.NotNull(windowFunc.Over.PartitionBy);
            Assert.Null(windowFunc.Over.OrderBy);
        }

        [Fact]
        public void Parse_AggregateWithEmptyOver_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT AVG(price) OVER () FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("AVG", windowFunc.Function.Callee.ObjectName.Name);
            Assert.Null(windowFunc.Over.PartitionBy);
            Assert.Null(windowFunc.Over.OrderBy);
            Assert.Null(windowFunc.Over.Frame);
        }

        // === Frame Clauses ===

        [Fact]
        public void Parse_RowsUnboundedPreceding_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS UNBOUNDED PRECEDING) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameType.Rows, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowFunc.Over.Frame.Start.BoundType);
            Assert.Null(windowFunc.Over.Frame.End); // Short syntax
        }

        [Fact]
        public void Parse_RowsNPreceding_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS 3 PRECEDING) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameType.Rows, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            var offset = Assert.IsType<Expr.Literal>(windowFunc.Over.Frame.Start.Offset);
            Assert.Equal(3, offset.Value);
        }

        [Fact]
        public void Parse_RowsBetween_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            var offset = Assert.IsType<Expr.Literal>(windowFunc.Over.Frame.Start.Offset);
            Assert.Equal(2, offset.Value);
            Assert.Equal(WindowFrameBoundType.CurrentRow, windowFunc.Over.Frame.End.BoundType);
        }

        [Fact]
        public void Parse_RangeBetween_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal(WindowFrameType.Range, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.CurrentRow, windowFunc.Over.Frame.Start.BoundType);
            Assert.Equal(WindowFrameBoundType.UnboundedFollowing, windowFunc.Over.Frame.End.BoundType);
        }

        [Fact]
        public void Parse_RowsBetweenNPrecedingAndNFollowing_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND 2 FOLLOWING) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            Assert.Equal(WindowFrameBoundType.Following, windowFunc.Over.Frame.End.BoundType);
        }

        // === Contextual Keywords as Identifiers ===

        [Fact]
        public void Parse_RowsAsColumnAlias_ParsesAsIdentifier()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT 1 AS ROWS FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("ROWS", item.Alias.Name.Lexeme);
        }

        [Fact]
        public void Parse_RankAsColumnName_ParsesAsIdentifier()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT RANK FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("RANK", columnId.ColumnName.Name);
        }

        [Fact]
        public void Parse_PartitionAsColumnName_ParsesAsIdentifier()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT PARTITION FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(select.SelectExpression.Columns[0]);
            var columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("PARTITION", columnId.ColumnName.Name);
        }

        [Fact]
        public void Parse_MultipleContextualKeywordsAsColumns_ParsesCorrectly()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT PARTITION, ROWS, PRECEDING FROM T");

            // Assert
            Assert.Equal(3, select.SelectExpression.Columns.Count);
        }

        // === Error Cases ===

        [Fact]
        public void Parse_RowNumberWithoutOver_ThrowsParseError()
        {
            // Arrange
            Scanner scanner = new Scanner("SELECT ROW_NUMBER() FROM T");
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => parser.Parse());
        }

        [Fact]
        public void Parse_FrameWithoutOrderBy_ThrowsParseError()
        {
            // Arrange
            Scanner scanner = new Scanner("SELECT SUM(x) OVER (ROWS UNBOUNDED PRECEDING) FROM T");
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => parser.Parse());
        }

        // === Round-Trip Tests ===

        [Theory]
        [InlineData("SELECT ROW_NUMBER() OVER (ORDER BY id) FROM T")]
        [InlineData("SELECT RANK() OVER (PARTITION BY dept ORDER BY salary DESC) FROM T")]
        [InlineData("SELECT DENSE_RANK() OVER (ORDER BY score) FROM T")]
        [InlineData("SELECT NTILE(4) OVER (ORDER BY val) FROM T")]
        [InlineData("SELECT SUM(amount) OVER (PARTITION BY customer_id) FROM T")]
        [InlineData("SELECT AVG(price) OVER () FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y ROWS UNBOUNDED PRECEDING) FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y ROWS 3 PRECEDING) FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND 2 FOLLOWING) FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) FROM T")]
        [InlineData("SELECT 1 AS ROWS FROM T")]
        [InlineData("SELECT ROWS FROM T")]
        [InlineData("SELECT RANK FROM T")]
        [InlineData("SELECT PARTITION, ROWS, PRECEDING FROM T")]
        [InlineData("SELECT ROW_NUMBER() OVER (PARTITION BY a, b ORDER BY c ASC, d DESC) FROM T")]
        public void Parse_WindowFunction_RoundTripsCorrectly(string source)
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

        #region WHERE Clause / Search Condition Tests

        [Fact]
        public void Parse_WhereComparison_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T WHERE a = 1");

            // Assert
            Assert.NotNull(select.SelectExpression.Where);
            var comparison = Assert.IsType<AST.Predicate.Comparison>(select.SelectExpression.Where);
            Assert.IsType<Expr.ColumnIdentifier>(comparison.Left);
            Assert.Equal("=", comparison.Operator.Lexeme);
            Assert.IsType<Expr.Literal>(comparison.Right);
        }

        [Fact]
        public void Parse_WhereIsNull_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T WHERE a IS NULL");

            // Assert
            var nullPred = Assert.IsType<AST.Predicate.Null>(select.SelectExpression.Where);
            Assert.False(nullPred.Negated);
        }

        [Fact]
        public void Parse_WhereIsNotNull_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T WHERE a IS NOT NULL");

            // Assert
            var nullPred = Assert.IsType<AST.Predicate.Null>(select.SelectExpression.Where);
            Assert.True(nullPred.Negated);
        }

        [Fact]
        public void Parse_WhereAndOr_HasCorrectPrecedence()
        {
            // a = 1 AND b = 2 OR c = 3 should be (a=1 AND b=2) OR c=3
            var select = ParseSelect("SELECT a FROM T WHERE a = 1 AND b = 2 OR c = 3");

            var orPred = Assert.IsType<AST.Predicate.Or>(select.SelectExpression.Where);
            Assert.IsType<AST.Predicate.And>(orPred.Left);
            Assert.IsType<AST.Predicate.Comparison>(orPred.Right);
        }

        [Fact]
        public void Parse_WhereNot_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE NOT a = 1");

            var notPred = Assert.IsType<AST.Predicate.Not>(select.SelectExpression.Where);
            Assert.IsType<AST.Predicate.Comparison>(notPred.Predicate);
        }

        [Fact]
        public void Parse_WhereLike_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE a LIKE '%test%'");

            var likePred = Assert.IsType<AST.Predicate.Like>(select.SelectExpression.Where);
            Assert.False(likePred.Negated);
            Assert.IsType<Expr.ColumnIdentifier>(likePred.Left);
            Assert.IsType<Expr.Literal>(likePred.Pattern);
        }

        [Fact]
        public void Parse_WhereBetween_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE a BETWEEN 1 AND 10");

            var between = Assert.IsType<AST.Predicate.Between>(select.SelectExpression.Where);
            Assert.False(between.Negated);
        }

        [Fact]
        public void Parse_WhereInList_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE a IN (1, 2, 3)");

            var inPred = Assert.IsType<AST.Predicate.In>(select.SelectExpression.Where);
            Assert.False(inPred.Negated);
            Assert.NotNull(inPred.ValueList);
            Assert.Equal(3, inPred.ValueList.Count);
        }

        [Fact]
        public void Parse_WhereExists_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE EXISTS (SELECT 1 FROM T2)");

            var exists = Assert.IsType<AST.Predicate.Exists>(select.SelectExpression.Where);
            Assert.NotNull(exists.Subquery);
        }

        [Theory]
        [InlineData("SELECT a FROM T WHERE a = 1")]
        [InlineData("SELECT a FROM T WHERE a > b")]
        [InlineData("SELECT a FROM T WHERE a >= 1")]
        [InlineData("SELECT a FROM T WHERE a <> 1")]
        [InlineData("SELECT a FROM T WHERE a != 1")]
        [InlineData("SELECT a FROM T WHERE a IS NULL")]
        [InlineData("SELECT a FROM T WHERE a IS NOT NULL")]
        [InlineData("SELECT a FROM T WHERE a BETWEEN 1 AND 10")]
        [InlineData("SELECT a FROM T WHERE a NOT BETWEEN 1 AND 10")]
        [InlineData("SELECT a FROM T WHERE a LIKE '%test%'")]
        [InlineData("SELECT a FROM T WHERE a NOT LIKE '%test%'")]
        [InlineData("SELECT a FROM T WHERE a LIKE '%test%' ESCAPE '\\'")]
        [InlineData("SELECT a FROM T WHERE a IN (1, 2, 3)")]
        [InlineData("SELECT a FROM T WHERE a NOT IN (1, 2, 3)")]
        [InlineData("SELECT a FROM T WHERE NOT a = 1")]
        [InlineData("SELECT a FROM T WHERE a = 1 AND b = 2")]
        [InlineData("SELECT a FROM T WHERE a = 1 OR b = 2")]
        [InlineData("SELECT a FROM T WHERE a = 1 AND b = 2 OR c = 3")]
        [InlineData("SELECT a FROM T WHERE EXISTS (SELECT 1 FROM T2)")]
        [InlineData("SELECT a FROM T WHERE CONTAINS (a, 'test')")]
        public void Parse_SearchCondition_RoundTripsCorrectly(string source)
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
