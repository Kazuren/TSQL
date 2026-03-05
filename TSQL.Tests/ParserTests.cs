using TSQL.AST;

namespace TSQL.Tests
{
    public class ParserTests
    {
        #region Helper Methods

        private static SelectExpression SelectExpressionOf(Stmt.Select stmt)
        {
            return Assert.IsType<SelectExpression>(stmt.Query);
        }

        private static string RoundTrip(string source)
        {
            return Stmt.ParseSelect(source).ToSource();
        }
        #endregion

        [Fact]
        public void ParsePrefixAlias_SimpleColumn_ParsesCorrectly()
        {
            // Act
            Stmt.Select select = Stmt.ParseSelect("SELECT bAlias = b FROM T");

            // Assert
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

        #region AST Structure Tests - Select Columns

        [Fact]
        public void Parse_SingleColumn_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.ColumnIdentifier columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("a", columnId.ColumnName.Name);
            Assert.Null(item.Alias);
        }

        [Fact]
        public void Parse_Variable_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT @P0 FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.Variable columnVariable = Assert.IsType<Expr.Variable>(item.Expression);
            Assert.Equal("@P0", columnVariable.Name);
            Assert.Null(item.Alias);
        }

        [Fact]
        public void Parse_MultipleColumns_HasCorrectCount()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a, b, c FROM T");

            // Assert
            Assert.Equal(3, SelectExpressionOf(select).Columns.Count);
            Assert.All(SelectExpressionOf(select).Columns, item => Assert.IsType<SelectColumn>(item));
        }

        [Fact]
        public void Parse_Wildcard_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT * FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            Assert.IsType<Expr.Wildcard>(SelectExpressionOf(select).Columns[0]);
        }

        [Fact]
        public void Parse_QualifiedWildcard_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT t.* FROM T");

            // Assert
            Assert.Single(SelectExpressionOf(select).Columns);
            Expr.QualifiedWildcard wildcard = Assert.IsType<Expr.QualifiedWildcard>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(wildcard.ObjectName);
            Assert.Equal("t", wildcard.ObjectName.Name);
        }

        [Fact]
        public void Parse_FullyQualifiedColumn_HasAllParts()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT d.s.o.c FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.ColumnIdentifier columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);

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
            Stmt.Select select = Stmt.ParseSelect("SELECT a AS alias FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("alias", item.Alias.Name);
        }

        [Fact]
        public void Parse_SuffixAliasWithoutAs_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a alias FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Assert.NotNull(item.Alias);
            Assert.Equal("alias", item.Alias.Name);
        }

        [Fact]
        public void Parse_PrefixAlias_HasCorrectAliasType()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT alias = a FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
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
            Stmt.Select select = Stmt.ParseSelect("SELECT a + b FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(item.Expression);

            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);
            Assert.Equal(Expr.ArithmeticOperator.Add, binary.Operator);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_BinaryMultiplication_HasHigherPrecedenceThanAddition()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a + b * c FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(item.Expression);

            // a + (b * c) - multiplication should be on the right
            Assert.Equal(Expr.ArithmeticOperator.Add, binary.Operator);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);

            Expr.Binary rightBinary = Assert.IsType<Expr.Binary>(binary.Right);
            Assert.Equal(Expr.ArithmeticOperator.Multiply, rightBinary.Operator);
        }

        [Fact]
        public void Parse_UnaryMinus_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT -a FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.Unary unary = Assert.IsType<Expr.Unary>(item.Expression);
            Assert.Equal(Expr.UnaryOperator.Negate, unary.Operator);
            Assert.IsType<Expr.ColumnIdentifier>(unary.Right);
        }

        [Fact]
        public void Parse_GroupedExpression_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT (a + b) * c FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(item.Expression);

            // (a + b) * c - grouped addition should be on the left
            Assert.Equal(Expr.ArithmeticOperator.Multiply, binary.Operator);

            Expr.Grouping grouping = Assert.IsType<Expr.Grouping>(binary.Left);
            Expr.Binary innerBinary = Assert.IsType<Expr.Binary>(grouping.Expression);
            Assert.Equal(Expr.ArithmeticOperator.Add, innerBinary.Operator);
        }

        [Fact]
        public void Parse_LiteralNumber_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT 42 FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.IntLiteral literal = Assert.IsType<Expr.IntLiteral>(item.Expression);
            Assert.Equal(42, literal.Value);
        }

        [Fact]
        public void Parse_LiteralString_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT 'hello' FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.StringLiteral literal = Assert.IsType<Expr.StringLiteral>(item.Expression);
            Assert.Contains("hello", literal.Value);
        }

        #endregion

        #region AST Structure Tests - Function Calls

        [Fact]
        public void Parse_FunctionCallNoArgs_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT GETDATE() FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.FunctionCall func = Assert.IsType<Expr.FunctionCall>(item.Expression);
            Assert.Equal("GETDATE", func.Callee.ObjectName.Name);
            Assert.Empty(func.Arguments);
        }

        [Fact]
        public void Parse_FunctionCallWithArgs_HasCorrectArgumentCount()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT COALESCE(a, b, c) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.FunctionCall func = Assert.IsType<Expr.FunctionCall>(item.Expression);
            Assert.Equal("COALESCE", func.Callee.ObjectName.Name);
            Assert.Equal(3, func.Arguments.Count);
        }

        #endregion

        #region AST Structure Tests - FROM Clause

        [Fact]
        public void Parse_SimpleFrom_HasTableSource()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM MyTable");

            // Assert
            Assert.NotNull(SelectExpressionOf(select).From);
            Assert.Equal(1, SelectExpressionOf(select).From.TableSources.Count);
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromWithAlias_HasAliasSet()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM MyTable AS t");

            // Assert
            Assert.NotNull(SelectExpressionOf(select).From);
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Alias);
            Assert.Equal("t", tableRef.Alias.Name);
        }

        [Fact]
        public void Parse_FromQualifiedName_TwoPart()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM dbo.MyTable");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromQualifiedName_ThreePart()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM MyDb.dbo.MyTable");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("MyDb", tableRef.TableName.DatabaseName.Name);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromQualifiedName_FourPart()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM Server1.MyDb.dbo.MyTable");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("Server1", tableRef.TableName.ServerName.Name);
            Assert.Equal("MyDb", tableRef.TableName.DatabaseName.Name);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromCommaSeparated_HasMultipleSources()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1, T2, T3");

            Assert.Equal(3, SelectExpressionOf(select).From.TableSources.Count);
            TableReference t1 = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            TableReference t2 = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[1]);
            TableReference t3 = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[2]);
            Assert.Equal("T1", t1.TableName.ObjectName.Name);
            Assert.Equal("T2", t2.TableName.ObjectName.Name);
            Assert.Equal("T3", t3.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_FromSubquery_HasSubqueryReference()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM (SELECT b FROM T) AS sub");

            SubqueryReference subRef = Assert.IsType<SubqueryReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(subRef.Subquery);
            Assert.NotNull(subRef.Alias);
            Assert.Equal("sub", subRef.Alias.Name);
        }

        [Fact]
        public void Parse_FromTableVariable_HasVariableReference()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM @TempTable");

            TableVariableReference varRef = Assert.IsType<TableVariableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("@TempTable", varRef.VariableName);
        }

        [Fact]
        public void Parse_FromTableVariableWithAlias_HasAliasSet()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM @TempTable AS t");

            TableVariableReference varRef = Assert.IsType<TableVariableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("@TempTable", varRef.VariableName);
            Assert.NotNull(varRef.Alias);
            Assert.Equal("t", varRef.Alias.Name);
        }

        [Fact]
        public void Parse_FromAliasWithoutAs_HasAliasSet()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM MyTable t");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Alias);
            Assert.Equal("t", tableRef.Alias.Name);
        }

        [Fact]
        public void Parse_InnerJoin_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.Inner, join.JoinType);
            TableReference left = Assert.IsType<TableReference>(join.Left);
            TableReference right = Assert.IsType<TableReference>(join.Right);
            Assert.Equal("T1", left.TableName.ObjectName.Name);
            Assert.Equal("T2", right.TableName.ObjectName.Name);
            Assert.NotNull(join.OnCondition);
        }

        [Fact]
        public void Parse_LeftJoin_HasCorrectJoinType()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 LEFT JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.LeftOuter, join.JoinType);
        }

        [Fact]
        public void Parse_LeftOuterJoin_HasCorrectJoinType()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 LEFT OUTER JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.LeftOuter, join.JoinType);
        }

        [Fact]
        public void Parse_RightJoin_HasCorrectJoinType()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 RIGHT JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.RightOuter, join.JoinType);
        }

        [Fact]
        public void Parse_FullOuterJoin_HasCorrectJoinType()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 FULL OUTER JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.FullOuter, join.JoinType);
        }

        [Fact]
        public void Parse_BareJoin_DefaultsToInner()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinType.Inner, join.JoinType);
        }

        [Fact]
        public void Parse_CrossJoin_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 CROSS JOIN T2");

            CrossJoin join = Assert.IsType<CrossJoin>(SelectExpressionOf(select).From.TableSources[0]);
            TableReference left = Assert.IsType<TableReference>(join.Left);
            TableReference right = Assert.IsType<TableReference>(join.Right);
            Assert.Equal("T1", left.TableName.ObjectName.Name);
            Assert.Equal("T2", right.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_CrossApply_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 CROSS APPLY (SELECT b FROM T2) AS sub");

            ApplyJoin join = Assert.IsType<ApplyJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(ApplyType.Cross, join.ApplyType);
            TableReference left = Assert.IsType<TableReference>(join.Left);
            Assert.Equal("T1", left.TableName.ObjectName.Name);
            SubqueryReference right = Assert.IsType<SubqueryReference>(join.Right);
            Assert.NotNull(right.Alias);
        }

        [Fact]
        public void Parse_OuterApply_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 OUTER APPLY (SELECT b FROM T2) AS sub");

            ApplyJoin join = Assert.IsType<ApplyJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(ApplyType.Outer, join.ApplyType);
        }

        [Fact]
        public void Parse_MultiJoinChain_IsLeftAssociative()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id INNER JOIN T3 ON T2.id = T3.id");

            // Should be: (T1 JOIN T2) JOIN T3
            QualifiedJoin outerJoin = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            QualifiedJoin innerJoin = Assert.IsType<QualifiedJoin>(outerJoin.Left);
            TableReference t3 = Assert.IsType<TableReference>(outerJoin.Right);
            TableReference t1 = Assert.IsType<TableReference>(innerJoin.Left);
            TableReference t2 = Assert.IsType<TableReference>(innerJoin.Right);
            Assert.Equal("T1", t1.TableName.ObjectName.Name);
            Assert.Equal("T2", t2.TableName.ObjectName.Name);
            Assert.Equal("T3", t3.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_LoopJoinWithoutType_TreatsLoopAsAlias()
        {
            // Without an explicit join type, LOOP is treated as an alias
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 LOOP JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            TableReference left = Assert.IsType<TableReference>(join.Left);
            Assert.Equal("LOOP", left.Alias.Name);
            Assert.Null(join.JoinHint);
            Assert.Equal(JoinType.Inner, join.JoinType);
        }

        [Fact]
        public void Parse_JoinHint_InnerHashJoin()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 INNER HASH JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinHint.Hash, join.JoinHint);
            Assert.Equal(JoinType.Inner, join.JoinType);
        }

        [Fact]
        public void Parse_JoinHint_LeftMergeJoin()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 LEFT MERGE JOIN T2 ON T1.id = T2.id");

            QualifiedJoin join = Assert.IsType<QualifiedJoin>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(JoinHint.Merge, join.JoinHint);
            Assert.Equal(JoinType.LeftOuter, join.JoinType);
        }

        [Fact]
        public void Parse_ForSystemTimeAsOf_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T FOR SYSTEM_TIME AS OF '2020-01-01'");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.ForSystemTime);
            Assert.Equal(SystemTimeType.AsOf, tableRef.ForSystemTime.TimeType);
        }

        [Fact]
        public void Parse_ForSystemTimeAll_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T FOR SYSTEM_TIME ALL");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.ForSystemTime);
            Assert.Equal(SystemTimeType.All, tableRef.ForSystemTime.TimeType);
        }

        [Fact]
        public void Parse_ForSystemTimeBetweenAnd_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T FOR SYSTEM_TIME BETWEEN '2020-01-01' AND '2021-01-01'");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.ForSystemTime);
            Assert.Equal(SystemTimeType.BetweenAnd, tableRef.ForSystemTime.TimeType);
        }

        [Fact]
        public void Parse_TablesamplePercent_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T AS t TABLESAMPLE (10 PERCENT)");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Tablesample);
            Assert.Equal(TableSampleUnit.Percent, tableRef.Tablesample.Unit);
        }

        [Fact]
        public void Parse_TablesampleWithRepeatable_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T TABLESAMPLE SYSTEM (100 ROWS) REPEATABLE (42)");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.Tablesample);
            Assert.Equal(TableSampleUnit.Rows, tableRef.Tablesample.Unit);
            Assert.NotNull(tableRef.Tablesample.RepeatSeed);
        }

        [Fact]
        public void Parse_TableHintNolock_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WITH (NOLOCK)");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.TableHints);
            Assert.Equal(1, tableRef.TableHints.Hints.Count);
            Assert.Equal(TableHintType.NoLock, tableRef.TableHints.Hints[0].HintType);
        }

        [Fact]
        public void Parse_TableHintMultiple_HasCorrectCount()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WITH (NOLOCK, NOWAIT)");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.NotNull(tableRef.TableHints);
            Assert.Equal(2, tableRef.TableHints.Hints.Count);
            Assert.Equal(TableHintType.NoLock, tableRef.TableHints.Hints[0].HintType);
            Assert.Equal(TableHintType.NoWait, tableRef.TableHints.Hints[1].HintType);
        }

        [Fact]
        public void Parse_TableHintIndex_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WITH (INDEX(1))");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(TableHintType.Index, tableRef.TableHints.Hints[0].HintType);
            Assert.Equal(1, tableRef.TableHints.Hints[0].IndexValues.Count);
        }

        [Fact]
        public void Parse_TableHintHoldLock_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WITH (HOLDLOCK)");

            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal(TableHintType.HoldLock, tableRef.TableHints.Hints[0].HintType);
        }

        [Fact]
        public void Parse_ParenthesizedJoin_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM (T1 INNER JOIN T2 ON T1.id = T2.id)");

            ParenthesizedTableSource paren = Assert.IsType<ParenthesizedTableSource>(SelectExpressionOf(select).From.TableSources[0]);
            QualifiedJoin join = Assert.IsType<QualifiedJoin>(paren.Inner);
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
        [InlineData("SELECT LEFT(Col, 5) FROM T")]
        [InlineData("SELECT RIGHT(Col, 5) FROM T")]
        [InlineData("SELECT RIGHT(FILENAME, CHARINDEX('.', REVERSE(FILENAME)) - 1) FROM T")]
        [InlineData("SELECT LEFT(Name, 3) AS Prefix, RIGHT(Name, 3) AS Suffix FROM T")]
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
            Stmt.Select select = Stmt.ParseSelect("SELECT ROW_NUMBER() OVER (ORDER BY id) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("ROW_NUMBER", windowFunc.Function.Callee.ObjectName.Name);
            Assert.NotNull(windowFunc.Over);
            Assert.NotNull(windowFunc.Over.OrderBy);
            Assert.Single(windowFunc.Over.OrderBy);
        }

        [Fact]
        public void Parse_RankWithPartition_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT RANK() OVER (PARTITION BY dept ORDER BY salary DESC) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("RANK", windowFunc.Function.Callee.ObjectName.Name);
            Assert.NotNull(windowFunc.Over.PartitionBy);
            Assert.Single(windowFunc.Over.PartitionBy);
            Assert.NotNull(windowFunc.Over.OrderBy);
            Assert.Single(windowFunc.Over.OrderBy);
            Assert.Equal(SortDirection.Descending, windowFunc.Over.OrderBy[0].Direction);
        }

        [Fact]
        public void Parse_DenseRank_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT DENSE_RANK() OVER (ORDER BY score) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("DENSE_RANK", windowFunc.Function.Callee.ObjectName.Name);
        }

        [Fact]
        public void Parse_Ntile_HasCorrectArgument()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT NTILE(4) OVER (ORDER BY val) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("NTILE", windowFunc.Function.Callee.ObjectName.Name);
            Assert.Single(windowFunc.Function.Arguments);
            Expr.IntLiteral arg = Assert.IsType<Expr.IntLiteral>(windowFunc.Function.Arguments[0]);
            Assert.Equal(4, arg.Value);
        }

        // === Aggregates with OVER ===

        [Fact]
        public void Parse_SumWithOver_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT SUM(amount) OVER (PARTITION BY customer_id) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal("SUM", windowFunc.Function.Callee.ObjectName.Name);
            Assert.NotNull(windowFunc.Over.PartitionBy);
            Assert.Null(windowFunc.Over.OrderBy);
        }

        [Fact]
        public void Parse_AggregateWithEmptyOver_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT AVG(price) OVER () FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
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
            Stmt.Select select = Stmt.ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS UNBOUNDED PRECEDING) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameType.Rows, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.UnboundedPreceding, windowFunc.Over.Frame.Start.BoundType);
            Assert.Null(windowFunc.Over.Frame.End); // Short syntax
        }

        [Fact]
        public void Parse_RowsNPreceding_HasCorrectFrame()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS 3 PRECEDING) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameType.Rows, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            Expr.IntLiteral offset = Assert.IsType<Expr.IntLiteral>(windowFunc.Over.Frame.Start.Offset);
            Assert.Equal(3, offset.Value);
        }

        [Fact]
        public void Parse_RowsBetween_HasCorrectFrame()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.NotNull(windowFunc.Over.Frame);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            Expr.IntLiteral offset = Assert.IsType<Expr.IntLiteral>(windowFunc.Over.Frame.Start.Offset);
            Assert.Equal(2, offset.Value);
            Assert.Equal(WindowFrameBoundType.CurrentRow, windowFunc.Over.Frame.End.BoundType);
        }

        [Fact]
        public void Parse_RangeBetween_HasCorrectFrame()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT SUM(x) OVER (ORDER BY y RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal(WindowFrameType.Range, windowFunc.Over.Frame.FrameType);
            Assert.Equal(WindowFrameBoundType.CurrentRow, windowFunc.Over.Frame.Start.BoundType);
            Assert.Equal(WindowFrameBoundType.UnboundedFollowing, windowFunc.Over.Frame.End.BoundType);
        }

        [Fact]
        public void Parse_RowsBetweenNPrecedingAndNFollowing_HasCorrectFrame()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND 2 FOLLOWING) FROM T");

            // Assert
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.WindowFunction windowFunc = Assert.IsType<Expr.WindowFunction>(item.Expression);
            Assert.Equal(WindowFrameBoundType.Preceding, windowFunc.Over.Frame.Start.BoundType);
            Assert.Equal(WindowFrameBoundType.Following, windowFunc.Over.Frame.End.BoundType);
        }


        // === Error Cases ===

        [Fact]
        public void Parse_RowNumberWithoutOver_ThrowsParseError()
        {
            Assert.ThrowsAny<ParseError>(() => Stmt.Parse("SELECT ROW_NUMBER() FROM T"));
        }

        [Fact]
        public void Parse_FrameWithoutOrderBy_ThrowsParseError()
        {
            Assert.ThrowsAny<ParseError>(() => Stmt.Parse("SELECT SUM(x) OVER (ROWS UNBOUNDED PRECEDING) FROM T"));
        }

        [Fact]
        public void Parse_Error_PopulatesStructuredProperties()
        {
            string sql = "SELECT FROM";
            ParseError ex = Assert.Throws<ParseError>(() => Stmt.Parse(sql));

            Assert.NotNull(ex.Line);
            Assert.Equal(7, ex.Column);
            Assert.Equal(sql, ex.SqlText);
        }

        [Fact]
        public void Parse_Error_MultiLine_ReportsCorrectColumn()
        {
            string sql = "SELECT\n  FROM";
            ParseError ex = Assert.Throws<ParseError>(() => Stmt.Parse(sql));

            Assert.Equal(2, ex.Line);
            Assert.Equal(2, ex.Column);
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
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a = 1");

            // Assert
            Assert.NotNull(SelectExpressionOf(select).Where);
            Predicate.Comparison comparison = Assert.IsType<AST.Predicate.Comparison>(SelectExpressionOf(select).Where);
            Assert.IsType<Expr.ColumnIdentifier>(comparison.Left);
            Assert.Equal(AST.ComparisonOperator.Equal, comparison.Operator);
            Assert.IsType<Expr.IntLiteral>(comparison.Right);
        }

        [Fact]
        public void Parse_WhereIsNull_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a IS NULL");

            // Assert
            Predicate.Null nullPred = Assert.IsType<AST.Predicate.Null>(SelectExpressionOf(select).Where);
            Assert.Equal(AST.Negation.NotNegated, nullPred.Negated);
        }

        [Fact]
        public void Parse_WhereIsNotNull_HasCorrectStructure()
        {
            // Arrange & Act
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a IS NOT NULL");

            // Assert
            Predicate.Null nullPred = Assert.IsType<AST.Predicate.Null>(SelectExpressionOf(select).Where);
            Assert.Equal(AST.Negation.Negated, nullPred.Negated);
        }

        [Fact]
        public void Parse_WhereAndOr_HasCorrectPrecedence()
        {
            // a = 1 AND b = 2 OR c = 3 should be (a=1 AND b=2) OR c=3
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a = 1 AND b = 2 OR c = 3");

            Predicate.Or orPred = Assert.IsType<AST.Predicate.Or>(SelectExpressionOf(select).Where);
            Assert.IsType<AST.Predicate.And>(orPred.Left);
            Assert.IsType<AST.Predicate.Comparison>(orPred.Right);
        }

        [Fact]
        public void Parse_WhereNot_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE NOT a = 1");

            Predicate.Not notPred = Assert.IsType<AST.Predicate.Not>(SelectExpressionOf(select).Where);
            Assert.IsType<AST.Predicate.Comparison>(notPred.Predicate);
        }

        [Fact]
        public void Parse_WhereLike_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a LIKE '%test%'");

            Predicate.Like likePred = Assert.IsType<AST.Predicate.Like>(SelectExpressionOf(select).Where);
            Assert.Equal(AST.Negation.NotNegated, likePred.Negated);
            Assert.IsType<Expr.ColumnIdentifier>(likePred.Left);
            Assert.IsType<Expr.StringLiteral>(likePred.Pattern);
        }

        [Fact]
        public void Parse_WhereBetween_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a BETWEEN 1 AND 10");

            Predicate.Between between = Assert.IsType<AST.Predicate.Between>(SelectExpressionOf(select).Where);
            Assert.Equal(AST.Negation.NotNegated, between.Negated);
        }

        [Fact]
        public void Parse_WhereInList_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE a IN (1, 2, 3)");

            Predicate.In inPred = Assert.IsType<AST.Predicate.In>(SelectExpressionOf(select).Where);
            Assert.Equal(AST.Negation.NotNegated, inPred.Negated);
            Assert.NotNull(inPred.ValueList);
            Assert.Equal(3, inPred.ValueList.Count);
        }

        [Fact]
        public void Parse_WhereExists_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE EXISTS (SELECT 1 FROM T2)");

            Predicate.Exists exists = Assert.IsType<AST.Predicate.Exists>(SelectExpressionOf(select).Where);
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
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM (VALUES (1, 'x'), (2, 'y')) AS t(id, name)");
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
            Stmt stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- Derived column aliases on subquery ----

        [Fact]
        public void Parse_SubqueryDerivedColumnAliases_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM (SELECT 1, 2) AS t(col1, col2)");
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
            Stmt stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- PIVOT ----

        [Fact]
        public void Parse_PivotTableSource_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb, Mar)) AS pvt");
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
            Stmt stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- UNPIVOT ----

        [Fact]
        public void Parse_UnpivotTableSource_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T UNPIVOT (Value FOR Quarter IN (Q1, Q2, Q3, Q4)) AS unpvt");
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
            Stmt stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- Rowset functions ----

        [Fact]
        public void Parse_OpenQueryRowsetFunction_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM OPENQUERY(LinkedServer, 'SELECT 1') AS oq");
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
            Stmt stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        // ---- Table-valued functions ----

        [Fact]
        public void Parse_TableValuedFunction_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM MyFunction(1, 2) AS f");
            FromClause from = SelectExpressionOf(select).From;

            RowsetFunctionReference rowset = Assert.IsType<RowsetFunctionReference>(from.TableSources[0]);
            Assert.Equal("MyFunction", rowset.FunctionCall.Callee.ObjectName.Name);
            Assert.Equal(2, rowset.FunctionCall.Arguments.Count);
            Assert.NotNull(rowset.Alias);
            Assert.Equal("f", rowset.Alias.Name);
        }

        [Fact]
        public void Parse_SchemaQualifiedTvf_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM dbo.MyFunction(1, 2) AS f");
            FromClause from = SelectExpressionOf(select).From;

            RowsetFunctionReference rowset = Assert.IsType<RowsetFunctionReference>(from.TableSources[0]);
            Assert.Equal("dbo", rowset.FunctionCall.Callee.SchemaName.Name);
            Assert.Equal("MyFunction", rowset.FunctionCall.Callee.ObjectName.Name);
            Assert.Equal(2, rowset.FunctionCall.Arguments.Count);
            Assert.NotNull(rowset.Alias);
            Assert.Equal("f", rowset.Alias.Name);
        }

        [Fact]
        public void Parse_TvfWithoutAlias_HasCorrectStructure()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM MyFunction(1, 2)");
            FromClause from = SelectExpressionOf(select).From;

            RowsetFunctionReference rowset = Assert.IsType<RowsetFunctionReference>(from.TableSources[0]);
            Assert.Equal("MyFunction", rowset.FunctionCall.Callee.ObjectName.Name);
            Assert.Equal(2, rowset.FunctionCall.Arguments.Count);
            Assert.Null(rowset.Alias);
        }

        [Theory]
        [InlineData("SELECT a FROM MyFunction(1, 2) AS f")]
        [InlineData("SELECT a FROM dbo.MyFunction(1, 2) AS f")]
        [InlineData("SELECT a FROM MyFunction(1, 2)")]
        [InlineData("SELECT a FROM T INNER JOIN MyFunction(1) AS f ON T.id = f.id")]
        public void Parse_TableValuedFunction_RoundTripsCorrectly(string source)
        {
            Assert.Equal(source, RoundTrip(source));
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
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id WHERE T1.active = 1");
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
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T1 CROSS APPLY (SELECT b FROM T2 WHERE T2.id = T1.id) AS sub");
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
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T WHERE (a = 1)");

            Predicate.Grouping grouping = Assert.IsType<AST.Predicate.Grouping>(SelectExpressionOf(select).Where);
            Assert.IsType<AST.Predicate.Comparison>(grouping.Predicate);
        }

        [Fact]
        public void Parse_PivotInValues_AcceptsIdentifiers()
        {
            // PIVOT IN values must be identifiers (they become output column names)
            Stmt.Select select = Stmt.ParseSelect("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb)) AS pvt");
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
                Stmt.ParseSelect("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (1 + 2)) AS pvt"));
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT COUNT(*) FROM T");
            SelectColumn selectColumn = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall funcCall = Assert.IsType<Expr.FunctionCall>(selectColumn.Expression);
            Assert.Equal(1, funcCall.Arguments.Count);
            Assert.IsType<Expr.Wildcard>(funcCall.Arguments[0]);
        }

        #endregion

        #region Aggregate DISTINCT/ALL Tests

        [Theory]
        [InlineData("SELECT COUNT(DISTINCT a) FROM T")]
        [InlineData("SELECT SUM(DISTINCT amount) FROM T")]
        [InlineData("SELECT AVG(ALL price) FROM T")]
        [InlineData("SELECT COUNT(DISTINCT a), SUM(b) FROM T GROUP BY b")]
        [InlineData("SELECT COUNT(DISTINCT a) FROM T WHERE x > 1")]
        public void Parse_AggregateDistinctAll(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_CountDistinct_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT COUNT(DISTINCT a) FROM T");
            SelectColumn selectColumn = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall funcCall = Assert.IsType<Expr.FunctionCall>(selectColumn.Expression);
            Assert.Equal(SetQuantifier.Distinct, funcCall.Quantifier);
            Assert.Equal(1, funcCall.Arguments.Count);
            Expr.ColumnIdentifier col = Assert.IsType<Expr.ColumnIdentifier>(funcCall.Arguments[0]);
            Assert.Equal("a", col.ColumnName.Name);
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, SUM(b) FROM T GROUP BY a");
            GroupByClause groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            Assert.IsType<GroupByExpression>(groupBy.Items[0]);
        }

        [Fact]
        public void Parse_GroupBy_RollupStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY ROLLUP(a, b)");
            GroupByClause groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            GroupByRollup rollup = Assert.IsType<GroupByRollup>(groupBy.Items[0]);
            Assert.Equal(2, rollup.Items.Count);
        }

        [Fact]
        public void Parse_GroupBy_CubeStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY CUBE(a, b)");
            GroupByClause groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            GroupByCube cube = Assert.IsType<GroupByCube>(groupBy.Items[0]);
            Assert.Equal(2, cube.Items.Count);
        }

        [Fact]
        public void Parse_GroupBy_GroupingSetsStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS((a, b), a, ())");
            GroupByClause groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.NotNull(groupBy);
            Assert.Equal(1, groupBy.Items.Count);
            GroupByGroupingSets gs = Assert.IsType<GroupByGroupingSets>(groupBy.Items[0]);
            Assert.Equal(3, gs.Items.Count);
            Assert.IsType<GroupByComposite>(gs.Items[0]);
            Assert.IsType<GroupByExpression>(gs.Items[1]);
            Assert.IsType<GroupByGrandTotal>(gs.Items[2]);
        }

        [Fact]
        public void Parse_GroupBy_CompositeInRollup()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, c, SUM(d) FROM T GROUP BY ROLLUP((a, b), c)");
            GroupByRollup rollup = Assert.IsType<GroupByRollup>(SelectExpressionOf(stmt).GroupBy.Items[0]);

            Assert.Equal(2, rollup.Items.Count);
            GroupByComposite composite = Assert.IsType<GroupByComposite>(rollup.Items[0]);
            Assert.Equal(2, composite.Expressions.Count);
            Assert.IsType<GroupByExpression>(rollup.Items[1]);
        }

        [Fact]
        public void Parse_GroupBy_MixedItems()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b, SUM(c) FROM T GROUP BY a, ROLLUP(b)");
            GroupByClause groupBy = SelectExpressionOf(stmt).GroupBy;

            Assert.Equal(2, groupBy.Items.Count);
            Assert.IsType<GroupByExpression>(groupBy.Items[0]);
            Assert.IsType<GroupByRollup>(groupBy.Items[1]);
        }

        [Fact]
        public void Parse_GroupBy_NoGroupBy_ReturnsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, SUM(b) FROM T GROUP BY a HAVING SUM(b) > 10");
            Assert.NotNull(SelectExpressionOf(stmt).GroupBy);
            Assert.NotNull(SelectExpressionOf(stmt).Having);
        }

        [Fact]
        public void Parse_Having_NoHaving_ReturnsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, SUM(b) FROM T GROUP BY a");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a, b FROM T ORDER BY a ASC, b DESC");
            OrderByClause orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.Equal(2, orderBy.Items.Count);
            Assert.Equal(SortDirection.Ascending, orderBy.Items[0].Direction);
            Assert.Equal(SortDirection.Descending, orderBy.Items[1].Direction);
        }

        [Fact]
        public void Parse_OrderBy_NoOrderBy_IsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T ORDER BY a OFFSET 10 ROWS");
            OrderByClause orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.Equal(1, orderBy.Items.Count);
            Assert.IsType<Expr.IntLiteral>(orderBy.OffsetCount);
            Assert.Null(orderBy.FetchCount);
        }

        [Fact]
        public void Parse_OffsetFetch_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T ORDER BY a OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY");
            OrderByClause orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.IsType<Expr.IntLiteral>(orderBy.OffsetCount);
            Assert.IsType<Expr.IntLiteral>(orderBy.FetchCount);
        }

        [Fact]
        public void Parse_OffsetFetch_NoOffset_IsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T ORDER BY a");
            OrderByClause orderBy = stmt.Query.OrderBy;

            Assert.NotNull(orderBy);
            Assert.Null(orderBy.OffsetCount);
            Assert.Null(orderBy.FetchCount);
        }

        [Fact]
        public void Parse_OffsetFetch_WithSetOperation()
        {
            string source = "SELECT a FROM T1 UNION ALL SELECT b FROM T2 ORDER BY a OFFSET 5 ROWS FETCH NEXT 10 ROWS ONLY";
            Assert.Equal(source, RoundTrip(source));

            Stmt.Select stmt = Stmt.ParseSelect(source);
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
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
            Stmt.Select stmt = Stmt.ParseSelect("WITH cte AS (SELECT a FROM T) SELECT a FROM cte");
            Assert.NotNull(stmt.CteStmt);
            Assert.Equal(1, stmt.CteStmt.Ctes.Count);
            Assert.Equal("cte", stmt.CteStmt.Ctes[0].Name);
            Assert.Null(stmt.CteStmt.Ctes[0].ColumnNames);
        }

        [Fact]
        public void Parse_Cte_WithColumnNames()
        {
            Stmt.Select stmt = Stmt.ParseSelect("WITH cte(x, y) AS (SELECT a, b FROM T) SELECT x, y FROM cte");
            CteDefinition def = stmt.CteStmt.Ctes[0];
            Assert.NotNull(def.ColumnNames);
            Assert.Equal(2, def.ColumnNames.ColumnNames.Count);
        }

        [Fact]
        public void Parse_Cte_MultipleCtes()
        {
            Stmt.Select stmt = Stmt.ParseSelect("WITH c1 AS (SELECT a FROM T), c2 AS (SELECT b FROM U) SELECT a, b FROM c1, c2");
            Assert.Equal(2, stmt.CteStmt.Ctes.Count);
            Assert.Equal("c1", stmt.CteStmt.Ctes[0].Name);
            Assert.Equal("c2", stmt.CteStmt.Ctes[1].Name);
        }

        [Fact]
        public void Parse_NoCte_ReturnsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT NULL");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT 10 % 3");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT CASE x WHEN 1 THEN 'a' WHEN 2 THEN 'b' ELSE 'z' END");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.SimpleCase sc = Assert.IsType<Expr.SimpleCase>(col.Expression);

            Assert.IsType<Expr.ColumnIdentifier>(sc.Operand);
            Assert.Equal(2, sc.WhenClauses.Count);
            Assert.NotNull(sc.ElseResult);
        }

        [Fact]
        public void Parse_SearchedCase_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT CASE WHEN x > 1 THEN 'a' WHEN x > 2 THEN 'b' ELSE 'z' END");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.SearchedCase sc = Assert.IsType<Expr.SearchedCase>(col.Expression);

            Assert.Equal(2, sc.WhenClauses.Count);
            Assert.NotNull(sc.ElseResult);
            Assert.IsType<AST.Predicate.Comparison>(sc.WhenClauses[0].Condition);
        }

        [Fact]
        public void Parse_CaseExpression_NoElse()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT CASE x WHEN 1 THEN 'a' END");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT CAST(x AS VARCHAR(50))");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT TRY_CAST(x AS INT)");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT CONVERT(VARCHAR(10), x, 121)");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT CONVERT(INT, x)");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.ConvertExpression conv = Assert.IsType<Expr.ConvertExpression>(col.Expression);
            Assert.Null(conv.Style);
        }

        [Fact]
        public void Parse_TryConvert_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT TRY_CONVERT(DATE, x, 103)");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T OPTION (RECOMPILE)");
            Assert.NotNull(stmt.Option);
            Assert.Equal(1, stmt.Option.Hints.Count);
            Assert.Equal(QueryHintType.Recompile, stmt.Option.Hints[0].HintType);
        }

        [Fact]
        public void Parse_OptionMultipleHints_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T OPTION (RECOMPILE, MAXDOP 1)");
            Assert.NotNull(stmt.Option);
            Assert.Equal(2, stmt.Option.Hints.Count);
            Assert.Equal(QueryHintType.Recompile, stmt.Option.Hints[0].HintType);
            Assert.Equal(QueryHintType.Maxdop, stmt.Option.Hints[1].HintType);
        }

        [Fact]
        public void Parse_OptionFast_HasValue()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T OPTION (FAST 10)");
            ValueQueryHint hint = Assert.IsType<ValueQueryHint>(stmt.Option.Hints[0]);
            Assert.Equal(QueryHintType.Fast, hint.HintType);
            Assert.NotNull(hint.Value);
        }

        [Fact]
        public void Parse_OptionOptimizeFor_HasVariables()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T OPTION (OPTIMIZE FOR (@x = 1, @y UNKNOWN))");
            OptimizeForQueryHint hint = Assert.IsType<OptimizeForQueryHint>(stmt.Option.Hints[0]);
            Assert.Equal(QueryHintType.OptimizeFor, hint.HintType);
            Assert.Equal(2, hint.OptimizeForVariables.Count);
        }

        [Fact]
        public void Parse_OptionUseHint_HasHintNames()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T OPTION (USE HINT ('ENABLE_HIST_AMENDMENT_FOR_ASC_KEYS'))");
            UseHintQueryHint hint = Assert.IsType<UseHintQueryHint>(stmt.Option.Hints[0]);
            Assert.Equal(QueryHintType.UseHint, hint.HintType);
            Assert.Equal(1, hint.UseHintNames.Count);
        }

        [Fact]
        public void Parse_OptionTableHint_ReusesTableHintParsing()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T OPTION (TABLE HINT (T, NOLOCK))");
            TableHintQueryHint hint = Assert.IsType<TableHintQueryHint>(stmt.Option.Hints[0]);
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT N'hello'");
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
            Assert.Throws<ParseError>(() => Stmt.ParseSelect(source));
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT NULLIF(a, b) FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall call = Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.Equal("NULLIF", call.Callee.ObjectName.Name);
            Assert.Equal(2, call.Arguments.Count);
        }

        #endregion

        #region LEFT / RIGHT Function Tests

        [Theory]
        [InlineData("SELECT RIGHT(Col, 5) FROM T", "RIGHT")]
        [InlineData("SELECT LEFT(Col, 5) FROM T", "LEFT")]
        public void Parse_LeftRight_IsFunctionCall(string source, string expectedName)
        {
            Stmt.Select stmt = Stmt.ParseSelect(source);
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall call = Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.Equal(expectedName, call.Callee.ObjectName.Name);
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT OPENXML(@idoc, '/root/row') FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.OpenXmlExpression openXml = Assert.IsType<Expr.OpenXmlExpression>(col.Expression);
            Assert.Equal(2, openXml.Arguments.Count);
            Assert.Null(openXml.WithClause);
        }

        [Fact]
        public void Parse_OpenXml_WithSchema_HasCorrectStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT OPENXML(@idoc, '/root/row', 2) WITH (col1 varchar(50), col2 int) FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT OPENXML(@idoc, '/root/row') WITH (name varchar(50) './Name') FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT OPENXML(@idoc, '/root/row') WITH (dbo.T) FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name) FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.FunctionCall call = Assert.IsType<Expr.FunctionCall>(col.Expression);
            Assert.NotNull(call.WithinGroup);
            Assert.Single(call.WithinGroup.OrderBy);
        }

        [Fact]
        public void Parse_WithinGroup_WithoutClause()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT STRING_AGG(name, ', ') FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT created AT TIME ZONE 'UTC' FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.AtTimeZone atz = Assert.IsType<Expr.AtTimeZone>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(atz.Expression);
            Assert.IsType<Expr.StringLiteral>(atz.TimeZone);
        }

        [Fact]
        public void Parse_AtTimeZone_ChainedStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT created AT TIME ZONE 'UTC' AT TIME ZONE 'PST' FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT IIF(a > 0, 'yes', 'no') FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT ~a FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Unary unary = Assert.IsType<Expr.Unary>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(unary.Right);
        }

        [Fact]
        public void Parse_BitwiseAnd_HasCorrectStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a & b FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(col.Expression);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Left);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_BitwiseNot_HigherPrecedenceThanMultiply()
        {
            // ~a * b should parse as (~a) * b
            Stmt.Select stmt = Stmt.ParseSelect("SELECT ~a * b FROM T");
            SelectColumn col = Assert.IsType<SelectColumn>(SelectExpressionOf(stmt).Columns[0]);
            Expr.Binary binary = Assert.IsType<Expr.Binary>(col.Expression);
            Assert.IsType<Expr.Unary>(binary.Left);
            Assert.IsType<Expr.ColumnIdentifier>(binary.Right);
        }

        [Fact]
        public void Parse_Bitwise_MultiplyHigherPrecedenceThanAnd()
        {
            // a * b & c should parse as (a * b) & c
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a * b & c FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a COLLATE Latin1_General_CI_AS FROM T");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T WHERE CONTAINS(*, 'test')");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            Assert.IsType<AST.Predicate.FullTextAllColumns>(contains.Columns);
        }

        [Fact]
        public void Parse_Contains_WithSingleColumn_HasCorrectStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T WHERE CONTAINS(col1, 'test')");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            AST.Predicate.FullTextColumnNames columnNames = Assert.IsType<AST.Predicate.FullTextColumnNames>(contains.Columns);
            Assert.Single(columnNames.Columns);
            Assert.Equal("col1", columnNames.Columns[0].ColumnName.Name);
        }

        [Fact]
        public void Parse_Contains_WithColumnList_HasCorrectStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T WHERE CONTAINS((col1, col2), 'test')");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            AST.Predicate.FullTextColumnNames columnNames = Assert.IsType<AST.Predicate.FullTextColumnNames>(contains.Columns);
            Assert.Equal(2, columnNames.Columns.Count);
            Assert.Equal("col1", columnNames.Columns[0].ColumnName.Name);
            Assert.Equal("col2", columnNames.Columns[1].ColumnName.Name);
        }

        [Fact]
        public void Parse_Contains_WithLanguage_HasCorrectStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T WHERE CONTAINS(a, 'test', LANGUAGE 1033)");
            AST.Predicate.Contains contains = Assert.IsType<AST.Predicate.Contains>(SelectExpressionOf(stmt).Where);
            Assert.NotNull(contains.Language);
        }

        [Fact]
        public void Parse_Freetext_HasCorrectStructure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T WHERE FREETEXT(col1, 'search')");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION ALL SELECT b FROM T2");
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.UnionAll, setOp.OperationType);
            Assert.IsType<SelectExpression>(setOp.Left);
            Assert.IsType<SelectExpression>(setOp.Right);
        }

        [Fact]
        public void Parse_Union_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2");
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Union, setOp.OperationType);
        }

        [Fact]
        public void Parse_Intersect_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 INTERSECT SELECT b FROM T2");
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Intersect, setOp.OperationType);
        }

        [Fact]
        public void Parse_Except_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 EXCEPT SELECT b FROM T2");
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Except, setOp.OperationType);
        }

        [Fact]
        public void Parse_ChainedUnion_IsLeftAssociative()
        {
            // A UNION B UNION C => (A UNION B) UNION C
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 UNION SELECT c FROM T3");
            SetOperation outerOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Union, outerOp.OperationType);
            Assert.IsType<SelectExpression>(outerOp.Right);

            SetOperation innerOp = Assert.IsType<SetOperation>(outerOp.Left);
            Assert.Equal(SetOperationType.Union, innerOp.OperationType);
            Assert.IsType<SelectExpression>(innerOp.Left);
            Assert.IsType<SelectExpression>(innerOp.Right);
        }

        [Fact]
        public void Parse_IntersectBindsTighterThanUnion()
        {
            // A UNION B INTERSECT C => A UNION (B INTERSECT C)
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 INTERSECT SELECT c FROM T3");
            SetOperation outerOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Union, outerOp.OperationType);
            Assert.IsType<SelectExpression>(outerOp.Left);

            SetOperation innerOp = Assert.IsType<SetOperation>(outerOp.Right);
            Assert.Equal(SetOperationType.Intersect, innerOp.OperationType);
        }

        [Fact]
        public void Parse_IntersectBindsTighterThanExcept()
        {
            // A EXCEPT B INTERSECT C => A EXCEPT (B INTERSECT C)
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 EXCEPT SELECT b FROM T2 INTERSECT SELECT c FROM T3");
            SetOperation outerOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(SetOperationType.Except, outerOp.OperationType);
            Assert.IsType<SelectExpression>(outerOp.Left);

            SetOperation innerOp = Assert.IsType<SetOperation>(outerOp.Right);
            Assert.Equal(SetOperationType.Intersect, innerOp.OperationType);
        }

        [Fact]
        public void Parse_SetOperationWithOrderBy_OrderByOnOutermost()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 ORDER BY a");
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
            Assert.Equal(1, setOp.OrderBy.Items.Count);
        }

        [Fact]
        public void Parse_SetOperationWithOption_OptionOnStatement()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T1 UNION SELECT b FROM T2 ORDER BY a OPTION (RECOMPILE)");
            SetOperation setOp = Assert.IsType<SetOperation>(stmt.Query);
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T ORDER BY a");
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
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T FOR XML RAW('row'), ROOT('data'), ELEMENTS XSINIL, TYPE");
            ForXmlClause forXml = Assert.IsType<ForXmlClause>(stmt.Query.For);

            Assert.Equal(ForXmlMode.Raw, forXml.Mode);
            Assert.Equal(3, forXml.Directives.Count);
            Assert.Equal(ForDirectiveType.Root, forXml.Directives[0].DirectiveType);
            Assert.Equal(ForDirectiveType.ElementsXsiNil, forXml.Directives[1].DirectiveType);
            Assert.Equal(ForDirectiveType.Type, forXml.Directives[2].DirectiveType);
        }

        [Fact]
        public void Parse_ForJson_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T FOR JSON PATH, ROOT('result'), INCLUDE_NULL_VALUES");
            ForJsonClause forJson = Assert.IsType<ForJsonClause>(stmt.Query.For);

            Assert.Equal(ForJsonMode.Path, forJson.Mode);
            Assert.Equal(2, forJson.Directives.Count);
            Assert.Equal(ForDirectiveType.Root, forJson.Directives[0].DirectiveType);
            Assert.Equal(ForDirectiveType.IncludeNullValues, forJson.Directives[1].DirectiveType);
        }

        [Fact]
        public void Parse_ForBrowse_Structure()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T FOR BROWSE");
            Assert.IsType<ForBrowseClause>(stmt.Query.For);
        }

        [Fact]
        public void Parse_NoForClause_IsNull()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T");
            Assert.Null(stmt.Query.For);
        }


        #endregion

        #region Programmatic AST Construction Tests

        [Fact]
        public void Construct_SelectStarFromTable_ProducesValidSql()
        {
            SelectExpression selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new Expr.Wildcard());

            FromClause from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("Users"))));
            selectExpr.From = from;

            Stmt.Select stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT * FROM Users", stmt.ToSource());
        }

        [Fact]
        public void Construct_SelectMultipleColumns_ProducesValidSql()
        {
            SelectExpression selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("a")), null));
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("b")), null));

            FromClause from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("T"))));
            selectExpr.From = from;

            Stmt.Select stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a, b FROM T", stmt.ToSource());
        }

        [Fact]
        public void Construct_SelectWithAlias_ProducesValidSql()
        {
            SelectExpression selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("a")),
                new SuffixAlias("alias")));

            FromClause from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("T"))));
            selectExpr.From = from;

            Stmt.Select stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a AS alias FROM T", stmt.ToSource());
        }

        [Fact]
        public void Construct_BinaryExpression_ProducesValidSql()
        {
            Expr.Binary binary = new Expr.Binary(
                new Expr.ColumnIdentifier(new ColumnName("a")),
                Expr.ArithmeticOperator.Add,
                new Expr.ColumnIdentifier(new ColumnName("b")));

            SelectExpression selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(binary, null));

            Stmt.Select stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a + b", stmt.ToSource());
        }

        [Fact]
        public void Construct_ComparisonPredicate_ProducesValidSql()
        {
            Predicate.Comparison comparison = new AST.Predicate.Comparison(
                new Expr.ColumnIdentifier(new ColumnName("x")),
                AST.ComparisonOperator.GreaterThan,
                new Expr.IntLiteral(10));

            SelectExpression selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new SelectColumn(
                new Expr.ColumnIdentifier(new ColumnName("a")), null));

            FromClause from = new FromClause();
            from.TableSources.Add(new TableReference(new Expr.ObjectIdentifier(new ObjectName("T"))));
            selectExpr.From = from;
            selectExpr.AddWhere(comparison);

            Stmt.Select stmt = new Stmt.Select(selectExpr);
            Assert.Equal("SELECT a FROM T WHERE x > 10", stmt.ToSource());
        }

        #endregion

        #region SELECT INTO Tests

        [Fact]
        public void ParseSelectInto_BasicTempTable_RoundTrips()
        {
            string source = "SELECT col1, col2 INTO #Temp FROM T";
            Stmt.Select stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseSelectInto_ParsesIntoTarget()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT col1 INTO #Temp FROM T");
            SelectExpression selectExpr = SelectExpressionOf(stmt);
            Assert.NotNull(selectExpr.Into);
            Assert.Equal("#Temp", selectExpr.Into.ObjectName.Name);
        }

        [Fact]
        public void ParseSelectInto_GlobalTempTable_RoundTrips()
        {
            string source = "SELECT * INTO ##GlobalTemp FROM T";
            Stmt.Select stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseSelectInto_SchemaQualifiedTarget_RoundTrips()
        {
            string source = "SELECT a, b INTO dbo.NewTable FROM T";
            Stmt.Select stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseSelectInto_WithWhere_RoundTrips()
        {
            string source = "SELECT col1 INTO #Temp FROM T WHERE col1 > 10";
            Stmt.Select stmt = Stmt.ParseSelect(source);
            Assert.Equal(source, stmt.ToSource());
        }

        #endregion

        #region INSERT Tests

        [Fact]
        public void ParseInsert_SelectSource_RoundTrips()
        {
            string source = "INSERT INTO #Temp SELECT col1, col2 FROM T";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_WithoutInto_RoundTrips()
        {
            string source = "INSERT #Temp SELECT col1 FROM T";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_WithColumnList_RoundTrips()
        {
            string source = "INSERT INTO #Temp (col1, col2) SELECT a, b FROM T";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_Values_RoundTrips()
        {
            string source = "INSERT INTO T (col1, col2) VALUES (1, 'hello')";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_MultipleValueRows_RoundTrips()
        {
            string source = "INSERT INTO T VALUES (1, 'a'), (2, 'b'), (3, 'c')";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_DefaultValues_RoundTrips()
        {
            string source = "INSERT INTO T DEFAULT VALUES";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_ExecSource_RoundTrips()
        {
            string source = "INSERT INTO #Temp EXEC sp_GetData";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_ExecWithArgs_RoundTrips()
        {
            string source = "INSERT INTO #Temp EXEC sp_GetData @Param1, @Param2";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_TableVariable_RoundTrips()
        {
            string source = "INSERT INTO @TableVar SELECT col1 FROM T";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_WithCte_RoundTrips()
        {
            string source = "WITH cte AS (SELECT col1 FROM T) INSERT INTO #Temp SELECT col1 FROM cte";
            Stmt stmt = Stmt.Parse(source);
            Assert.IsType<Stmt.Insert>(stmt);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_SelectSource_HasCorrectSourceType()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO T SELECT 1");
            Assert.IsType<SelectSource>(stmt.Source);
        }

        [Fact]
        public void ParseInsert_ValuesSource_HasCorrectSourceType()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO T VALUES (1)");
            Assert.IsType<ValuesSource>(stmt.Source);
        }

        [Fact]
        public void ParseInsert_DefaultValuesSource_HasCorrectSourceType()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO T DEFAULT VALUES");
            Assert.IsType<DefaultValuesSource>(stmt.Source);
        }

        [Fact]
        public void ParseInsert_ExecSource_HasCorrectSourceType()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO T EXEC sp_Proc");
            Assert.IsType<ExecSource>(stmt.Source);
        }

        [Fact]
        public void ParseInsert_TargetIsObjectIdentifier()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO dbo.T VALUES (1)");
            Expr.ObjectIdentifier target = Assert.IsType<Expr.ObjectIdentifier>(stmt.Target);
            Assert.Equal("T", target.ObjectName.Name);
            Assert.Equal("dbo", target.SchemaName.Name);
        }

        [Fact]
        public void ParseInsert_TargetIsVariable()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO @TableVar VALUES (1)");
            Assert.IsType<Expr.Variable>(stmt.Target);
        }

        [Fact]
        public void ParseInsert_ColumnListParsed()
        {
            Stmt.Insert stmt = Stmt.ParseInsert("INSERT INTO T (a, b, c) VALUES (1, 2, 3)");
            Assert.NotNull(stmt.ColumnList);
            Assert.Equal(3, stmt.ColumnList.Columns.Count);
            Assert.Equal("a", stmt.ColumnList.Columns[0].Name);
            Assert.Equal("b", stmt.ColumnList.Columns[1].Name);
            Assert.Equal("c", stmt.ColumnList.Columns[2].Name);
        }

        [Fact]
        public void ParseInsert_SchemaQualifiedTable_RoundTrips()
        {
            string source = "INSERT INTO dbo.MyTable (col1) VALUES (1)";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseInsert_Execute_RoundTrips()
        {
            string source = "INSERT INTO #Temp EXECUTE sp_GetData";
            Stmt.Insert stmt = Stmt.ParseInsert(source);
            Assert.Equal(source, stmt.ToSource());
        }

        #endregion

        #region Script (Multi-Statement) Tests

        [Fact]
        public void ParseScript_SingleSelect_RoundTrips()
        {
            string source = "SELECT 1";
            Script script = Script.Parse(source);
            Assert.Single(script.Statements);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ParseScript_TwoSelects_WithSemicolons_RoundTrips()
        {
            string source = "SELECT 1; SELECT 2";
            Script script = Script.Parse(source);
            Assert.Equal(2, script.Statements.Count);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ParseScript_SelectThenInsert_RoundTrips()
        {
            string source = "SELECT col1 FROM T; INSERT INTO #Temp SELECT col1 FROM T";
            Script script = Script.Parse(source);
            Assert.Equal(2, script.Statements.Count);
            Assert.IsType<Stmt.Select>(script.Statements[0]);
            Assert.IsType<Stmt.Insert>(script.Statements[1]);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ParseScript_InsertThenSelect_RoundTrips()
        {
            string source = "INSERT INTO #Temp SELECT 1; SELECT * FROM #Temp";
            Script script = Script.Parse(source);
            Assert.Equal(2, script.Statements.Count);
            Assert.IsType<Stmt.Insert>(script.Statements[0]);
            Assert.IsType<Stmt.Select>(script.Statements[1]);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ParseScript_ThreeStatements_RoundTrips()
        {
            string source = "SELECT 1; SELECT 2; SELECT 3";
            Script script = Script.Parse(source);
            Assert.Equal(3, script.Statements.Count);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ParseScript_TrailingSemicolon_RoundTrips()
        {
            string source = "SELECT 1;";
            Script script = Script.Parse(source);
            Assert.Single(script.Statements);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ParseScript_NoSemicolonBetweenSelectAndInsert_RoundTrips()
        {
            // Statements can be separated by the next statement keyword without semicolon
            string source = "SELECT 1\nINSERT INTO T VALUES (1)";
            Script script = Script.Parse(source);
            Assert.Equal(2, script.Statements.Count);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void Parse_SingleStatement_AllowsTrailingSemicolon()
        {
            string source = "SELECT 1;";
            Stmt stmt = Stmt.Parse(source);
            Assert.IsType<Stmt.Select>(stmt);
        }

        [Fact]
        public void Parse_InsertViaGenericParse_ReturnsInsert()
        {
            Stmt stmt = Stmt.Parse("INSERT INTO T VALUES (1)");
            Assert.IsType<Stmt.Insert>(stmt);
        }

        #endregion

        #region SELECT ALL

        [Fact]
        public void Parse_SelectAll_RoundTrips()
        {
            string source = "SELECT ALL a FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_SelectAll_SetsQuantifier()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT ALL a FROM T");
            SelectExpression expr = SelectExpressionOf(select);
            Assert.Equal(SetQuantifier.All, expr.Quantifier);
        }

        #endregion

        #region Bracket/Quote Normalization

        [Fact]
        public void Parse_BracketedTableName_NameIsNormalized()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT * FROM [users]");
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("users", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_BracketedTableName_LexemePreservesBrackets()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT * FROM [users]");
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("[users]", tableRef.TableName.ObjectName.Lexeme);
        }

        [Fact]
        public void Parse_BracketedTableName_ToSourcePreservesBrackets()
        {
            string source = "SELECT * FROM [users]";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_BracketedSchemaAndTable_NamesAreNormalized()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT * FROM [dbo].[MyTable]");
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("MyTable", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_BracketedSchemaAndTable_ToSourcePreservesBrackets()
        {
            string source = "SELECT * FROM [dbo].[MyTable]";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_BracketedColumnName_NameIsNormalized()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT [Col] FROM T");
            SelectColumn item = Assert.IsType<SelectColumn>(SelectExpressionOf(select).Columns[0]);
            Expr.ColumnIdentifier columnId = Assert.IsType<Expr.ColumnIdentifier>(item.Expression);
            Assert.Equal("Col", columnId.ColumnName.Name);
        }

        [Fact]
        public void Parse_BracketedColumnName_ToSourcePreservesBrackets()
        {
            string source = "SELECT [Col] FROM T";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_BracketedAlias_NameIsNormalized()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT * FROM T1 AS [A]");
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("A", tableRef.Alias.Name);
        }

        [Fact]
        public void Parse_BracketedAlias_LexemePreservesBrackets()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT * FROM T1 AS [A]");
            TableReference tableRef = Assert.IsType<TableReference>(SelectExpressionOf(select).From.TableSources[0]);
            Assert.Equal("[A]", tableRef.Alias.Lexeme);
        }

        [Fact]
        public void Parse_BracketedAlias_ToSourcePreservesBrackets()
        {
            string source = "SELECT * FROM T1 AS [A]";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_BracketedCteName_NameIsNormalized()
        {
            Stmt.Select stmt = Stmt.ParseSelect("WITH [MyCTE] AS (SELECT a FROM T) SELECT a FROM MyCTE");
            Assert.Equal("MyCTE", stmt.CteStmt.Ctes[0].Name);
        }

        [Fact]
        public void Parse_BracketedCteName_ToSourcePreservesBrackets()
        {
            string source = "WITH [MyCTE] AS (SELECT a FROM T) SELECT a FROM MyCTE";
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_MixedBracketedAndUnbracketed_AllNamesNormalized()
        {
            Stmt.Select select = Stmt.ParseSelect("SELECT [a], b FROM [dbo].[T1]");
            SelectExpression expr = SelectExpressionOf(select);

            SelectColumn col0 = Assert.IsType<SelectColumn>(expr.Columns[0]);
            Expr.ColumnIdentifier colId0 = Assert.IsType<Expr.ColumnIdentifier>(col0.Expression);
            Assert.Equal("a", colId0.ColumnName.Name);

            SelectColumn col1 = Assert.IsType<SelectColumn>(expr.Columns[1]);
            Expr.ColumnIdentifier colId1 = Assert.IsType<Expr.ColumnIdentifier>(col1.Expression);
            Assert.Equal("b", colId1.ColumnName.Name);

            TableReference tableRef = Assert.IsType<TableReference>(expr.From.TableSources[0]);
            Assert.Equal("dbo", tableRef.TableName.SchemaName.Name);
            Assert.Equal("T1", tableRef.TableName.ObjectName.Name);
        }

        [Fact]
        public void Parse_MixedBracketedAndUnbracketed_ToSourcePreservesBrackets()
        {
            string source = "SELECT [a], b FROM [dbo].[T1]";
            Assert.Equal(source, RoundTrip(source));
        }

        #endregion

        #region DROP TABLE

        [Fact]
        public void DropTable_Basic()
        {
            var drop = Stmt.ParseDrop("DROP TABLE MyTable");

            Assert.Equal(ObjectType.Table, drop.ObjectType);
            Assert.False(drop.IfExists);
            Assert.Single(drop.Targets);
            Assert.Equal("MyTable", drop.Targets[0].ObjectName.Name);
        }

        [Fact]
        public void DropTable_IfExists()
        {
            var drop = Stmt.ParseDrop("DROP TABLE IF EXISTS #TempTable");

            Assert.Equal(ObjectType.Table, drop.ObjectType);
            Assert.True(drop.IfExists);
            Assert.Single(drop.Targets);
            Assert.Equal("#TempTable", drop.Targets[0].ObjectName.Name);
        }

        [Fact]
        public void DropTable_MultipleTargets()
        {
            var drop = Stmt.ParseDrop("DROP TABLE T1, T2, T3");

            Assert.Equal(3, drop.Targets.Count);
            Assert.Equal("T1", drop.Targets[0].ObjectName.Name);
            Assert.Equal("T2", drop.Targets[1].ObjectName.Name);
            Assert.Equal("T3", drop.Targets[2].ObjectName.Name);
        }

        [Theory]
        [InlineData("DROP TABLE MyTable")]
        [InlineData("DROP TABLE IF EXISTS #TempTable")]
        [InlineData("DROP TABLE T1, T2, T3")]
        [InlineData("DROP TABLE dbo.MyTable")]
        [InlineData("DROP TABLE IF EXISTS dbo.T1, dbo.T2")]
        public void DropTable_RoundTrips(string source)
        {
            Assert.Equal(source, Stmt.ParseDrop(source).ToSource());
        }

        [Fact]
        public void DropTable_InScript()
        {
            string sql = "DROP TABLE IF EXISTS #temp; SELECT 1 INTO #temp; SELECT * FROM #temp";
            var script = Script.Parse(sql);

            Assert.Equal(3, script.Statements.Count);
            Assert.IsType<Stmt.Drop>(script.Statements[0]);
            Assert.IsType<Stmt.Select>(script.Statements[1]);
            Assert.IsType<Stmt.Select>(script.Statements[2]);
            Assert.Equal(sql, script.ToSource());
        }

        #endregion

        #region EXECUTE Statement - Proc Form Round-Trips

        [Theory]
        [InlineData("EXEC sp_Help")]
        [InlineData("EXEC sp_GetData 1")]
        [InlineData("EXECUTE dbo.MyProc 1")]
        [InlineData("EXEC sp_GetData 1, 'hello', @Var")]
        [InlineData("EXECUTE sp_GetData")]
        [InlineData("EXEC dbo.MyProc @p1 = 1, @p2 = 'hello'")]
        [InlineData("EXEC sp_Proc @Var OUTPUT")]
        [InlineData("EXEC sp_Proc @p1 = @Var OUTPUT")]
        [InlineData("EXEC sp_Proc DEFAULT, @p2 = DEFAULT")]
        [InlineData("EXEC @ret = dbo.MyProc 1")]
        [InlineData("EXEC @procVar")]
        [InlineData("EXEC sp_Proc WITH RECOMPILE")]
        [InlineData("EXEC sp_Proc @p1 = @Var OUT")]
        public void Execute_RoundTrips(string source)
        {
            Assert.Equal(source, Stmt.ParseExecute(source).ToSource());
        }

        #endregion

        #region EXECUTE Statement - String Form Round-Trips

        [Theory]
        [InlineData("EXEC ('SELECT 1')")]
        [InlineData("EXEC (@sql)")]
        [InlineData("EXEC (N'SELECT ' + @col + N' FROM T')")]
        [InlineData("EXEC ('SELECT 1') AS USER = 'dbo'")]
        [InlineData("EXEC ('SELECT 1') AS LOGIN = 'sa'")]
        [InlineData("EXEC ('SELECT 1') AT LinkedServer")]
        [InlineData("EXEC ('SELECT ?', @p1) AT LinkedServer")]
        public void ExecuteString_RoundTrips(string source)
        {
            Assert.Equal(source, Stmt.ParseExecute(source).ToSource());
        }

        #endregion

        #region EXECUTE Statement - WITH RESULT SETS Round-Trips

        [Theory]
        [InlineData("EXEC sp_Proc WITH RESULT SETS ((col1 INT, col2 VARCHAR(50)))")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS ((col1 INT NOT NULL, col2 NVARCHAR(MAX) NULL))")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS ((col1 INT COLLATE Latin1_General_CI_AS))")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS NONE")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS UNDEFINED")]
        [InlineData("EXEC sp_Proc WITH RECOMPILE, RESULT SETS NONE")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS (AS OBJECT dbo.MyTable)")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS (AS TYPE dbo.MyTableType)")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS (AS FOR XML)")]
        [InlineData("EXEC sp_Proc WITH RESULT SETS ((col1 INT), (col2 VARCHAR(50)))")]
        public void Execute_WithResultSets_RoundTrips(string source)
        {
            Assert.Equal(source, Stmt.ParseExecute(source).ToSource());
        }

        #endregion

        #region EXECUTE Statement - Structure Tests

        [Fact]
        public void Execute_HasCorrectTarget()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC dbo.MyProc 1"));
            var target = Assert.IsType<Expr.ObjectIdentifier>(exec.Target);
            Assert.Equal("MyProc", target.ObjectName.Name);
            Assert.Equal("dbo", target.SchemaName.Name);
        }

        [Fact]
        public void Execute_NoArgs_HasEmptyArguments()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC sp_Help"));
            Assert.Equal(0, exec.Arguments.Count);
        }

        [Fact]
        public void Execute_MultipleArgs_HasCorrectCount()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC sp_GetData 1, 'hello', @Var"));
            Assert.Equal(3, exec.Arguments.Count);
        }

        [Fact]
        public void Execute_ReturnStatus_ParsedCorrectly()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC @ret = dbo.MyProc 1"));
            Assert.NotNull(exec.ReturnVariable);
            Assert.Equal("@ret", exec.ReturnVariable.Lexeme);
        }

        [Fact]
        public void Execute_NamedParameters_CorrectNames()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC sp_Proc @p1 = 1, @p2 = 'hello'"));
            Assert.Equal(2, exec.Arguments.Count);
            Assert.Equal("@p1", exec.Arguments[0].ParameterName.Lexeme);
            Assert.Equal("@p2", exec.Arguments[1].ParameterName.Lexeme);
        }

        [Fact]
        public void Execute_OutputFlag_IsSet()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC sp_Proc @Var OUTPUT"));
            Assert.Single(exec.Arguments);
            Assert.IsType<OutputArgument>(exec.Arguments[0]);
        }

        [Fact]
        public void Execute_VariableTarget_IsVariable()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC @procVar"));
            Assert.IsType<Expr.Variable>(exec.Target);
        }

        [Fact]
        public void Execute_DefaultArg_IsDefault()
        {
            var exec = Assert.IsType<Stmt.Execute>(Stmt.ParseExecute("EXEC sp_Proc DEFAULT, @p2 = DEFAULT"));
            Assert.IsType<DefaultArgument>(exec.Arguments[0]);
            Assert.IsType<DefaultArgument>(exec.Arguments[1]);
        }

        #endregion

        #region EXECUTE Statement - Script/Dispatch Tests

        [Fact]
        public void Execute_ViaGenericParse_ReturnsExecute()
        {
            Stmt stmt = Stmt.Parse("EXEC sp_GetData 1");
            Assert.IsType<Stmt.Execute>(stmt);
        }

        [Fact]
        public void Execute_ViaGenericParse_ReturnsExecuteString()
        {
            Stmt stmt = Stmt.Parse("EXEC ('SELECT 1')");
            Assert.IsType<Stmt.ExecuteString>(stmt);
        }

        [Fact]
        public void Execute_InScript_RoundTrips()
        {
            string source = "EXEC sp_A; SELECT 1";
            Script script = Script.Parse(source);
            Assert.Equal(2, script.Statements.Count);
            Assert.IsType<Stmt.Execute>(script.Statements[0]);
            Assert.IsType<Stmt.Select>(script.Statements[1]);
            Assert.Equal(source, script.ToSource());
        }

        [Fact]
        public void ExecuteString_ViaParseExecute_ReturnsExecuteString()
        {
            Stmt stmt = Stmt.ParseExecute("EXEC ('SELECT 1')");
            Assert.IsType<Stmt.ExecuteString>(stmt);
        }

        #endregion

        #region Contextual Keyword as Identifier

        [Theory]
        // Window frame keywords
        [InlineData("SELECT ROWS FROM T")]
        [InlineData("SELECT RANGE FROM T")]
        [InlineData("SELECT PARTITION FROM T")]
        [InlineData("SELECT UNBOUNDED FROM T")]
        [InlineData("SELECT PRECEDING FROM T")]
        [InlineData("SELECT FOLLOWING FROM T")]
        [InlineData("SELECT ROW FROM T")]
        // Ranking function names (without parens, parsed as identifiers)
        [InlineData("SELECT ROW_NUMBER FROM T")]
        [InlineData("SELECT RANK FROM T")]
        [InlineData("SELECT DENSE_RANK FROM T")]
        [InlineData("SELECT NTILE FROM T")]
        // Join / apply keywords
        [InlineData("SELECT APPLY FROM T")]
        // Join hint keywords
        [InlineData("SELECT LOOP FROM T")]
        [InlineData("SELECT HASH FROM T")]
        [InlineData("SELECT REMOTE FROM T")]
        // TABLESAMPLE keywords
        [InlineData("SELECT SYSTEM FROM T")]
        [InlineData("SELECT CONTAINED FROM T")]
        [InlineData("SELECT REPEATABLE FROM T")]
        // GROUP BY keywords
        [InlineData("SELECT ROLLUP FROM T")]
        [InlineData("SELECT CUBE FROM T")]
        [InlineData("SELECT GROUPING FROM T")]
        [InlineData("SELECT SETS FROM T")]
        // Full-text / misc expression keywords
        [InlineData("SELECT LANGUAGE FROM T")]
        [InlineData("SELECT IIF FROM T")]
        // AT TIME ZONE keywords
        [InlineData("SELECT AT FROM T")]
        [InlineData("SELECT TIME FROM T")]
        [InlineData("SELECT ZONE FROM T")]
        // TOP clause keywords
        [InlineData("SELECT TIES FROM T")]
        // OFFSET-FETCH keywords
        [InlineData("SELECT OFFSET FROM T")]
        [InlineData("SELECT FIRST FROM T")]
        [InlineData("SELECT NEXT FROM T")]
        [InlineData("SELECT ONLY FROM T")]
        // FOR XML/JSON keywords
        [InlineData("SELECT XML FROM T")]
        [InlineData("SELECT JSON FROM T")]
        [InlineData("SELECT RAW FROM T")]
        [InlineData("SELECT AUTO FROM T")]
        [InlineData("SELECT EXPLICIT FROM T")]
        [InlineData("SELECT PATH FROM T")]
        [InlineData("SELECT ROOT FROM T")]
        [InlineData("SELECT ELEMENTS FROM T")]
        [InlineData("SELECT TYPE FROM T")]
        [InlineData("SELECT BINARY FROM T")]
        [InlineData("SELECT BASE64 FROM T")]
        [InlineData("SELECT XMLDATA FROM T")]
        [InlineData("SELECT XMLSCHEMA FROM T")]
        [InlineData("SELECT XSINIL FROM T")]
        [InlineData("SELECT ABSENT FROM T")]
        [InlineData("SELECT INCLUDE_NULL_VALUES FROM T")]
        [InlineData("SELECT WITHOUT_ARRAY_WRAPPER FROM T")]
        // Table hint keywords
        [InlineData("SELECT NOEXPAND FROM T")]
        [InlineData("SELECT FORCESCAN FROM T")]
        [InlineData("SELECT FORCESEEK FROM T")]
        [InlineData("SELECT NOLOCK FROM T")]
        [InlineData("SELECT NOWAIT FROM T")]
        [InlineData("SELECT PAGLOCK FROM T")]
        [InlineData("SELECT READCOMMITTED FROM T")]
        [InlineData("SELECT READCOMMITTEDLOCK FROM T")]
        [InlineData("SELECT READPAST FROM T")]
        [InlineData("SELECT READUNCOMMITTED FROM T")]
        [InlineData("SELECT REPEATABLEREAD FROM T")]
        [InlineData("SELECT ROWLOCK FROM T")]
        [InlineData("SELECT SERIALIZABLE FROM T")]
        [InlineData("SELECT SNAPSHOT FROM T")]
        [InlineData("SELECT SPATIAL_WINDOW_MAX_CELLS FROM T")]
        [InlineData("SELECT TABLOCK FROM T")]
        [InlineData("SELECT TABLOCKX FROM T")]
        [InlineData("SELECT UPDLOCK FROM T")]
        [InlineData("SELECT XLOCK FROM T")]
        // Query hint keywords
        [InlineData("SELECT CONCAT FROM T")]
        [InlineData("SELECT DISABLE FROM T")]
        [InlineData("SELECT DISABLE_OPTIMIZED_PLAN_FORCING FROM T")]
        [InlineData("SELECT EXPAND FROM T")]
        [InlineData("SELECT EXTERNALPUSHDOWN FROM T")]
        [InlineData("SELECT FAST FROM T")]
        [InlineData("SELECT FORCE FROM T")]
        [InlineData("SELECT HINT FROM T")]
        [InlineData("SELECT IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX FROM T")]
        [InlineData("SELECT KEEP FROM T")]
        [InlineData("SELECT KEEPFIXED FROM T")]
        [InlineData("SELECT LABEL FROM T")]
        [InlineData("SELECT MAX_GRANT_PERCENT FROM T")]
        [InlineData("SELECT MAXDOP FROM T")]
        [InlineData("SELECT MAXRECURSION FROM T")]
        [InlineData("SELECT MIN_GRANT_PERCENT FROM T")]
        [InlineData("SELECT NO_PERFORMANCE_SPOOL FROM T")]
        [InlineData("SELECT OPTIMIZE FROM T")]
        [InlineData("SELECT PARAMETERIZATION FROM T")]
        [InlineData("SELECT QUERYTRACEON FROM T")]
        [InlineData("SELECT RECOMPILE FROM T")]
        [InlineData("SELECT ROBUST FROM T")]
        [InlineData("SELECT SCALEOUTEXECUTION FROM T")]
        [InlineData("SELECT UNKNOWN FROM T")]
        [InlineData("SELECT VIEWS FROM T")]
        // Temporal table keywords
        [InlineData("SELECT SYSTEM_TIME FROM T")]
        // Miscellaneous keywords
        [InlineData("SELECT TIMESTAMP FROM T")]
        [InlineData("SELECT PRECISION FROM T")]
        // EXECUTE statement keywords
        [InlineData("SELECT OUTPUT FROM T")]
        [InlineData("SELECT OUT FROM T")]
        [InlineData("SELECT LOGIN FROM T")]
        [InlineData("SELECT RESULT FROM T")]
        [InlineData("SELECT NONE FROM T")]
        [InlineData("SELECT UNDEFINED FROM T")]
        [InlineData("SELECT OBJECT FROM T")]
        [InlineData("SELECT SIMPLE FROM T")]
        [InlineData("SELECT FORCED FROM T")]
        public void Parse_ContextualKeywordAsIdentifier_RoundTrips(string source)
        {
            Assert.Equal(source, RoundTrip(source));
        }

        [Fact]
        public void Parse_ContextualKeywordAsAlias_RoundTrips()
        {
            Assert.Equal("SELECT 1 AS ROWS FROM T", RoundTrip("SELECT 1 AS ROWS FROM T"));
        }

[Fact]
        public void Parse_QualifiedContextualKeyword_RoundTrips()
        {
            Assert.Equal("SELECT c.precision AS Precision FROM sys.columns c",
                RoundTrip("SELECT c.precision AS Precision FROM sys.columns c"));
        }

        #endregion

        #region Procedural Statements - Round-Trips

        [Theory]
        // DECLARE
        [InlineData("DECLARE @SQL NVARCHAR(250)")]
        [InlineData("DECLARE @X INT")]
        [InlineData("DECLARE @Name VARCHAR(100)")]
        [InlineData("DECLARE @X INT, @Y INT")]
        [InlineData("DECLARE @X INT, @Y VARCHAR(50), @Z DECIMAL(10, 2)")]
        [InlineData("DECLARE @X INT = 1")]
        [InlineData("DECLARE @X INT = 1, @Y INT = 2")]
        // SET
        [InlineData("SET @X = 1")]
        [InlineData("SET @SQL = N'SELECT 1'")]
        [InlineData("SET @X = @Y + 1")]
        [InlineData("SET @Name = 'hello'")]
        // BEGIN/END
        [InlineData("BEGIN SELECT 1 END")]
        [InlineData("BEGIN SELECT 1; SELECT 2 END")]
        [InlineData("BEGIN EXEC sp_Test END")]
        // IF/ELSE
        [InlineData("IF 1 = 1 SELECT 1")]
        [InlineData("IF 1 = 1 SELECT 1 ELSE SELECT 0")]
        [InlineData("IF @X > 0 SELECT 1 ELSE SELECT 0")]
        [InlineData("IF EXISTS (SELECT 1 FROM T) SELECT 1")]
        [InlineData("IF EXISTS (SELECT 1 FROM T) SELECT 1 ELSE SELECT 0")]
        [InlineData("IF 1 = 1 BEGIN SELECT 1 END")]
        [InlineData("IF 1 = 1 BEGIN SELECT 1 END ELSE BEGIN SELECT 0 END")]
        [InlineData("IF 1 = 1 IF 2 = 2 SELECT 1 ELSE SELECT 0")]
        // Edge cases
        [InlineData("BEGIN END")]
        [InlineData("BEGIN BEGIN SELECT 1 END END")]
        [InlineData("SET @X = CASE WHEN @Y > 0 THEN 1 ELSE 0 END")]
        [InlineData("IF 1 = 1 SELECT 1; SELECT 2")]
        [InlineData("IF 1 = 1 EXEC sp_Test ELSE SELECT 0")]
        // Script combinations
        [InlineData("DECLARE @X INT; SET @X = 1; SELECT @X")]
        [InlineData("DECLARE @SQL NVARCHAR(250) SET @SQL = N'SELECT COL1 FROM T1 WHERE ID = 1 AND TYPE = 2' IF EXISTS (SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = N'T1' AND COLUMN_NAME = N'COL1') BEGIN EXEC SP_EXECUTESQL @SQL END ELSE SELECT 0")]
        public void ProceduralStatement_RoundTrips(string source)
        {
            Assert.Equal(source, Script.Parse(source).ToSource());
        }

        #endregion
    }
}
