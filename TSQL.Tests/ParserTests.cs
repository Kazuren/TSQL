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

        private static SelectExpression SelectExpressionOf(Stmt.Select stmt)
        {
            return Assert.IsType<SelectExpression>(stmt.Query);
        }

        private static string RoundTrip(string source)
        {
            return ParseSelect(source).ToSource();
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
            Assert.Single(SelectExpressionOf(select).Columns);

            SelectItem item = SelectExpressionOf(select).Columns[0];
            Assert.IsType<SelectColumn>(item);
            SelectColumn column = (SelectColumn)item;
            Assert.NotNull(column.Alias);
            Assert.Equal("bAlias", column.Alias.Name);
        }

        [Fact]
        public void ParsePrefixAlias_RoundTripsCorrectly()
        {
            string source = "SELECT bAlias = b FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void ParseMixedAliasStyles_RoundTripsCorrectly()
        {
            string source = "SELECT a, bAlias = b, c AS cAlias FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void ParsePrefixAlias_WithExpression_RoundTripsCorrectly()
        {
            string source = "SELECT total = a + b FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void ParseIdentifiers_ParsesCorrectly()
        {
            string source = "SELECT *, o.*, d..o.*, d.s.o.*, d.s.o.a, d..o.a, o.a, a FROM T";
            Assert.Equal(source, RoundTrip(source));
        }
        #endregion

        #region AST Structure Tests - Select Columns

        [Fact]
        public void Parse_SingleColumn_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            Assert.Single(SelectExpressionOf(select).Columns);
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            Assert.Equal(3, SelectExpressionOf(select).Columns.Count);
            Assert.All(SelectExpressionOf(select).Columns, item => Assert.IsType<SelectColumn>(item));
        }

        [Fact]
        public void Parse_Wildcard_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT * FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            Assert.IsType<Expr.Wildcard>(SelectExpressionOf(select).Columns[0]);
        }

        [Fact]
        public void Parse_QualifiedWildcard_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT t.* FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            var wildcard = Assert.IsType<Expr.QualifiedWildcard>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(wildcard.ObjectName);
            Assert.Equal("t", wildcard.ObjectName.Name);
        }

        [Fact]
        public void Parse_FullyQualifiedColumn_HasAllParts()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT d.s.o.c FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("alias", item.Alias.Name);
        }

        [Fact]
        public void Parse_SuffixAliasWithoutAs_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a alias FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("alias", item.Alias.Name);
        }

        [Fact]
        public void Parse_PrefixAlias_HasCorrectAliasType()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT alias = a FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.IsType<PrefixAlias>(item.Alias);
            Assert.Equal("alias", item.Alias.Name);
        }

        #endregion

        #region AST Structure Tests - Expressions

        [Fact]
        public void Parse_BinaryAddition_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a + b FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var binary = Assert.IsType<Expr.Binary>(item.Expression);

            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);
            Assert.Equal(Expr.ArithmeticOperator.Add, binary.Operator);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_BinaryMultiplication_HasHigherPrecedenceThanAddition()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a + b * c FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var binary = Assert.IsType<Expr.Binary>(item.Expression);

            // a + (b * c) - multiplication should be on the right
            Assert.Equal(Expr.ArithmeticOperator.Add, binary.Operator);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);

            var rightBinary = Assert.IsType<Expr.Binary>(binary.Right);
            Assert.Equal(Expr.ArithmeticOperator.Multiply, rightBinary.Operator);
        }

        [Fact]
        public void Parse_UnaryMinus_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT -a FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var unary = Assert.IsType<Expr.Unary>(item.Expression);
            Assert.Equal(Expr.UnaryOperator.Negate, unary.Operator);
            Assert.IsType<Expr.ColumnIdentifier>(unary.Right);
        }

        [Fact]
        public void Parse_GroupedExpression_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT (a + b) * c FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var binary = Assert.IsType<Expr.Binary>(item.Expression);

            // (a + b) * c - grouped addition should be on the left
            Assert.Equal(Expr.ArithmeticOperator.Multiply, binary.Operator);

            var grouping = Assert.IsType<Expr.Grouping>(binary.Left);
            var innerBinary = Assert.IsType<Expr.Binary>(grouping.Expression);
            Assert.Equal(Expr.ArithmeticOperator.Add, innerBinary.Operator);
        }

        [Fact]
        public void Parse_LiteralNumber_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT 42 FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var literal = Assert.IsType<Expr.IntLiteral>(item.Expression);
            Assert.Equal(42, literal.Value);
        }

        [Fact]
        public void Parse_LiteralString_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT 'hello' FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var literal = Assert.IsType<Expr.StringLiteral>(item.Expression);
            Assert.Contains("hello", literal.Value);
        }

        #endregion

        #region AST Structure Tests - Function Calls

        [Fact]
        public void Parse_FunctionCallNoArgs_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT GETDATE() FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            Assert.NotNull(SelectExpressionOf(select).From);
            Assert.Equal(1, SelectExpressionOf(select).From.TableSources.Count);
            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromWithAlias_HasAliasSet()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM MyTable AS t");

            // Assert
            Assert.NotNull(SelectExpressionOf(select).From);
            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Alias);
            Assert.Equal("t", tableRef.Alias.Name);
        }

        [Fact]
        public void Parse_FromQualifiedName_TwoPart()
        {
            var select = ParseSelect("SELECT a FROM dbo.MyTable");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromQualifiedName_ThreePart()
        {
            var select = ParseSelect("SELECT a FROM MyDb.dbo.MyTable");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("MyDb", tableRef.TableName.DatabaseName.Name);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromQualifiedName_FourPart()
        {
            var select = ParseSelect("SELECT a FROM Server1.MyDb.dbo.MyTable");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("Server1", tableRef.TableName.ServerName.Name);
            Assert.Equal("MyDb", tableRef.TableName.DatabaseName.Name);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromCommaSeparated_HasMultipleSources()
        {
            var select = ParseSelect("SELECT a FROM T1, T2, T3");

            Assert.Equal(3, SelectExpressionOf(select).From.TableSources.Count);
            var t1 = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            var t2 = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[1]);
            var t3 = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[2]);
            Assert.Equal("T1", t1.TableName.ObjectName.Name);
            Assert.Equal("T2", t2.TableName.ObjectName.Name);
            Assert.Equal("T3", t3.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromSubquery_HasSubqueryReference()
        {
            var select = ParseSelect("SELECT a FROM (SELECT b FROM T) AS sub");

            var subRef = Assert.IsType<SubqueryReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(subRef.Subquery);
            Assert.NotNull(subRef.Alias);
            Assert.Equal("sub", subRef.Alias.Name);
        }

        [Fact]
        public void Parse_FromTableVariable_HasVariableReference()
        {
            var select = ParseSelect("SELECT a FROM @TempTable");

            var varRef = Assert.IsType<TableVariableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("@TempTable", varRef.VariableName);
        }

        [Fact]
        public void Parse_FromTableVariableWithAlias_HasAliasSet()
        {
            var select = ParseSelect("SELECT a FROM @TempTable AS t");

            var varRef = Assert.IsType<TableVariableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("@TempTable", varRef.VariableName);
            Assert.NotNull(varRef.Alias);
            Assert.Equal("t", varRef.Alias.Name);
        }

        [Fact]
        public void Parse_FromAliasWithoutAs_HasAliasSet()
        {
            var select = ParseSelect("SELECT a FROM MyTable t");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Alias);
            Assert.Equal("t", tableRef.Alias.Name);
        }

        [Fact]
        public void Parse_InnerJoin_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.Inner, join.JoinType);
            var left = Assert.IsType<TableReference>(join.Left);
            var right = Assert.IsType<TableReference>(join.Right);
            Assert.Equal("T1", left.TableName.ObjectName.Name);
            Assert.Equal("T2", right.TableName.ObjectName.Name);
            Assert.NotNull(join.OnCondition);
        }

        [Fact]
        public void Parse_LeftJoin_HasCorrectJoinType()
        {
            var select = ParseSelect("SELECT a FROM T1 LEFT JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.LeftOuter, join.JoinType);
        }

        [Fact]
        public void Parse_LeftOuterJoin_HasCorrectJoinType()
        {
            var select = ParseSelect("SELECT a FROM T1 LEFT OUTER JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.LeftOuter, join.JoinType);
        }

        [Fact]
        public void Parse_RightJoin_HasCorrectJoinType()
        {
            var select = ParseSelect("SELECT a FROM T1 RIGHT JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.RightOuter, join.JoinType);
        }

        [Fact]
        public void Parse_FullOuterJoin_HasCorrectJoinType()
        {
            var select = ParseSelect("SELECT a FROM T1 FULL OUTER JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.FullOuter, join.JoinType);
        }

        [Fact]
        public void Parse_BareJoin_DefaultsToInner()
        {
            var select = ParseSelect("SELECT a FROM T1 JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.Inner, join.JoinType);
        }

        [Fact]
        public void Parse_CrossJoin_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T1 CROSS JOIN T2");

            var join = Assert.IsType<CrossJoin>(SelectExpressionOf(select).From.TableSources[0]);
            var left = Assert.IsType<TableReference>(join.Left);
            var right = Assert.IsType<TableReference>(join.Right);
            Assert.Equal("T1", left.TableName.ObjectName.Name);
            Assert.Equal("T2", right.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_CrossApply_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T1 CROSS APPLY (SELECT b FROM T2) AS sub");

            var join = Assert.IsType<ApplyJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(ApplyType.Cross, join.ApplyType);
            var left = Assert.IsType<TableReference>(join.Left);
            Assert.Equal("T1", left.TableName.ObjectName.Name);
            var right = Assert.IsType<SubqueryReference>(join.Right);
            Assert.NotNull(right.Alias);
        }

        [Fact]
        public void Parse_OuterApply_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T1 OUTER APPLY (SELECT b FROM T2) AS sub");

            var join = Assert.IsType<ApplyJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(ApplyType.Outer, join.ApplyType);
        }

        [Fact]
        public void Parse_MultiJoinChain_IsLeftAssociative()
        {
            var select = ParseSelect("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id INNER JOIN T3 ON T2.id = T3.id");

            // Should be: (T1 JOIN T2) JOIN T3
            var outerJoin = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            var innerJoin = Assert.IsType<QualifiedJoin>(outerJoin.Left);
            var t3 = Assert.IsType<TableReference>(outerJoin.Right);
            var t1 = Assert.IsType<TableReference>(innerJoin.Left);
            var t2 = Assert.IsType<TableReference>(innerJoin.Right);
            Assert.Equal("T1", t1.TableName.ObjectName.Name);
            Assert.Equal("T2", t2.TableName.ObjectName.Name);
            Assert.Equal("T3", t3.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_LoopJoinWithoutType_TreatsLoopAsAlias()
        {
            // Without an explicit join type, LOOP is treated as an alias
            var select = ParseSelect("SELECT a FROM T1 LOOP JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            var left = Assert.IsType<TableReference>(join.Left);
            Assert.Equal("LOOP", left.Alias.Name);
            Assert.Null(join.JoinHint);
            Assert.Equal(JoinType.Inner, join.JoinType);
        }

        [Fact]
        public void Parse_JoinHint_InnerHashJoin()
        {
            var select = ParseSelect("SELECT a FROM T1 INNER HASH JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinHint.Hash, join.JoinHint);
            Assert.Equal(JoinType.Inner, join.JoinType);
        }

        [Fact]
        public void Parse_JoinHint_LeftMergeJoin()
        {
            var select = ParseSelect("SELECT a FROM T1 LEFT MERGE JOIN T2 ON T1.id = T2.id");

            var join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinHint.Merge, join.JoinHint);
            Assert.Equal(JoinType.LeftOuter, join.JoinType);
        }

        [Fact]
        public void Parse_ForSystemTimeAsOf_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T FOR SYSTEM_TIME AS OF '2020-01-01'");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.ForSystemTime);
            Assert.Equal(SystemTimeType.AsOf, tableRef.ForSystemTime.TimeType);
        }

        [Fact]
        public void Parse_ForSystemTimeAll_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T FOR SYSTEM_TIME ALL");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.ForSystemTime);
            Assert.Equal(SystemTimeType.All, tableRef.ForSystemTime.TimeType);
        }

        [Fact]
        public void Parse_ForSystemTimeBetweenAnd_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T FOR SYSTEM_TIME BETWEEN '2020-01-01' AND '2021-01-01'");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.ForSystemTime);
            Assert.Equal(SystemTimeType.BetweenAnd, tableRef.ForSystemTime.TimeType);
        }

        [Fact]
        public void Parse_TablesamplePercent_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T AS t TABLESAMPLE (10 PERCENT)");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Tablesample);
            Assert.Equal(TableSampleUnit.Percent, tableRef.Tablesample.Unit);
        }

        [Fact]
        public void Parse_TablesampleWithRepeatable_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T TABLESAMPLE SYSTEM (100 ROWS) REPEATABLE (42)");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Tablesample);
            Assert.Equal(TableSampleUnit.Rows, tableRef.Tablesample.Unit);
            Assert.NotNull(tableRef.Tablesample.RepeatSeed);
        }

        [Fact]
        public void Parse_TableHintNolock_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WITH (NOLOCK)");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.TableHints);
            Assert.Equal(1, tableRef.TableHints.Hints.Count);
            Assert.Equal(TableHintType.NoLock, tableRef.TableHints.Hints[0].HintType);
        }

        [Fact]
        public void Parse_TableHintMultiple_HasCorrectCount()
        {
            var select = ParseSelect("SELECT a FROM T WITH (NOLOCK, NOWAIT)");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.TableHints);
            Assert.Equal(2, tableRef.TableHints.Hints.Count);
            Assert.Equal(TableHintType.NoLock, tableRef.TableHints.Hints[0].HintType);
            Assert.Equal(TableHintType.NoWait, tableRef.TableHints.Hints[1].HintType);
        }

        [Fact]
        public void Parse_TableHintIndex_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WITH (INDEX(1))");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(TableHintType.Index, tableRef.TableHints.Hints[0].HintType);
            Assert.Equal(1, tableRef.TableHints.Hints[0].IndexValues.Count);
        }

        [Fact]
        public void Parse_TableHintHoldLock_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WITH (HOLDLOCK)");

            var tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(TableHintType.HoldLock, tableRef.TableHints.Hints[0].HintType);
        }

        [Fact]
        public void Parse_ParenthesizedJoin_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM (T1 INNER JOIN T2 ON T1.id = T2.id)");

            var paren = Assert.IsType<ParenthesizedTableSource>(SelectExpressionOf(select).From.TableSources[0]);
            var join = Assert.IsType<QualifiedJoin>(paren.Inner);
            Assert.Equal(JoinType.Inner, join.JoinType);
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
        // FROM clause - qualified names
        [InlineData("SELECT a FROM dbo.T")]
        [InlineData("SELECT a FROM MyDb.dbo.T")]
        [InlineData("SELECT a FROM Server1.MyDb.dbo.T")]
        [InlineData("SELECT a FROM T AS t1")]
        [InlineData("SELECT a FROM dbo.T AS t1")]
        // FROM clause - comma-separated sources
        [InlineData("SELECT a FROM T1, T2")]
        [InlineData("SELECT a FROM T1, T2, T3")]
        [InlineData("SELECT a FROM dbo.T1 AS t1, dbo.T2 AS t2")]
        // FROM clause - subquery
        [InlineData("SELECT a FROM (SELECT b FROM T) AS sub")]
        // FROM clause - table variable
        [InlineData("SELECT a FROM @TempTable")]
        [InlineData("SELECT a FROM @TempTable AS t")]
        // FROM clause - JOINs
        [InlineData("SELECT a FROM T1 JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 LEFT JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 LEFT OUTER JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 RIGHT JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 RIGHT OUTER JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 FULL JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 FULL OUTER JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 CROSS JOIN T2")]
        [InlineData("SELECT a FROM T1 CROSS APPLY (SELECT b FROM T2) AS sub")]
        [InlineData("SELECT a FROM T1 OUTER APPLY (SELECT b FROM T2) AS sub")]
        // FROM clause - join hints
        [InlineData("SELECT a FROM T1 LOOP JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 INNER HASH JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 LEFT MERGE JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 LEFT OUTER REMOTE JOIN T2 ON T1.id = T2.id")]
        // FROM clause - multi-join chains
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id INNER JOIN T3 ON T2.id = T3.id")]
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id LEFT JOIN T3 ON T2.id = T3.id CROSS JOIN T4")]
        // FROM clause - parenthesized joins
        [InlineData("SELECT a FROM (T1 INNER JOIN T2 ON T1.id = T2.id)")]
        [InlineData("SELECT a FROM (T1 INNER JOIN T2 ON T1.id = T2.id) CROSS JOIN T3")]
        // FROM clause - FOR SYSTEM_TIME
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME AS OF '2020-01-01'")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME FROM '2020-01-01' TO '2021-01-01'")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME BETWEEN '2020-01-01' AND '2021-01-01'")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME CONTAINED IN ('2020-01-01', '2021-01-01')")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME ALL")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME AS OF @AsOfDate")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME AS OF '2020-01-01' AS t")]
        // FROM clause - TABLESAMPLE
        [InlineData("SELECT a FROM T TABLESAMPLE (10 PERCENT)")]
        [InlineData("SELECT a FROM T AS t TABLESAMPLE (100 ROWS)")]
        [InlineData("SELECT a FROM T TABLESAMPLE SYSTEM (10 PERCENT)")]
        [InlineData("SELECT a FROM T TABLESAMPLE (10 PERCENT) REPEATABLE (42)")]
        // FROM clause - table hints
        [InlineData("SELECT a FROM T WITH (NOLOCK)")]
        [InlineData("SELECT a FROM T WITH (NOLOCK, NOWAIT)")]
        [InlineData("SELECT a FROM T WITH (HOLDLOCK)")]
        [InlineData("SELECT a FROM T WITH (INDEX(1))")]
        [InlineData("SELECT a FROM T WITH (INDEX(1, 2))")]
        [InlineData("SELECT a FROM T WITH (INDEX = 1)")]
        [InlineData("SELECT a FROM T WITH (READCOMMITTED, ROWLOCK)")]
        // FROM clause - combined suffixes
        [InlineData("SELECT a FROM T AS t TABLESAMPLE (10 PERCENT) WITH (NOLOCK)")]
        [InlineData("SELECT a FROM T1 WITH (NOLOCK) INNER JOIN T2 WITH (NOLOCK) ON T1.id = T2.id")]

        public void Parse_ValidSql_RoundTripsCorrectly(string source)
        {
            Assert.Equal(source, RoundTrip(source));
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("DENSE_RANK", windowFunc.Function.Callee.ObjectName.Name);
        }

        [Fact]
        public void Parse_Ntile_HasCorrectArgument()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT NTILE(4) OVER (ORDER BY val) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("NTILE", windowFunc.Function.Callee.ObjectName.Name);
            Assert.Single(windowFunc.Function.Arguments);
            var arg = Assert.IsType<Expr.IntLiteral>(windowFunc.Function.Arguments[0]);
            Assert.Equal(4, arg.Value);
        }

        // === Aggregates with OVER ===

        [Fact]
        public void Parse_SumWithOver_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(amount) OVER (PARTITION BY customer_id) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameType.Rows, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            var offset = Assert.IsType<Expr.IntLiteral>(windowFunc.Over.Frame.Start.Offset);
            Assert.Equal(3, offset.Value);
        }

        [Fact]
        public void Parse_RowsBetween_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            var offset = Assert.IsType<Expr.IntLiteral>(windowFunc.Over.Frame.Start.Offset);
            Assert.Equal(2, offset.Value);
            Assert.Equal(WindowFrameBoundType.CurrentRow, windowFunc.Over.Frame.End.BoundType);
        }

        [Fact]
        public void Parse_RangeBetween_HasCorrectFrame()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT SUM(x) OVER (ORDER BY y RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("ROWS", item.Alias.Name);
        }

        [Fact]
        public void Parse_RankAsColumnName_ParsesAsIdentifier()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT RANK FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("RANK", columnId.ColumnName.Name);
        }

        [Fact]
        public void Parse_PartitionAsColumnName_ParsesAsIdentifier()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT PARTITION FROM T");

            // Assert
            var item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            var columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("PARTITION", columnId.ColumnName.Name);
        }

        [Fact]
        public void Parse_MultipleContextualKeywordsAsColumns_ParsesCorrectly()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT PARTITION, ROWS, PRECEDING FROM T");

            // Assert
            Assert.Equal(3, SelectExpressionOf(select).Columns.Count);
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
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region WHERE Clause / Search Condition Tests

        [Fact]
        public void Parse_WhereComparison_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T WHERE a = 1");

            // Assert
            Assert.NotNull(SelectExpressionOf(select).Where);
            var comparison = Assert.IsType<AST.Predicate.Comparison>(SelectExpressionOf(select).Where);
            Assert.IsType<Expr.ColumnIdentifier>(comparison.Left);
            Assert.Equal(AST.ComparisonOperator.Equal, comparison.Operator);
            Assert.IsType<Expr.IntLiteral>(comparison.Right);
        }

        [Fact]
        public void Parse_WhereIsNull_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T WHERE a IS NULL");

            // Assert
            var nullPred = Assert.IsType<AST.Predicate.Null>(SelectExpressionOf(select).Where);
            Assert.False(nullPred.Negated);
        }

        [Fact]
        public void Parse_WhereIsNotNull_HasCorrectStructure()
        {
            // Arrange & Act
            var select = ParseSelect("SELECT a FROM T WHERE a IS NOT NULL");

            // Assert
            var nullPred = Assert.IsType<AST.Predicate.Null>(SelectExpressionOf(select).Where);
            Assert.True(nullPred.Negated);
        }

        [Fact]
        public void Parse_WhereAndOr_HasCorrectPrecedence()
        {
            // a = 1 AND b = 2 OR c = 3 should be (a=1 AND b=2) OR c=3
            var select = ParseSelect("SELECT a FROM T WHERE a = 1 AND b = 2 OR c = 3");

            var orPred = Assert.IsType<AST.Predicate.Or>(SelectExpressionOf(select).Where);
            Assert.IsType<AST.Predicate.And>(orPred.Left);
            Assert.IsType<AST.Predicate.Comparison>(orPred.Right);
        }

        [Fact]
        public void Parse_WhereNot_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE NOT a = 1");

            var notPred = Assert.IsType<AST.Predicate.Not>(SelectExpressionOf(select).Where);
            Assert.IsType<AST.Predicate.Comparison>(notPred.Predicate);
        }

        [Fact]
        public void Parse_WhereLike_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE a LIKE '%test%'");

            var likePred = Assert.IsType<AST.Predicate.Like>(SelectExpressionOf(select).Where);
            Assert.False(likePred.Negated);
            Assert.IsType<Expr.ColumnIdentifier>(likePred.Left);
            Assert.IsType<Expr.StringLiteral>(likePred.Pattern);
        }

        [Fact]
        public void Parse_WhereBetween_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE a BETWEEN 1 AND 10");

            var between = Assert.IsType<AST.Predicate.Between>(SelectExpressionOf(select).Where);
            Assert.False(between.Negated);
        }

        [Fact]
        public void Parse_WhereInList_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE a IN (1, 2, 3)");

            var inPred = Assert.IsType<AST.Predicate.In>(SelectExpressionOf(select).Where);
            Assert.False(inPred.Negated);
            Assert.NotNull(inPred.ValueList);
            Assert.Equal(3, inPred.ValueList.Count);
        }

        [Fact]
        public void Parse_WhereExists_HasCorrectStructure()
        {
            var select = ParseSelect("SELECT a FROM T WHERE EXISTS (SELECT 1 FROM T2)");

            var exists = Assert.IsType<AST.Predicate.Exists>(SelectExpressionOf(select).Where);
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
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region PIVOT, UNPIVOT, VALUES, Rowset Functions, Derived Column Aliases

        // ---- VALUES table source ----

        [Fact]
        public void Parse_ValuesTableSource_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = ParseSelect("SELECT a FROM (VALUES (1, 'x'), (2, 'y')) AS t(id, name)");
            FromClause from = SelectExpressionOf(select).From;

            // Assert
            ValuesTableSource values = Assert.IsType<ValuesTableSource>(from.TableSources[0]);
            Assert.Equal(2, values.Rows.Count);
            Assert.Equal(2, values.Rows[0].Values.Count);
            Assert.Equal(2, values.Rows[1].Values.Count);
            Assert.NotNull(values.Alias);
            Assert.Equal("t", values.Alias.Name);
            Assert.NotNull(values.ColumnAliases);
            Assert.Equal(2, values.ColumnAliases.ColumnNames.Count);
            Assert.Equal("id", values.ColumnAliases.ColumnNames[0].Name);
            Assert.Equal("name", values.ColumnAliases.ColumnNames[1].Name);
        }

        [Theory]
        [InlineData("SELECT a FROM (VALUES (1, 'x'), (2, 'y')) AS t(id, name)")]
        [InlineData("SELECT a FROM (VALUES (1), (2), (3)) AS t(id)")]
        [InlineData("SELECT a FROM (VALUES (1 + 2, 'hello')) AS t(val, txt)")]
        public void Parse_ValuesTableSource_RoundTripsCorrectly(string source)
        {
            Stmt stmt = ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- Derived column aliases on subquery ----

        [Fact]
        public void Parse_SubqueryDerivedColumnAliases_HasCorrectStructure()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM (SELECT 1, 2) AS t(col1, col2)");
            FromClause from = SelectExpressionOf(select).From;

            SubqueryReference subRef = Assert.IsType<SubqueryReference>(from.TableSources[0]);
            Assert.NotNull(subRef.Alias);
            Assert.Equal("t", subRef.Alias.Name);
            Assert.NotNull(subRef.ColumnAliases);
            Assert.Equal(2, subRef.ColumnAliases.ColumnNames.Count);
            Assert.Equal("col1", subRef.ColumnAliases.ColumnNames[0].Name);
            Assert.Equal("col2", subRef.ColumnAliases.ColumnNames[1].Name);
        }

        [Theory]
        [InlineData("SELECT a FROM (SELECT 1, 2) AS t(col1, col2)")]
        [InlineData("SELECT a FROM (SELECT x FROM T) AS sub(val)")]
        public void Parse_SubqueryDerivedColumnAliases_RoundTripsCorrectly(string source)
        {
            Stmt stmt = ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- PIVOT ----

        [Fact]
        public void Parse_PivotTableSource_HasCorrectStructure()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb, Mar)) AS pvt");
            FromClause from = SelectExpressionOf(select).From;

            PivotTableSource pivot = Assert.IsType<PivotTableSource>(from.TableSources[0]);
            Assert.IsType<TableReference>(pivot.Source);
            Assert.Equal("SUM", pivot.AggregateFunction.Callee.ObjectName.Name);
            Assert.Equal("Month", pivot.PivotColumn.ObjectName.Name);
            Assert.Equal(3, pivot.ValueList.Count);
            Assert.NotNull(pivot.Alias);
            Assert.Equal("pvt", pivot.Alias.Name);
        }

        [Theory]
        [InlineData("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb, Mar)) AS pvt")]
        [InlineData("SELECT a FROM T PIVOT (COUNT(Id) FOR Status IN (Active, Inactive)) AS p")]
        public void Parse_PivotTableSource_RoundTripsCorrectly(string source)
        {
            Stmt stmt = ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- UNPIVOT ----

        [Fact]
        public void Parse_UnpivotTableSource_HasCorrectStructure()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM T UNPIVOT (Value FOR Quarter IN (Q1, Q2, Q3, Q4)) AS unpvt");
            FromClause from = SelectExpressionOf(select).From;

            UnpivotTableSource unpivot = Assert.IsType<UnpivotTableSource>(from.TableSources[0]);
            Assert.IsType<TableReference>(unpivot.Source);
            Assert.Equal("Value", unpivot.ValueColumn.ObjectName.Name);
            Assert.Equal("Quarter", unpivot.PivotColumn.ObjectName.Name);
            Assert.Equal(4, unpivot.ColumnList.Count);
            Assert.Equal("Q1", unpivot.ColumnList[0].Name);
            Assert.Equal("Q4", unpivot.ColumnList[3].Name);
            Assert.NotNull(unpivot.Alias);
            Assert.Equal("unpvt", unpivot.Alias.Name);
        }

        [Theory]
        [InlineData("SELECT a FROM T UNPIVOT (Value FOR Quarter IN (Q1, Q2, Q3, Q4)) AS unpvt")]
        [InlineData("SELECT a FROM T UNPIVOT (Amount FOR Year IN (Y2020, Y2021)) AS u")]
        public void Parse_UnpivotTableSource_RoundTripsCorrectly(string source)
        {
            Stmt stmt = ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- Rowset functions ----

        [Fact]
        public void Parse_OpenQueryRowsetFunction_HasCorrectStructure()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM OPENQUERY(LinkedServer, 'SELECT 1') AS oq");
            FromClause from = SelectExpressionOf(select).From;

            RowsetFunctionReference rowset = Assert.IsType<RowsetFunctionReference>(from.TableSources[0]);
            Assert.Equal("OPENQUERY", rowset.FunctionCall.Callee.ObjectName.Name);
            Assert.Equal(2, rowset.FunctionCall.Arguments.Count);
            Assert.NotNull(rowset.Alias);
            Assert.Equal("oq", rowset.Alias.Name);
        }

        [Theory]
        [InlineData("SELECT a FROM OPENQUERY(LinkedServer, 'SELECT 1') AS oq")]
        [InlineData("SELECT a FROM OPENROWSET('SQLNCLI', 'Server=srv;', 'SELECT 1') AS ors")]
        public void Parse_RowsetFunction_RoundTripsCorrectly(string source)
        {
            Stmt stmt = ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- Combined / edge cases ----

        [Theory]
        [InlineData("SELECT a FROM (SELECT 1) AS t")]
        [InlineData("SELECT a FROM (VALUES (1)) AS t(id)")]
        [InlineData("SELECT a FROM T PIVOT (MAX(Val) FOR Col IN (A, B)) AS p")]
        [InlineData("SELECT a FROM T UNPIVOT (Val FOR Col IN (A, B)) AS u")]
        public void Parse_PivotUnpivotValuesFeatures_RoundTripsCorrectly(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region Integration Tests - Complex FROM + WHERE Combinations

        [Theory]
        // JOIN + WHERE
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id WHERE T1.active = 1")]
        [InlineData("SELECT a FROM T1 LEFT JOIN T2 ON T1.id = T2.id WHERE T2.id IS NULL")]
        // Multi-join chain + WHERE
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id LEFT JOIN T3 ON T2.id = T3.id WHERE T1.x > 0")]
        // Qualified names + JOIN
        [InlineData("SELECT a FROM dbo.T1 t1 CROSS JOIN dbo.T2 t2")]
        [InlineData("SELECT a FROM dbo.T1 t1 INNER JOIN dbo.T2 t2 ON t1.id = t2.id")]
        // Table hints + JOIN
        [InlineData("SELECT a FROM T1 WITH (NOLOCK) INNER JOIN T2 WITH (NOLOCK) ON T1.id = T2.id")]
        // CROSS APPLY with subquery
        [InlineData("SELECT a FROM T1 CROSS APPLY (SELECT TOP 1 b FROM T2 WHERE T2.id = T1.id) AS sub")]
        // Join hint with qualified names
        [InlineData("SELECT a FROM T1 t1 INNER LOOP JOIN T2 t2 ON t1.id = t2.id")]
        // PIVOT + WHERE
        [InlineData("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb)) AS pvt WHERE pvt.Jan > 0")]
        // VALUES + WHERE
        [InlineData("SELECT a FROM (VALUES (1, 'x'), (2, 'y')) AS t(id, name) WHERE t.id > 1")]
        // Subquery derived table + WHERE
        [InlineData("SELECT a FROM (SELECT x, y FROM T) AS sub WHERE sub.x = 1")]
        // Table variable + JOIN
        [InlineData("SELECT a FROM @TempTable t INNER JOIN T2 ON t.id = T2.id")]
        // Multiple comma-separated sources + WHERE
        [InlineData("SELECT a FROM T1, T2 WHERE T1.id = T2.id")]
        // Complex predicates in ON clause
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id AND T1.type = T2.type")]
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id OR T1.alt_id = T2.id")]
        // Nested subquery in FROM + JOIN
        [InlineData("SELECT a FROM (SELECT x FROM T1) AS sub INNER JOIN T2 ON sub.x = T2.id")]
        // OUTER APPLY
        [InlineData("SELECT a FROM T1 OUTER APPLY (SELECT b FROM T2 WHERE T2.id = T1.id) AS sub")]
        // Full outer join
        [InlineData("SELECT a FROM T1 FULL OUTER JOIN T2 ON T1.id = T2.id WHERE T1.id IS NOT NULL")]
        // Parenthesized join group
        [InlineData("SELECT a FROM (T1 INNER JOIN T2 ON T1.id = T2.id) CROSS JOIN T3")]
        // Derived column aliases on subquery + JOIN
        [InlineData("SELECT a FROM (SELECT 1, 2) AS t(c1, c2) INNER JOIN T2 ON t.c1 = T2.id")]
        public void Parse_IntegrationTests_RoundTripsCorrectly(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_JoinWithWhere_HasCorrectASTStructure()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id WHERE T1.active = 1");
            SelectExpression expr = SelectExpressionOf(select);

            // FROM should contain a QualifiedJoin
            QualifiedJoin join = Assert.IsType<QualifiedJoin>(expr.From.TableSources[0]);
            Assert.Equal(JoinType.Inner, join.JoinType);

            TableReference t1 = Assert.IsType<TableReference>(join.Left);
            Assert.Equal("T1", t1.TableName.ObjectName.Name);

            TableReference t2 = Assert.IsType<TableReference>(join.Right);
            Assert.Equal("T2", t2.TableName.ObjectName.Name);

            // ON condition should be a comparison
            Assert.IsType<AST.Predicate.Comparison>(join.OnCondition);

            // WHERE should be a comparison predicate
            Assert.NotNull(expr.Where);
            Assert.IsType<AST.Predicate.Comparison>(expr.Where);
        }

        [Fact]
        public void Parse_CrossApplyWithSubquery_HasCorrectASTStructure()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM T1 CROSS APPLY (SELECT b FROM T2 WHERE T2.id = T1.id) AS sub");
            SelectExpression expr = SelectExpressionOf(select);

            ApplyJoin apply = Assert.IsType<ApplyJoin>(expr.From.TableSources[0]);
            Assert.Equal(ApplyType.Cross, apply.ApplyType);

            TableReference t1 = Assert.IsType<TableReference>(apply.Left);
            Assert.Equal("T1", t1.TableName.ObjectName.Name);

            SubqueryReference sub = Assert.IsType<SubqueryReference>(apply.Right);
            Assert.Equal("sub", sub.Alias.Name);
        }

        #endregion

        #region Bug Fix Tests

        [Theory]
        [InlineData("SELECT a FROM T WHERE a IN (SELECT b FROM T2)")]
        [InlineData("SELECT a FROM T WHERE a NOT IN (SELECT b FROM T2)")]
        public void Parse_InSubquery_RoundTripsCorrectly(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Theory]
        [InlineData("SELECT a FROM T WHERE (a = 1)")]
        [InlineData("SELECT a FROM T WHERE (a = 1 AND b = 2)")]
        [InlineData("SELECT a FROM T WHERE (a = 1) AND b = 2")]
        [InlineData("SELECT a FROM T WHERE (a + b) > 0")]
        public void Parse_GroupedPredicate_RoundTripsCorrectly(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_GroupedPredicate_ProducesGroupingNode()
        {
            Stmt.Select select = ParseSelect("SELECT a FROM T WHERE (a = 1)");

            var grouping = Assert.IsType<AST.Predicate.Grouping>(SelectExpressionOf(select).Where);
            Assert.IsType<AST.Predicate.Comparison>(grouping.Predicate);
        }

        [Fact]
        public void Parse_PivotInValues_AcceptsIdentifiers()
        {
            // PIVOT IN values must be identifiers (they become output column names)
            Stmt.Select select = ParseSelect("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb)) AS pvt");
            PivotTableSource pivot = Assert.IsType<PivotTableSource>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(2, pivot.ValueList.Count);
            Assert.Equal("Jan", pivot.ValueList[0].Name);
            Assert.Equal("Feb", pivot.ValueList[1].Name);
        }

        [Fact]
        public void Parse_PivotInValues_RejectsExpressions()
        {
            // PIVOT IN values cannot be arbitrary expressions like 1 + 2
            Assert.Throws<ParseError>(() =>
                ParseSelect("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (1 + 2)) AS pvt"));
        }

        #endregion

        #region COUNT(*) / Star Argument Tests

        [Theory]
        [InlineData("SELECT COUNT(*) FROM T")]
        [InlineData("SELECT a, COUNT(*) FROM T GROUP BY a")]
        [InlineData("SELECT COUNT(*) FROM T WHERE x > 1")]
        public void Parse_CountStar(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_CountStar_Structure()
        {
            var stmt = ParseSelect("SELECT COUNT(*) FROM T");
            var selectColumn = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            var funcCall = Assert.IsType<Expr.FunctionCall>(selectColumn.Expression);
            Assert.Equal(1, funcCall.Arguments.Count);
            Assert.IsType<Expr.Wildcard>(funcCall.Arguments[0]);
        }

        #endregion

        #region GROUP BY Tests

        [Theory]
        [InlineData("SELECT a, SUM(b) FROM T GROUP BY a")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY a, b")]
        [InlineData("SELECT DATEPART(yyyy, d), SUM(e) FROM T GROUP BY DATEPART(yyyy, d)")]
        [InlineData("SELECT a, SUM(b) FROM T WHERE x > 1 GROUP BY a")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY ROLLUP(a, b)")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY CUBE(a, b)")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS(a, b)")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS((a, b), a, ())")]
        [InlineData("SELECT a, b, c, SUM(d) FROM T GROUP BY ROLLUP((a, b), c)")]
        [InlineData("SELECT SUM(c) FROM T GROUP BY ()")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY a, ROLLUP(b)")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS(ROLLUP(a, b), ())")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS(CUBE(a, b), (a), ())")]
        public void Parse_GroupBy(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_GroupBy_SimpleStructure()
        {
            var stmt = ParseSelect("SELECT a, SUM(b) FROM T GROUP BY a");
            var groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            Assert.IsType<GroupByExpression>(groupBy.Items[0]);
        }

        [Fact]
        public void Parse_GroupBy_RollupStructure()
        {
            var stmt = ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY ROLLUP(a, b)");
            var groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            var rollup = Assert.IsType<GroupByRollup>(groupBy.Items[0]);
            Assert.Equal(2, rollup.Items.Count);
        }

        [Fact]
        public void Parse_GroupBy_CubeStructure()
        {
            var stmt = ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY CUBE(a, b)");
            var groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            var cube = Assert.IsType<GroupByCube>(groupBy.Items[0]);
            Assert.Equal(2, cube.Items.Count);
        }

        [Fact]
        public void Parse_GroupBy_GroupingSetsStructure()
        {
            var stmt = ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS((a, b), a, ())");
            var groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            var gs = Assert.IsType<GroupByGroupingSets>(groupBy.Items[0]);
            Assert.Equal(3, gs.Items.Count);
            Assert.IsType<GroupByComposite>(gs.Items[0]);
            Assert.IsType<GroupByExpression>(gs.Items[1]);
            Assert.IsType<GroupByGrandTotal>(gs.Items[2]);
        }

        [Fact]
        public void Parse_GroupBy_CompositeInRollup()
        {
            var stmt = ParseSelect("SELECT a, b, c, SUM(d) FROM T GROUP BY ROLLUP((a, b), c)");
            var rollup = Assert.IsType<GroupByRollup>(SelectExpressionOf(stmt).GroupBy.Items[0]);

            Assert.Equal(2, rollup.Items.Count);
            var composite = Assert.IsType<GroupByComposite>(rollup.Items[0]);
            Assert.Equal(2, composite.Expressions.Count);
            Assert.IsType<GroupByExpression>(rollup.Items[1]);
        }

        [Fact]
        public void Parse_GroupBy_MixedItems()
        {
            var stmt = ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY a, ROLLUP(b)");
            var groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.Equal(2, groupBy.Items.Count);
            Assert.IsType<GroupByExpression>(groupBy.Items[0]);
            Assert.IsType<GroupByRollup>(groupBy.Items[1]);
        }

        [Fact]
        public void Parse_GroupBy_NoGroupBy_ReturnsNull()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            Assert.Null(SelectExpressionOf(stmt).GroupBy);
        }

        #endregion

        #region HAVING Tests

        [Theory]
        [InlineData("SELECT a, SUM(b) FROM T GROUP BY a HAVING SUM(b) > 10")]
        [InlineData("SELECT a, SUM(b) FROM T GROUP BY a HAVING SUM(b) > 10 AND SUM(b) < 100")]
        [InlineData("SELECT a FROM T GROUP BY a HAVING a > 1")]
        public void Parse_Having(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Having_Structure()
        {
            var stmt = ParseSelect("SELECT a, SUM(b) FROM T GROUP BY a HAVING SUM(b) > 10");
            Assert.NotNull(SelectExpressionOf(stmt).GroupBy);
            Assert.NotNull(SelectExpressionOf(stmt).Having);
        }

        [Fact]
        public void Parse_Having_NoHaving_ReturnsNull()
        {
            var stmt = ParseSelect("SELECT a, SUM(b) FROM T GROUP BY a");
            Assert.Null(SelectExpressionOf(stmt).Having);
        }

        #endregion

        #region ORDER BY Tests

        [Theory]
        [InlineData("SELECT a FROM T ORDER BY a")]
        [InlineData("SELECT a, b FROM T ORDER BY a ASC, b DESC")]
        [InlineData("SELECT a FROM T ORDER BY a DESC")]
        [InlineData("SELECT a FROM T WHERE x > 1 ORDER BY a")]
        [InlineData("SELECT a, SUM(b) FROM T GROUP BY a ORDER BY SUM(b) DESC")]
        [InlineData("SELECT a, SUM(b) FROM T GROUP BY a HAVING SUM(b) > 10 ORDER BY a")]
        public void Parse_OrderBy(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_OrderBy_Structure()
        {
            var stmt = ParseSelect("SELECT a, b FROM T ORDER BY a ASC, b DESC");
            var orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.Equal(2, orderBy.Items.Count);
            Assert.False(orderBy.Items[0].Descending);
            Assert.True(orderBy.Items[1].Descending);
        }

        [Fact]
        public void Parse_OrderBy_NoOrderBy_IsNull()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            Assert.Null(stmt.Query.OrderBy);
        }

        [Theory]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 10 ROWS")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 0 ROWS")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 10 ROW")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET @skip ROWS")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 10 ROWS FETCH FIRST 5 ROWS ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 10 ROWS FETCH NEXT 5 ROW ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET 10 ROW FETCH FIRST 1 ROW ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET (2 * 5) ROWS FETCH NEXT (10 + 5) ROWS ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a ASC, b DESC OFFSET 10 ROWS FETCH NEXT 25 ROWS ONLY")]
        public void Parse_OffsetFetch(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_OffsetOnly_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T ORDER BY a OFFSET 10 ROWS");
            var orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.Equal(1, orderBy.Items.Count);
            Assert.IsType<Expr.IntLiteral>(orderBy.OffsetCount);
            Assert.Null(orderBy.FetchCount);
        }

        [Fact]
        public void Parse_OffsetFetch_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T ORDER BY a OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY");
            var orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.IsType<Expr.IntLiteral>(orderBy.OffsetCount);
            Assert.IsType<Expr.IntLiteral>(orderBy.FetchCount);
        }

        [Fact]
        public void Parse_OffsetFetch_NoOffset_IsNull()
        {
            var stmt = ParseSelect("SELECT a FROM T ORDER BY a");
            var orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.Null(orderBy.OffsetCount);
            Assert.Null(orderBy.FetchCount);
        }

        [Fact]
        public void Parse_OffsetFetch_WithSetOperation()
        {
            string source = "SELECT a FROM T1 UNION ALL SELECT b FROM T2 ORDER BY a OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY";
            Assert.Equal(source, RoundTrip(source));

            var stmt = ParseSelect(source);
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.NotNull(setOp.OrderBy);
            Assert.IsType<Expr.IntLiteral>(setOp.OrderBy.OffsetCount);
            Assert.IsType<Expr.IntLiteral>(setOp.OrderBy.FetchCount);
        }

        [Fact]
        public void Parse_Offset_AsIdentifier()
        {
            string source = "SELECT OFFSET FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Next_AsIdentifier()
        {
            string source = "SELECT NEXT FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region CTE Tests

        [Theory]
        [InlineData("WITH cte AS (SELECT a FROM T) SELECT a FROM cte")]
        [InlineData("WITH cte(x, y) AS (SELECT a, b FROM T) SELECT x, y FROM cte")]
        [InlineData("WITH c1 AS (SELECT a FROM T), c2 AS (SELECT b FROM U) SELECT a, b FROM c1, c2")]
        [InlineData("WITH cte AS (SELECT a FROM T WHERE x > 1) SELECT a FROM cte ORDER BY a")]
        public void Parse_Cte(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Cte_SimpleStructure()
        {
            var stmt = ParseSelect("WITH cte AS (SELECT a FROM T) SELECT a FROM cte");
            Assert.NotNull(stmt.CteStmt);
            Assert.Equal(1, stmt.CteStmt.Ctes.Count);
            Assert.Equal("cte", stmt.CteStmt.Ctes[0].Name);
            Assert.Null(stmt.CteStmt.Ctes[0].ColumnNames);
        }

        [Fact]
        public void Parse_Cte_WithColumnNames()
        {
            var stmt = ParseSelect("WITH cte(x, y) AS (SELECT a, b FROM T) SELECT x, y FROM cte");
            var def = stmt.CteStmt.Ctes[0];
            Assert.NotNull(def.ColumnNames);
            Assert.Equal(2, def.ColumnNames.ColumnNames.Count);
        }

        [Fact]
        public void Parse_Cte_MultipleCtes()
        {
            var stmt = ParseSelect("WITH c1 AS (SELECT a FROM T), c2 AS (SELECT b FROM U) SELECT a, b FROM c1, c2");
            Assert.Equal(2, stmt.CteStmt.Ctes.Count);
            Assert.Equal("c1", stmt.CteStmt.Ctes[0].Name);
            Assert.Equal("c2", stmt.CteStmt.Ctes[1].Name);
        }

        [Fact]
        public void Parse_NoCte_ReturnsNull()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            Assert.Null(stmt.CteStmt);
        }

        #endregion

        #region Integration Tests - Full SELECT Pipeline

        [Theory]
        [InlineData("WITH cte AS (SELECT a, b FROM T) SELECT a, SUM(b) FROM cte WHERE a > 1 GROUP BY a HAVING SUM(b) > 10 ORDER BY a DESC")]
        [InlineData("SELECT a, b, SUM(c) FROM T JOIN U ON T.id = U.id WHERE a > 1 GROUP BY a, b HAVING SUM(c) > 0 ORDER BY a, b DESC")]
        public void Parse_FullSelectPipeline(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region NULL Literal Tests

        [Theory]
        [InlineData("SELECT NULL")]
        [InlineData("SELECT a FROM T WHERE a IS NULL")]
        [InlineData("SELECT NULL, a, NULL FROM T")]
        public void Parse_NullLiteral_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_NullLiteral_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT NULL");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Assert.IsType<Expr.NullLiteral>(col.Expression);
        }

        #endregion

        #region Modulo Tests

        [Theory]
        [InlineData("SELECT a % b FROM T")]
        [InlineData("SELECT 10 % 3")]
        [InlineData("SELECT a % b % c FROM T")]
        [InlineData("SELECT a + b % c FROM T")]
        public void Parse_Modulo_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Modulo_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT 10 % 3");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Binary bin = Assert.IsType<Expr.Binary>(col.Expression);
            Assert.Equal(Expr.ArithmeticOperator.Modulo, bin.Operator);
        }

        #endregion

        #region CASE Expression Tests

        [Theory]
        [InlineData("SELECT CASE x WHEN 1 THEN 'a' WHEN 2 THEN 'b' END FROM T")]
        [InlineData("SELECT CASE x WHEN 1 THEN 'a' ELSE 'z' END FROM T")]
        [InlineData("SELECT CASE x WHEN 1 THEN 'a' WHEN 2 THEN 'b' ELSE 'z' END FROM T")]
        [InlineData("SELECT CASE WHEN x > 1 THEN 'a' WHEN x > 2 THEN 'b' END FROM T")]
        [InlineData("SELECT CASE WHEN x > 1 THEN 'a' ELSE 'z' END FROM T")]
        [InlineData("SELECT CASE WHEN x > 1 THEN 'a' WHEN x > 2 THEN 'b' ELSE 'z' END FROM T")]
        [InlineData("SELECT CASE WHEN x IS NULL THEN 0 ELSE x END FROM T")]
        [InlineData("SELECT CASE status WHEN 1 THEN 'Active' WHEN 2 THEN 'Inactive' WHEN 3 THEN 'Deleted' ELSE 'Unknown' END AS StatusText FROM Users")]
        public void Parse_CaseExpression_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_SimpleCase_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT CASE x WHEN 1 THEN 'a' WHEN 2 THEN 'b' ELSE 'z' END");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.SimpleCase sc = Assert.IsType<Expr.SimpleCase>(col.Expression);

            Assert.IsType<Expr.ColumnIdentifier>(sc.Operand);
            Assert.Equal(2, sc.WhenClauses.Count);
            Assert.NotNull(sc.ElseResult);
        }

        [Fact]
        public void Parse_SearchedCase_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT CASE WHEN x > 1 THEN 'a' WHEN x > 2 THEN 'b' ELSE 'z' END");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.SearchedCase sc = Assert.IsType<Expr.SearchedCase>(col.Expression);

            Assert.Equal(2, sc.WhenClauses.Count);
            Assert.NotNull(sc.ElseResult);
            Assert.IsType<AST.Predicate.Comparison>(sc.WhenClauses[0].Condition);
        }

        [Fact]
        public void Parse_CaseExpression_NoElse()
        {
            Stmt.Select stmt = ParseSelect("SELECT CASE x WHEN 1 THEN 'a' END");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.SimpleCase sc = Assert.IsType<Expr.SimpleCase>(col.Expression);
            Assert.Null(sc.ElseResult);
        }

        #endregion

        #region CAST / TRY_CAST Tests

        [Theory]
        [InlineData("SELECT CAST(x AS INT) FROM T")]
        [InlineData("SELECT CAST(x AS VARCHAR(50)) FROM T")]
        [InlineData("SELECT CAST(x AS DECIMAL(10, 2)) FROM T")]
        [InlineData("SELECT TRY_CAST(x AS INT) FROM T")]
        [InlineData("SELECT TRY_CAST(x AS NVARCHAR(MAX)) FROM T")]
        [InlineData("SELECT CAST(x + 1 AS BIGINT) FROM T")]
        public void Parse_Cast_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Cast_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT CAST(x AS VARCHAR(50))");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.CastExpression cast = Assert.IsType<Expr.CastExpression>(col.Expression);

            Assert.IsType<Expr.ColumnIdentifier>(cast.Expression);
            Assert.Equal("VARCHAR", cast.DataType.TypeName);
            Assert.Single(cast.DataType.Parameters);
            Assert.Equal(TokenType.CAST, cast._castKeyword.Type);
        }

        [Fact]
        public void Parse_TryCast_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT TRY_CAST(x AS INT)");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.CastExpression cast = Assert.IsType<Expr.CastExpression>(col.Expression);
            Assert.Equal(TokenType.TRY_CAST, cast._castKeyword.Type);
            Assert.Null(cast.DataType.Parameters);
        }

        #endregion

        #region CONVERT / TRY_CONVERT Tests

        [Theory]
        [InlineData("SELECT CONVERT(INT, x) FROM T")]
        [InlineData("SELECT CONVERT(VARCHAR(50), x) FROM T")]
        [InlineData("SELECT CONVERT(VARCHAR(10), x, 121) FROM T")]
        [InlineData("SELECT TRY_CONVERT(INT, x) FROM T")]
        [InlineData("SELECT TRY_CONVERT(DATE, x, 103) FROM T")]
        public void Parse_Convert_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Convert_WithStyle_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT CONVERT(VARCHAR(10), x, 121)");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.ConvertExpression conv = Assert.IsType<Expr.ConvertExpression>(col.Expression);

            Assert.Equal("VARCHAR", conv.DataType.TypeName);
            Assert.Single(conv.DataType.Parameters);
            Assert.IsType<Expr.ColumnIdentifier>(conv.Expression);
            Assert.NotNull(conv.Style);
            Assert.Equal(TokenType.CONVERT, conv._convertKeyword.Type);
        }

        [Fact]
        public void Parse_Convert_WithoutStyle_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT CONVERT(INT, x)");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.ConvertExpression conv = Assert.IsType<Expr.ConvertExpression>(col.Expression);
            Assert.Null(conv.Style);
        }

        [Fact]
        public void Parse_TryConvert_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT TRY_CONVERT(DATE, x, 103)");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.ConvertExpression conv = Assert.IsType<Expr.ConvertExpression>(col.Expression);
            Assert.Equal(TokenType.TRY_CONVERT, conv._convertKeyword.Type);
        }

        #endregion

        #region Integration Tests - CASE/CAST/CONVERT Combinations

        [Theory]
        [InlineData("SELECT CASE WHEN CAST(x AS INT) > 1 THEN 'yes' ELSE 'no' END FROM T")]
        [InlineData("SELECT CAST(CASE WHEN x > 1 THEN x ELSE 0 END AS VARCHAR(10)) FROM T")]
        [InlineData("SELECT CONVERT(VARCHAR(10), CASE status WHEN 1 THEN 'A' ELSE 'B' END) FROM T")]
        [InlineData("SELECT a, CASE WHEN a % 2 = 0 THEN 'even' ELSE 'odd' END FROM T")]
        [InlineData("SELECT COALESCE(CAST(x AS INT), 0) FROM T")]
        [InlineData("SELECT CASE WHEN x IS NULL THEN CAST(0 AS INT) ELSE CAST(x AS INT) END FROM T")]
        public void Parse_CaseCastConvert_Integration_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region OPTION Clause - Simple Hint Round-Trip Tests

        [Theory]
        [InlineData("SELECT a FROM T OPTION (HASH GROUP)")]
        [InlineData("SELECT a FROM T OPTION (ORDER GROUP)")]
        [InlineData("SELECT a FROM T OPTION (CONCAT UNION)")]
        [InlineData("SELECT a FROM T OPTION (HASH UNION)")]
        [InlineData("SELECT a FROM T OPTION (MERGE UNION)")]
        [InlineData("SELECT a FROM T OPTION (LOOP JOIN)")]
        [InlineData("SELECT a FROM T OPTION (MERGE JOIN)")]
        [InlineData("SELECT a FROM T OPTION (HASH JOIN)")]
        [InlineData("SELECT a FROM T OPTION (RECOMPILE)")]
        [InlineData("SELECT a FROM T OPTION (EXPAND VIEWS)")]
        [InlineData("SELECT a FROM T OPTION (FORCE ORDER)")]
        [InlineData("SELECT a FROM T OPTION (KEEP PLAN)")]
        [InlineData("SELECT a FROM T OPTION (KEEPFIXED PLAN)")]
        [InlineData("SELECT a FROM T OPTION (ROBUST PLAN)")]
        [InlineData("SELECT a FROM T OPTION (NO_PERFORMANCE_SPOOL)")]
        [InlineData("SELECT a FROM T OPTION (OPTIMIZE FOR UNKNOWN)")]
        [InlineData("SELECT a FROM T OPTION (IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX)")]
        [InlineData("SELECT a FROM T OPTION (DISABLE_OPTIMIZED_PLAN_FORCING)")]
        [InlineData("SELECT a FROM T OPTION (FORCE EXTERNALPUSHDOWN)")]
        [InlineData("SELECT a FROM T OPTION (DISABLE EXTERNALPUSHDOWN)")]
        [InlineData("SELECT a FROM T OPTION (FORCE SCALEOUTEXECUTION)")]
        [InlineData("SELECT a FROM T OPTION (DISABLE SCALEOUTEXECUTION)")]
        public void Parse_OptionSimpleHint_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region OPTION Clause - Value Hint Round-Trip Tests

        [Theory]
        [InlineData("SELECT a FROM T OPTION (FAST 10)")]
        [InlineData("SELECT a FROM T OPTION (MAXDOP 4)")]
        [InlineData("SELECT a FROM T OPTION (MAXRECURSION 100)")]
        [InlineData("SELECT a FROM T OPTION (QUERYTRACEON 9481)")]
        [InlineData("SELECT a FROM T OPTION (MAX_GRANT_PERCENT = 25)")]
        [InlineData("SELECT a FROM T OPTION (MIN_GRANT_PERCENT = 10)")]
        [InlineData("SELECT a FROM T OPTION (LABEL = 'MyQuery')")]
        [InlineData("SELECT a FROM T OPTION (PARAMETERIZATION SIMPLE)")]
        [InlineData("SELECT a FROM T OPTION (PARAMETERIZATION FORCED)")]
        public void Parse_OptionValueHint_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region OPTION Clause - Complex Hint Round-Trip Tests

        [Theory]
        [InlineData("SELECT a FROM T OPTION (OPTIMIZE FOR (@x = 1))")]
        [InlineData("SELECT a FROM T OPTION (OPTIMIZE FOR (@x UNKNOWN))")]
        [InlineData("SELECT a FROM T OPTION (OPTIMIZE FOR (@x = 1, @y UNKNOWN))")]
        [InlineData("SELECT a FROM T OPTION (USE HINT ('ENABLE_HIST_AMENDMENT_FOR_ASC_KEYS'))")]
        [InlineData("SELECT a FROM T OPTION (USE HINT ('FORCE_LEGACY_CARDINALITY_ESTIMATION', 'ENABLE_QUERY_OPTIMIZER_HOTFIXES'))")]
        [InlineData("SELECT a FROM T OPTION (USE PLAN N'<xml/>')")]
        [InlineData("SELECT a FROM T OPTION (TABLE HINT (T, NOLOCK))")]
        [InlineData("SELECT a FROM T OPTION (TABLE HINT (dbo.T, INDEX(1), NOLOCK))")]
        [InlineData("SELECT a FROM T OPTION (FOR TIMESTAMP AS OF '2024-01-01')")]
        public void Parse_OptionComplexHint_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region OPTION Clause - Multiple Hints and Integration

        [Theory]
        [InlineData("SELECT a FROM T OPTION (RECOMPILE, MAXDOP 1)")]
        [InlineData("SELECT a FROM T OPTION (HASH JOIN, FORCE ORDER)")]
        [InlineData("SELECT a FROM T OPTION (FAST 10, RECOMPILE, MAXDOP 4)")]
        [InlineData("SELECT a FROM T WHERE a > 1 OPTION (RECOMPILE)")]
        [InlineData("SELECT a FROM T ORDER BY a OPTION (MAXDOP 4)")]
        [InlineData("SELECT a, SUM(b) FROM T WHERE a > 1 GROUP BY a HAVING SUM(b) > 10 ORDER BY a OPTION (RECOMPILE)")]
        [InlineData("SELECT a FROM (SELECT b FROM T) AS sub OPTION (RECOMPILE)")]
        public void Parse_OptionMultipleAndIntegration_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region OPTION Clause - Structure Tests

        [Fact]
        public void Parse_OptionRecompile_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T OPTION (RECOMPILE)");
            Assert.NotNull(stmt.Option);
            Assert.Equal(1, stmt.Option.Hints.Count);
            Assert.Equal(QueryHintType.Recompile, stmt.Option.Hints[0].HintType);
        }

        [Fact]
        public void Parse_OptionMultipleHints_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T OPTION (RECOMPILE, MAXDOP 1)");
            Assert.NotNull(stmt.Option);
            Assert.Equal(2, stmt.Option.Hints.Count);
            Assert.Equal(QueryHintType.Recompile, stmt.Option.Hints[0].HintType);
            Assert.Equal(QueryHintType.Maxdop, stmt.Option.Hints[1].HintType);
        }

        [Fact]
        public void Parse_OptionFast_HasValue()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T OPTION (FAST 10)");
            QueryHint hint = stmt.Option.Hints[0];
            Assert.Equal(QueryHintType.Fast, hint.HintType);
            Assert.NotNull(hint.Value);
        }

        [Fact]
        public void Parse_OptionOptimizeFor_HasVariables()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T OPTION (OPTIMIZE FOR (@x = 1, @y UNKNOWN))");
            QueryHint hint = stmt.Option.Hints[0];
            Assert.Equal(QueryHintType.OptimizeFor, hint.HintType);
            Assert.Equal(2, hint.OptimizeForVariables.Count);
        }

        [Fact]
        public void Parse_OptionUseHint_HasHintNames()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T OPTION (USE HINT ('ENABLE_HIST_AMENDMENT_FOR_ASC_KEYS'))");
            QueryHint hint = stmt.Option.Hints[0];
            Assert.Equal(QueryHintType.UseHint, hint.HintType);
            Assert.Equal(1, hint.UseHintNames.Count);
        }

        [Fact]
        public void Parse_OptionTableHint_ReusesTableHintParsing()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T OPTION (TABLE HINT (T, NOLOCK))");
            QueryHint hint = stmt.Option.Hints[0];
            Assert.Equal(QueryHintType.QueryTableHint, hint.HintType);
            Assert.NotNull(hint.TableHintObjectName);
            Assert.Equal(1, hint.TableHints.Count);
            Assert.Equal(TableHintType.NoLock, hint.TableHints[0].HintType);
        }

        #endregion

        #region Unicode String Prefix Tests

        [Theory]
        [InlineData("SELECT N'hello'")]
        [InlineData("SELECT N'hello world' FROM T")]
        [InlineData("SELECT a FROM T WHERE name = N'test'")]
        [InlineData("SELECT N'it''s escaped'")]
        public void Parse_UnicodeStringPrefix_RoundTrip(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_UnicodeStringPrefix_Structure()
        {
            Stmt.Select stmt = ParseSelect("SELECT N'hello'");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.StringLiteral lit = Assert.IsType<Expr.StringLiteral>(col.Expression);
            Assert.Equal("hello", lit.Value);
        }

        #endregion

        #region TOP Validation Tests

        [Theory]
        [InlineData("SELECT TOP 10 * FROM T")]
        [InlineData("SELECT TOP (@n) * FROM T")]
        [InlineData("SELECT TOP (10 + 5) * FROM T")]
        [InlineData("SELECT TOP (CAST(@n AS INT)) * FROM T")]
        [InlineData("SELECT TOP 10 a, b FROM T")]
        [InlineData("SELECT TOP 10 PERCENT * FROM T")]
        [InlineData("SELECT TOP (50) PERCENT * FROM T")]
        [InlineData("SELECT TOP 10 PERCENT WITH TIES * FROM T")]
        [InlineData("SELECT TOP (10) WITH TIES * FROM T")]
        public void Parse_Top_ValidExpressions(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Theory]
        [InlineData("SELECT TOP NULL * FROM T")]
        [InlineData("SELECT TOP 'x' * FROM T")]
        [InlineData("SELECT TOP 3.14 * FROM T")]
        [InlineData("SELECT TOP @n * FROM T")]
        public void Parse_Top_RejectsInvalidLiterals(string source)
        {
            Assert.Throws<ParseError>(() => ParseSelect(source));
        }

        [Theory]
        [InlineData("SELECT a AS TIES FROM T")]
        [InlineData("SELECT TIES = a FROM T")]
        [InlineData("SELECT a FROM T AS TIES")]
        [InlineData("SELECT TIES FROM T")]
        public void Parse_Ties_WorksAsIdentifier(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region NULLIF Tests

        [Theory]
        [InlineData("SELECT NULLIF(a, b) FROM T")]
        [InlineData("SELECT NULLIF(a + 1, 0) FROM T")]
        [InlineData("SELECT NULLIF(x, '') AS result FROM T")]
        public void Parse_Nullif(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Nullif_InWhereClause()
        {
            string source = "SELECT a FROM T WHERE NULLIF(a, 0) > 1";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Nullif_IsFunctionCall()
        {
            Stmt.Select stmt = ParseSelect("SELECT NULLIF(a, b) FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall call = Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.Equal("NULLIF", call.Callee.ObjectName.Name);
            Assert.Equal(2, call.Arguments.Count);
        }

        #endregion

        #region OPENXML WITH Tests

        [Theory]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row')")]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row', 2)")]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row', 2) WITH (col1 varchar(50), col2 int)")]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row', 2) WITH (col1 varchar(50) './name', col2 int './id')")]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row') WITH (col1 nvarchar(100))")]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row') WITH (T)")]
        [InlineData("SELECT * FROM OPENXML(@idoc, '/root/row') WITH (dbo.T)")]
        public void Parse_OpenXml_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_OpenXml_WithoutWith_IsOpenXmlExpression()
        {
            Stmt.Select stmt = ParseSelect("SELECT OPENXML(@idoc, '/root/row') FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.OpenXmlExpression openXml = Assert.IsType<Expr.OpenXmlExpression>(col.Expression);
            Assert.Equal(2, openXml.Arguments.Count);
            Assert.Null(openXml.WithClause);
        }

        [Fact]
        public void Parse_OpenXml_WithSchema_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT OPENXML(@idoc, '/root/row', 2) WITH (col1 varchar(50), col2 int) FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.OpenXmlExpression openXml = Assert.IsType<Expr.OpenXmlExpression>(col.Expression);
            Assert.Equal(3, openXml.Arguments.Count);
            Expr.OpenXmlSchemaDeclaration schema = Assert.IsType<Expr.OpenXmlSchemaDeclaration>(openXml.WithClause);
            Assert.Equal(2, schema.Columns.Count);
            Assert.Equal("col1", schema.Columns[0].Name);
            Assert.Equal("col2", schema.Columns[1].Name);
        }

        [Fact]
        public void Parse_OpenXml_WithColPattern_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT OPENXML(@idoc, '/root/row') WITH (name varchar(50) './Name') FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.OpenXmlExpression openXml = Assert.IsType<Expr.OpenXmlExpression>(col.Expression);
            Expr.OpenXmlSchemaDeclaration schema = Assert.IsType<Expr.OpenXmlSchemaDeclaration>(openXml.WithClause);
            Assert.Single(schema.Columns);
            Assert.Equal("name", schema.Columns[0].Name);
            Assert.Equal("'./Name'", schema.Columns[0].ColPattern);
        }

        [Fact]
        public void Parse_OpenXml_WithTableName_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT OPENXML(@idoc, '/root/row') WITH (dbo.T) FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.OpenXmlExpression openXml = Assert.IsType<Expr.OpenXmlExpression>(col.Expression);
            Expr.OpenXmlTableName tableName = Assert.IsType<Expr.OpenXmlTableName>(openXml.WithClause);
            Assert.Equal("T", tableName.TableName.ObjectName.Name);
        }

        #endregion

        #region WITHIN GROUP Tests

        [Theory]
        [InlineData("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name) FROM T")]
        [InlineData("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name ASC) FROM T")]
        [InlineData("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name DESC) FROM T")]
        [InlineData("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY last_name, first_name) FROM T")]
        public void Parse_WithinGroup_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_WithinGroup_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name) FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall call = Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.NotNull(call.WithinGroup);
            Assert.Single(call.WithinGroup.OrderBy);
        }

        [Fact]
        public void Parse_WithinGroup_WithoutClause()
        {
            Stmt.Select stmt = ParseSelect("SELECT STRING_AGG(name, ', ') FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall call = Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.Null(call.WithinGroup);
        }

        #endregion

        #region AT TIME ZONE Tests

        [Theory]
        [InlineData("SELECT created AT TIME ZONE 'UTC' FROM T")]
        [InlineData("SELECT created AT TIME ZONE 'Pacific Standard Time' FROM T")]
        [InlineData("SELECT created AT TIME ZONE @tz FROM T")]
        public void Parse_AtTimeZone_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_AtTimeZone_Chained()
        {
            string source = "SELECT created AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time' FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_AtTimeZone_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT created AT TIME ZONE 'UTC' FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.AtTimeZone atz = Assert.IsType<Expr.AtTimeZone>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(atz.Expression);
            Assert.IsType<Expr.StringLiteral>(atz.TimeZone);
        }

        [Fact]
        public void Parse_AtTimeZone_ChainedStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT created AT TIME ZONE 'UTC' AT TIME ZONE 'PST' FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.AtTimeZone outer = Assert.IsType<Expr.AtTimeZone>(col.Expression);
            Expr.AtTimeZone inner = Assert.IsType<Expr.AtTimeZone>(outer.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(inner.Expression);
        }

        [Fact]
        public void Parse_At_WorksAsIdentifier()
        {
            string source = "SELECT AT FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Time_WorksAsIdentifier()
        {
            string source = "SELECT TIME FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Zone_WorksAsIdentifier()
        {
            string source = "SELECT ZONE FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region IIF Tests

        [Theory]
        [InlineData("SELECT IIF(a > 0, 'positive', 'non-positive') FROM T")]
        [InlineData("SELECT IIF(a = 1 AND b = 2, x, y) FROM T")]
        [InlineData("SELECT IIF(a IS NULL, 0, a) FROM T")]
        [InlineData("SELECT IIF(a > 0, IIF(a > 10, 'big', 'small'), 'zero') FROM T")]
        public void Parse_Iif_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Iif_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT IIF(a > 0, 'yes', 'no') FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Iif iif = Assert.IsType<Expr.Iif>(col.Expression);
            Assert.IsType<AST.Predicate.Comparison>(iif.Condition);
            Assert.IsType<Expr.StringLiteral>(iif.TrueValue);
            Assert.IsType<Expr.StringLiteral>(iif.FalseValue);
        }

        [Fact]
        public void Parse_Iif_WorksAsIdentifier()
        {
            string source = "SELECT IIF FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Iif_WorksAsAlias()
        {
            string source = "SELECT a AS IIF FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region Bitwise Operator Tests

        [Theory]
        [InlineData("SELECT a & b FROM T")]
        [InlineData("SELECT a | b FROM T")]
        [InlineData("SELECT a ^ b FROM T")]
        [InlineData("SELECT ~a FROM T")]
        [InlineData("SELECT a & b | c FROM T")]
        [InlineData("SELECT a & b ^ c FROM T")]
        [InlineData("SELECT ~a & b FROM T")]
        [InlineData("SELECT a | b | c FROM T")]
        public void Parse_BitwiseOperators_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_BitwiseNot_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT ~a FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Unary unary = Assert.IsType<Expr.Unary>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(unary.Right);
        }

        [Fact]
        public void Parse_BitwiseAnd_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a & b FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_BitwiseNot_HigherPrecedenceThanMultiply()
        {
            // ~a * b should parse as (~a) * b
            Stmt.Select stmt = ParseSelect("SELECT ~a * b FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(col.Expression);
            Assert.IsType<Expr.Unary>(binary.Left);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_Bitwise_MultiplyHigherPrecedenceThanAnd()
        {
            // a * b & c should parse as (a * b) & c
            Stmt.Select stmt = ParseSelect("SELECT a * b & c FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Binary outer = Assert.IsType<Expr.Binary>(col.Expression);
            Assert.IsType<Expr.Binary>(outer.Left);
            Assert.IsType<Expr.ColumnIdentifier>(outer.Right);
        }

        #endregion

        #region COLLATE Tests

        [Theory]
        [InlineData("SELECT a COLLATE Latin1_General_CI_AS FROM T")]
        [InlineData("SELECT a COLLATE SQL_Latin1_General_CP1_CI_AS FROM T")]
        public void Parse_Collate_InSelect(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Collate_InWhereClause()
        {
            string source = "SELECT a FROM T WHERE a COLLATE Latin1_General_CI_AS = 'test'";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Collate_InOrderBy()
        {
            string source = "SELECT a FROM T ORDER BY a COLLATE Latin1_General_CI_AS";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Collate_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a COLLATE Latin1_General_CI_AS FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Collate collate = Assert.IsType<Expr.Collate>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(collate.Expression);
            Assert.Equal("Latin1_General_CI_AS", collate.CollationName);
        }

        [Fact]
        public void Parse_Collate_WithStringLiteral()
        {
            string source = "SELECT 'hello' COLLATE Latin1_General_CI_AS FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region CONTAINS / FREETEXT Tests

        [Theory]
        [InlineData("SELECT a FROM T WHERE CONTAINS(a, 'test')")]
        [InlineData("SELECT a FROM T WHERE CONTAINS(*, 'test')")]
        [InlineData("SELECT a FROM T WHERE CONTAINS((a, b), 'test')")]
        [InlineData("SELECT a FROM T WHERE CONTAINS((a, b, c), 'test')")]
        [InlineData("SELECT a FROM T WHERE CONTAINS(a, 'test', LANGUAGE 1033)")]
        [InlineData("SELECT a FROM T WHERE CONTAINS(*, 'test', LANGUAGE @lang)")]
        [InlineData("SELECT a FROM T WHERE CONTAINS(dbo.T.col, 'test')")]
        public void Parse_Contains_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Theory]
        [InlineData("SELECT a FROM T WHERE FREETEXT(a, 'search term')")]
        [InlineData("SELECT a FROM T WHERE FREETEXT(*, 'search term')")]
        [InlineData("SELECT a FROM T WHERE FREETEXT((a, b), 'search term')")]
        [InlineData("SELECT a FROM T WHERE FREETEXT((a, b, c), 'search term')")]
        [InlineData("SELECT a FROM T WHERE FREETEXT(a, 'search term', LANGUAGE 1033)")]
        [InlineData("SELECT a FROM T WHERE FREETEXT(*, 'search term', LANGUAGE @lang)")]
        public void Parse_Freetext_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Contains_WithStar_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T WHERE CONTAINS(*, 'test')");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            Assert.IsType<AST.Predicate.FullTextAllColumns>(contains.Columns);
        }

        [Fact]
        public void Parse_Contains_WithSingleColumn_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T WHERE CONTAINS(col1, 'test')");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            AST.Predicate.FullTextColumnNames columnNames = Assert.IsType<AST.Predicate.FullTextColumnNames>(contains.Columns);
            Assert.Single(columnNames.Columns);
            Assert.Equal("col1", columnNames.Columns[0].ColumnName.Name);
        }

        [Fact]
        public void Parse_Contains_WithColumnList_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T WHERE CONTAINS((col1, col2), 'test')");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            AST.Predicate.FullTextColumnNames columnNames = Assert.IsType<AST.Predicate.FullTextColumnNames>(contains.Columns);
            Assert.Equal(2, columnNames.Columns.Count);
            Assert.Equal("col1", columnNames.Columns[0].ColumnName.Name);
            Assert.Equal("col2", columnNames.Columns[1].ColumnName.Name);
        }

        [Fact]
        public void Parse_Contains_WithLanguage_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T WHERE CONTAINS(a, 'test', LANGUAGE 1033)");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            Assert.NotNull(contains.Language);
        }

        [Fact]
        public void Parse_Freetext_HasCorrectStructure()
        {
            Stmt.Select stmt = ParseSelect("SELECT a FROM T WHERE FREETEXT(col1, 'search')");
            AST.Predicate.Freetext freetext = Assert.IsType<AST.Predicate.Freetext>(SelectExpressionOf(stmt).Where);
            AST.Predicate.FullTextColumnNames columnNames = Assert.IsType<AST.Predicate.FullTextColumnNames>(freetext.Columns);
            Assert.Single(columnNames.Columns);
            Assert.Equal("col1", columnNames.Columns[0].ColumnName.Name);
        }

        [Fact]
        public void Parse_Language_WorksAsIdentifier()
        {
            string source = "SELECT LANGUAGE FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Language_WorksAsAlias()
        {
            string source = "SELECT a AS LANGUAGE FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region Set Operations (UNION / INTERSECT / EXCEPT)

        [Fact]
        public void Parse_BasicUnion_RoundTrips()
        {
            string source = "SELECT a FROM T1 UNION SELECT b FROM T2";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_UnionAll_RoundTrips()
        {
            string source = "SELECT a FROM T1 UNION ALL SELECT b FROM T2";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Intersect_RoundTrips()
        {
            string source = "SELECT a FROM T1 INTERSECT SELECT b FROM T2";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_Except_RoundTrips()
        {
            string source = "SELECT a FROM T1 EXCEPT SELECT b FROM T2";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_UnionWithOrderBy_RoundTrips()
        {
            string source = "SELECT a FROM T1 UNION SELECT b FROM T2 ORDER BY a";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_ChainedUnion_RoundTrips()
        {
            string source = "SELECT a FROM T1 UNION SELECT b FROM T2 UNION SELECT c FROM T3";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_UnionAll_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T1 UNION ALL SELECT b FROM T2");
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.UnionAll, setOp.OperationType);
            Assert.IsType<SelectExpression>(setOp.Left);
            Assert.IsType<SelectExpression>(setOp.Right);
        }

        [Fact]
        public void Parse_Union_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2");
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Union, setOp.OperationType);
        }

        [Fact]
        public void Parse_Intersect_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T1 INTERSECT SELECT b FROM T2");
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Intersect, setOp.OperationType);
        }

        [Fact]
        public void Parse_Except_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T1 EXCEPT SELECT b FROM T2");
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Except, setOp.OperationType);
        }

        [Fact]
        public void Parse_ChainedUnion_IsLeftAssociative()
        {
            // A UNION B UNION C => (A UNION B) UNION C
            var stmt = ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 UNION SELECT c FROM T3");
            var outerOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Union, outerOp.OperationType);
            Assert.IsType<SelectExpression>(outerOp.Right);

            var innerOp = Assert.IsType<SetOperation>(outerOp.Left);
            Assert.Equal(SetOperationType.Union, innerOp.OperationType);
            Assert.IsType<SelectExpression>(innerOp.Left);
            Assert.IsType<SelectExpression>(innerOp.Right);
        }

        [Fact]
        public void Parse_IntersectBindsTighterThanUnion()
        {
            // A UNION B INTERSECT C => A UNION (B INTERSECT C)
            var stmt = ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 INTERSECT SELECT c FROM T3");
            var outerOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Union, outerOp.OperationType);
            Assert.IsType<SelectExpression>(outerOp.Left);

            var innerOp = Assert.IsType<SetOperation>(outerOp.Right);
            Assert.Equal(SetOperationType.Intersect, innerOp.OperationType);
        }

        [Fact]
        public void Parse_IntersectBindsTighterThanExcept()
        {
            // A EXCEPT B INTERSECT C => A EXCEPT (B INTERSECT C)
            var stmt = ParseSelect("SELECT a FROM T1 EXCEPT SELECT b FROM T2 INTERSECT SELECT c FROM T3");
            var outerOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Except, outerOp.OperationType);
            Assert.IsType<SelectExpression>(outerOp.Left);

            var innerOp = Assert.IsType<SetOperation>(outerOp.Right);
            Assert.Equal(SetOperationType.Intersect, innerOp.OperationType);
        }

        [Fact]
        public void Parse_SetOperationWithOrderBy_OrderByOnOutermost()
        {
            var stmt = ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 ORDER BY a");
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(1, setOp.OrderBy.Items.Count);
        }

        [Fact]
        public void Parse_SetOperationWithOption_OptionOnStatement()
        {
            var stmt = ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 ORDER BY a OPTION (RECOMPILE)");
            var setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(1, setOp.OrderBy.Items.Count);
            Assert.NotNull(stmt.Option);
            Assert.Equal(QueryHintType.Recompile, stmt.Option.Hints[0].HintType);
        }

        [Fact]
        public void Parse_UnionInSubquery_RoundTrips()
        {
            string source = "SELECT * FROM (SELECT a FROM T1 UNION SELECT b FROM T2) AS sub";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_UnionInInSubquery_RoundTrips()
        {
            string source = "SELECT * FROM T WHERE x IN (SELECT a FROM T1 UNION SELECT b FROM T2)";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_UnionInCte_RoundTrips()
        {
            string source = "WITH CTE AS (SELECT 1 AS x UNION SELECT 2) SELECT * FROM CTE";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_MixedSetOperations_RoundTrips()
        {
            string source = "SELECT a FROM T1 UNION ALL SELECT b FROM T2 INTERSECT SELECT c FROM T3 EXCEPT SELECT d FROM T4";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_SimpleSelect_StillReturnsSelectExpression()
        {
            var stmt = ParseSelect("SELECT a FROM T ORDER BY a");
            Assert.IsType<SelectExpression>(stmt.Query);
            Assert.Equal(1, stmt.Query.OrderBy.Items.Count);
        }

        #endregion

        #region FOR Clause Tests

        // FOR XML round-trip tests
        [Theory]
        [InlineData("SELECT a FROM T FOR XML RAW")]
        [InlineData("SELECT a FROM T FOR XML AUTO")]
        [InlineData("SELECT a FROM T FOR XML EXPLICIT")]
        [InlineData("SELECT a FROM T FOR XML PATH")]
        [InlineData("SELECT a FROM T FOR XML RAW('Employee')")]
        [InlineData("SELECT a FROM T FOR XML PATH('row')")]
        [InlineData("SELECT a FROM T FOR XML RAW, ROOT")]
        [InlineData("SELECT a FROM T FOR XML RAW, ROOT('data')")]
        [InlineData("SELECT a FROM T FOR XML RAW, ELEMENTS")]
        [InlineData("SELECT a FROM T FOR XML RAW, ELEMENTS XSINIL")]
        [InlineData("SELECT a FROM T FOR XML RAW, ELEMENTS ABSENT")]
        [InlineData("SELECT a FROM T FOR XML RAW, TYPE")]
        [InlineData("SELECT a FROM T FOR XML RAW, BINARY BASE64")]
        [InlineData("SELECT a FROM T FOR XML RAW, XMLDATA")]
        [InlineData("SELECT a FROM T FOR XML AUTO, XMLSCHEMA")]
        [InlineData("SELECT a FROM T FOR XML AUTO, XMLSCHEMA('http://example.com')")]
        [InlineData("SELECT a FROM T FOR XML RAW('row'), ROOT('data'), ELEMENTS XSINIL, BINARY BASE64, TYPE")]
        [InlineData("SELECT a FROM T FOR XML PATH('emp'), ROOT('employees'), ELEMENTS, TYPE")]
        [InlineData("SELECT a FROM T FOR XML AUTO, TYPE, XMLSCHEMA, ELEMENTS XSINIL")]
        [InlineData("SELECT a FROM T FOR XML EXPLICIT, XMLDATA")]
        [InlineData("SELECT a FROM T FOR XML EXPLICIT, BINARY BASE64")]
        public void Parse_ForXml(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        // FOR JSON round-trip tests
        [Theory]
        [InlineData("SELECT a FROM T FOR JSON AUTO")]
        [InlineData("SELECT a FROM T FOR JSON PATH")]
        [InlineData("SELECT a FROM T FOR JSON PATH, ROOT")]
        [InlineData("SELECT a FROM T FOR JSON PATH, ROOT('result')")]
        [InlineData("SELECT a FROM T FOR JSON AUTO, INCLUDE_NULL_VALUES")]
        [InlineData("SELECT a FROM T FOR JSON PATH, WITHOUT_ARRAY_WRAPPER")]
        [InlineData("SELECT a FROM T FOR JSON PATH, ROOT('data'), INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER")]
        public void Parse_ForJson(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        // FOR BROWSE round-trip
        [Fact]
        public void Parse_ForBrowse()
        {
            string source = "SELECT a FROM T FOR BROWSE";
            Assert.Equal(source, RoundTrip(source));
        }

        // FOR with ORDER BY
        [Theory]
        [InlineData("SELECT a FROM T ORDER BY a FOR XML RAW")]
        [InlineData("SELECT a FROM T ORDER BY a FOR JSON PATH")]
        public void Parse_ForWithOrderBy(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        // FOR with OPTION
        [Fact]
        public void Parse_ForXml_WithOption()
        {
            string source = "SELECT a FROM T FOR XML RAW OPTION (RECOMPILE)";
            Assert.Equal(source, RoundTrip(source));
        }

        // FOR with UNION
        [Fact]
        public void Parse_ForXml_WithUnion()
        {
            string source = "SELECT a FROM T1 UNION ALL SELECT b FROM T2 FOR XML RAW";
            Assert.Equal(source, RoundTrip(source));
        }

        // FOR in subquery
        [Fact]
        public void Parse_ForXml_InSubquery()
        {
            string source = "SELECT (SELECT a FROM T FOR XML PATH(''), TYPE) AS XmlCol";
            Assert.Equal(source, RoundTrip(source));
        }

        // Structure tests
        [Fact]
        public void Parse_ForXml_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T FOR XML RAW('row'), ROOT('data'), ELEMENTS XSINIL, TYPE");
            var forXml = Assert.IsType<ForXmlClause>(stmt.Query.For);

            Assert.Equal(ForXmlMode.Raw, forXml.Mode);
            Assert.Equal(3, forXml.Directives.Count);
            Assert.Equal(ForDirectiveType.Root, forXml.Directives[0].DirectiveType);
            Assert.Equal(ForDirectiveType.ElementsXsiNil, forXml.Directives[1].DirectiveType);
            Assert.Equal(ForDirectiveType.Type, forXml.Directives[2].DirectiveType);
        }

        [Fact]
        public void Parse_ForJson_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T FOR JSON PATH, ROOT('result'), INCLUDE_NULL_VALUES");
            var forJson = Assert.IsType<ForJsonClause>(stmt.Query.For);

            Assert.Equal(ForJsonMode.Path, forJson.Mode);
            Assert.Equal(2, forJson.Directives.Count);
            Assert.Equal(ForDirectiveType.Root, forJson.Directives[0].DirectiveType);
            Assert.Equal(ForDirectiveType.IncludeNullValues, forJson.Directives[1].DirectiveType);
        }

        [Fact]
        public void Parse_ForBrowse_Structure()
        {
            var stmt = ParseSelect("SELECT a FROM T FOR BROWSE");
            Assert.IsType<ForBrowseClause>(stmt.Query.For);
        }

        [Fact]
        public void Parse_NoForClause_IsNull()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            Assert.Null(stmt.Query.For);
        }

        // Contextual keyword as identifier tests
        [Theory]
        [InlineData("SELECT XML FROM T")]
        [InlineData("SELECT JSON FROM T")]
        [InlineData("SELECT RAW FROM T")]
        [InlineData("SELECT AUTO FROM T")]
        [InlineData("SELECT PATH FROM T")]
        [InlineData("SELECT ROOT FROM T")]
        [InlineData("SELECT ELEMENTS FROM T")]
        [InlineData("SELECT TYPE FROM T")]
        public void Parse_ForKeywords_AsIdentifiers(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region Programmatic AST Construction Tests

        [Fact]
        public void Construct_SelectStarFromTable_ProducesValidSql()
        {
            var selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new Expr.Wildcard());

            var from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("Users"))));
            selectExpr.From = from;

            var stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT * FROM Users", stmt.ToSource());
        }

        [Fact]
        public void Construct_SelectMultipleColumns_ProducesValidSql()
        {
            var selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("a")), null));
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("b")), null));

            var from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("T"))));
            selectExpr.From = from;

            var stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a, b FROM T", stmt.ToSource());
        }

        [Fact]
        public void Construct_SelectWithAlias_ProducesValidSql()
        {
            var selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("a")),
                new SuffixAlias("alias")));

            var from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("T"))));
            selectExpr.From = from;

            var stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a AS alias FROM T", stmt.ToSource());
        }

        [Fact]
        public void Construct_BinaryExpression_ProducesValidSql()
        {
            var binary = new Expr.Binary(
                new Expr.ColumnIdentifier(new ColumnName("a")),
                Expr.ArithmeticOperator.Add,
                new Expr.ColumnIdentifier(new ColumnName("b")));

            var selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(binary, null));

            var stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a + b", stmt.ToSource());
        }

        [Fact]
        public void Construct_ComparisonPredicate_ProducesValidSql()
        {
            var comparison = new AST.Predicate.Comparison(
                new Expr.ColumnIdentifier(new ColumnName("x")),
                AST.ComparisonOperator.GreaterThan,
                new Expr.IntLiteral(10));

            var selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("a")), null));

            var from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("T"))));
            selectExpr.From = from;
            selectExpr.AddWhere(comparison);

            var stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a FROM T WHERE x > 10", stmt.ToSource());
        }

        #endregion
    }
}
