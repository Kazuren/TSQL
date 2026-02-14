using TSQL.AST;
using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class VisitorTests
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

        #region Expr.Visitor Accept Dispatch

        [Fact]
        public void ColumnIdentifier_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            var col = (SelectColumn)stmt.SelectExpression.Columns[0];
            var visitor = new ExprTypeRecorder();
            col.Expression.Accept(visitor);
            Assert.Equal("ColumnIdentifier", visitor.LastType);
        }

        [Fact]
        public void ObjectIdentifier_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var tableRef = (TableReference)stmt.SelectExpression.From.TableSources[0];
            var visitor = new ExprTypeRecorder();
            tableRef.TableName.Accept(visitor);
            Assert.Equal("ObjectIdentifier", visitor.LastType);
        }

        [Fact]
        public void Wildcard_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var wildcard = (Expr.Wildcard)stmt.SelectExpression.Columns[0];
            var visitor = new ExprTypeRecorder();
            wildcard.Accept(visitor);
            Assert.Equal("Wildcard", visitor.LastType);
        }

        [Fact]
        public void QualifiedWildcard_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT T.* FROM T");
            var qualWildcard = (Expr.QualifiedWildcard)stmt.SelectExpression.Columns[0];
            var visitor = new ExprTypeRecorder();
            qualWildcard.Accept(visitor);
            Assert.Equal("QualifiedWildcard", visitor.LastType);
        }

        private class ExprTypeRecorder : Expr.Visitor<object?>
        {
            public string? LastType { get; private set; }
            public object? VisitBinaryExpr(Expr.Binary expr) { LastType = "Binary"; return null; }
            public object? VisitLiteralExpr(Expr.Literal expr) { LastType = "Literal"; return null; }
            public object? VisitColumnIdentifierExpr(Expr.ColumnIdentifier expr) { LastType = "ColumnIdentifier"; return null; }
            public object? VisitObjectIdentifierExpr(Expr.ObjectIdentifier expr) { LastType = "ObjectIdentifier"; return null; }
            public object? VisitWildcardExpr(Expr.Wildcard expr) { LastType = "Wildcard"; return null; }
            public object? VisitQualifiedWildcardExpr(Expr.QualifiedWildcard expr) { LastType = "QualifiedWildcard"; return null; }
            public object? VisitUnaryExpr(Expr.Unary expr) { LastType = "Unary"; return null; }
            public object? VisitGroupingExpr(Expr.Grouping expr) { LastType = "Grouping"; return null; }
            public object? VisitSubqueryExpr(Expr.Subquery expr) { LastType = "Subquery"; return null; }
            public object? VisitFunctionCallExpr(Expr.FunctionCall expr) { LastType = "FunctionCall"; return null; }
            public object? VisitVariableExpr(Expr.Variable expr) { LastType = "Variable"; return null; }
            public object? VisitWindowFunctionExpr(Expr.WindowFunction expr) { LastType = "WindowFunction"; return null; }
            public object? VisitSimpleCaseExpr(Expr.SimpleCase expr) { LastType = "SimpleCase"; return null; }
            public object? VisitSearchedCaseExpr(Expr.SearchedCase expr) { LastType = "SearchedCase"; return null; }
            public object? VisitCastExpr(Expr.CastExpression expr) { LastType = "Cast"; return null; }
            public object? VisitConvertExpr(Expr.ConvertExpression expr) { LastType = "Convert"; return null; }
            public object? VisitCollateExpr(Expr.Collate expr) { LastType = "Collate"; return null; }
        }

        #endregion

        #region TableSource.Visitor Accept Dispatch

        [Fact]
        public void TableReference_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var tableSource = stmt.SelectExpression.From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("TableReference", visitor.LastType);
        }

        [Fact]
        public void QualifiedJoin_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM A INNER JOIN B ON A.id = B.id");
            var tableSource = stmt.SelectExpression.From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("QualifiedJoin", visitor.LastType);
        }

        [Fact]
        public void CrossJoin_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM A CROSS JOIN B");
            var tableSource = stmt.SelectExpression.From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("CrossJoin", visitor.LastType);
        }

        [Fact]
        public void SubqueryReference_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM (SELECT 1 AS x) AS sub");
            var tableSource = stmt.SelectExpression.From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("SubqueryReference", visitor.LastType);
        }

        private class TableSourceTypeRecorder : TableSource.Visitor<object?>
        {
            public string? LastType { get; private set; }
            public object? VisitTableReference(TableReference source) { LastType = "TableReference"; return null; }
            public object? VisitSubqueryReference(SubqueryReference source) { LastType = "SubqueryReference"; return null; }
            public object? VisitTableVariableReference(TableVariableReference source) { LastType = "TableVariableReference"; return null; }
            public object? VisitQualifiedJoin(QualifiedJoin source) { LastType = "QualifiedJoin"; return null; }
            public object? VisitCrossJoin(CrossJoin source) { LastType = "CrossJoin"; return null; }
            public object? VisitApplyJoin(ApplyJoin source) { LastType = "ApplyJoin"; return null; }
            public object? VisitParenthesizedTableSource(ParenthesizedTableSource source) { LastType = "ParenthesizedTableSource"; return null; }
            public object? VisitPivotTableSource(PivotTableSource source) { LastType = "PivotTableSource"; return null; }
            public object? VisitUnpivotTableSource(UnpivotTableSource source) { LastType = "UnpivotTableSource"; return null; }
            public object? VisitValuesTableSource(ValuesTableSource source) { LastType = "ValuesTableSource"; return null; }
            public object? VisitRowsetFunctionReference(RowsetFunctionReference source) { LastType = "RowsetFunctionReference"; return null; }
        }

        #endregion

        #region SqlWalker

        [Fact]
        public void SqlWalker_VisitsAllNodeTypes_InComplexQuery()
        {
            var stmt = ParseSelect(
                "SELECT a, COUNT(b) FROM T1 INNER JOIN T2 ON T1.id = T2.id WHERE x = 42 AND y LIKE '%test%'");
            var counter = new NodeCounter();
            counter.Walk(stmt);

            Assert.True(counter.ColumnIdentifierCount > 0, "Should visit column identifiers");
            Assert.True(counter.LiteralCount > 0, "Should visit literals");
            Assert.True(counter.FunctionCallCount > 0, "Should visit function calls");
            Assert.True(counter.TableReferenceCount > 0, "Should visit table references");
            Assert.True(counter.QualifiedJoinCount > 0, "Should visit qualified joins");
            Assert.True(counter.ComparisonCount > 0, "Should visit comparisons");
        }

        [Fact]
        public void SqlWalker_VisitsSubqueryExpressions()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IN (SELECT id FROM S)");
            var counter = new NodeCounter();
            counter.Walk(stmt);

            Assert.True(counter.TableReferenceCount >= 2, "Should visit tables in both outer and subquery");
        }

        private class NodeCounter : SqlWalker
        {
            public int ColumnIdentifierCount { get; private set; }
            public int LiteralCount { get; private set; }
            public int FunctionCallCount { get; private set; }
            public int TableReferenceCount { get; private set; }
            public int QualifiedJoinCount { get; private set; }
            public int ComparisonCount { get; private set; }

            protected override void VisitColumnIdentifier(Expr.ColumnIdentifier expr) { ColumnIdentifierCount++; }
            protected override void VisitLiteral(Expr.Literal expr) { LiteralCount++; }
            protected override void VisitFunctionCall(Expr.FunctionCall expr) { FunctionCallCount++; base.VisitFunctionCall(expr); }
            protected override void VisitTableReference(TableReference source) { TableReferenceCount++; }
            protected override void VisitQualifiedJoin(QualifiedJoin source) { QualifiedJoinCount++; base.VisitQualifiedJoin(source); }
            protected override void VisitComparison(Predicate.Comparison pred) { ComparisonCount++; base.VisitComparison(pred); }
        }

        #endregion

        #region AST Mutability

        [Fact]
        public void Mutability_ReplaceLiteralWithVariable_UpdatesToSource()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 42");
            var where = (Predicate.Comparison)stmt.SelectExpression.Where;

            Assert.IsType<Expr.Literal>(where.Right);
            where.Right = new Expr.Variable("@P0");

            Assert.Equal("SELECT * FROM T WHERE x = @P0", stmt.ToSource());
        }

        [Fact]
        public void Mutability_ReplaceStringLiteralWithVariable_UpdatesToSource()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE name = 'hello'");
            var where = (Predicate.Comparison)stmt.SelectExpression.Where;

            Assert.IsType<Expr.Literal>(where.Right);
            Assert.Equal("hello", ((Expr.Literal)where.Right).Value);

            where.Right = new Expr.Variable("@P0");

            Assert.Equal("SELECT * FROM T WHERE name = @P0", stmt.ToSource());
        }

        [Fact]
        public void Mutability_NewLiteral_ProducesValidSource()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 1");
            var where = (Predicate.Comparison)stmt.SelectExpression.Where;

            where.Right = new Expr.Literal(99);

            Assert.Equal("SELECT * FROM T WHERE x = 99", stmt.ToSource());
        }

        #endregion

        #region LiteralParameterizer

        [Fact]
        public void LiteralParameterizer_ParameterizesIntLiteral()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 42");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE x = @P0", result.Sql);
            Assert.Single(result.Parameters);
            Assert.Equal(42, result.Parameters["@P0"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesStringLiteral()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE name = 'hello'");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE name = @P0", result.Sql);
            Assert.Single(result.Parameters);
            Assert.Equal("hello", result.Parameters["@P0"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesMultipleLiterals()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 42 AND y = 'hello'");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND y = @P1", result.Sql);
            Assert.Equal(2, result.Parameters.Count);
            Assert.Equal(42, result.Parameters["@P0"]);
            Assert.Equal("hello", result.Parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_SkipsNullLiteral()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IS NULL");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE x IS NULL", result.Sql);
            Assert.Empty(result.Parameters);
        }

        [Fact]
        public void LiteralParameterizer_AvoidsConflictWithExistingVariables()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = @P0 AND y = 42");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND y = @P1", result.Sql);
            Assert.Single(result.Parameters);
            Assert.Equal(42, result.Parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesLiteralsInFunctionArgs()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE LEN('test') > 0");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE LEN(@P0) > @P1", result.Sql);
            Assert.Equal(2, result.Parameters.Count);
            Assert.Equal("test", result.Parameters["@P0"]);
            Assert.Equal(0, result.Parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesLiteralsInBetween()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x BETWEEN 10 AND 20");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE x BETWEEN @P0 AND @P1", result.Sql);
            Assert.Equal(2, result.Parameters.Count);
            Assert.Equal(10, result.Parameters["@P0"]);
            Assert.Equal(20, result.Parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesLiteralsInInList()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IN (1, 2, 3)");
            var result = LiteralParameterizer.Parameterize(stmt);

            Assert.Equal("SELECT * FROM T WHERE x IN (@P0, @P1, @P2)", result.Sql);
            Assert.Equal(3, result.Parameters.Count);
            Assert.Equal(1, result.Parameters["@P0"]);
            Assert.Equal(2, result.Parameters["@P1"]);
            Assert.Equal(3, result.Parameters["@P2"]);
        }

        #endregion

        #region TableReferenceCollector

        [Fact]
        public void TableReferenceCollector_FindsSingleTable()
        {
            var stmt = ParseSelect("SELECT * FROM Customers");
            var collector = TableReferenceCollector.Collect(stmt);

            Assert.Single(collector.Tables);
            Assert.Equal("Customers", collector.Tables[0].TableName.ObjectName.Name);
            Assert.Empty(collector.Joins);
        }

        [Fact]
        public void TableReferenceCollector_FindsMultipleTables_WithJoin()
        {
            var stmt = ParseSelect("SELECT * FROM Orders INNER JOIN Customers ON Orders.CustId = Customers.Id");
            var collector = TableReferenceCollector.Collect(stmt);

            Assert.Equal(2, collector.Tables.Count);
            Assert.Single(collector.Joins);

            var tableNames = collector.Tables.Select(t => t.TableName.ObjectName.Name).OrderBy(n => n).ToList();
            Assert.Contains("Customers", tableNames);
            Assert.Contains("Orders", tableNames);
        }

        [Fact]
        public void TableReferenceCollector_FindsTablesInMultipleJoins()
        {
            var stmt = ParseSelect(
                "SELECT * FROM A INNER JOIN B ON A.id = B.id LEFT JOIN C ON B.id = C.id");
            var collector = TableReferenceCollector.Collect(stmt);

            Assert.Equal(3, collector.Tables.Count);
            Assert.Equal(2, collector.Joins.Count);
        }

        [Fact]
        public void TableReferenceCollector_FindsTablesInSubquery()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IN (SELECT id FROM S)");
            var collector = TableReferenceCollector.Collect(stmt);

            Assert.Equal(2, collector.Tables.Count);
            var tableNames = collector.Tables.Select(t => t.TableName.ObjectName.Name).OrderBy(n => n).ToList();
            Assert.Contains("S", tableNames);
            Assert.Contains("T", tableNames);
        }

        #endregion
    }
}
