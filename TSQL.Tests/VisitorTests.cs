using System.Collections.Generic;
using TSQL.AST;
using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class VisitorTests
    {
        #region Helper Methods

        private static Stmt.Select ParseSelect(string source)
        {
            Stmt stmt = Stmt.Parse(source);
            Assert.IsType<Stmt.Select>(stmt);
            return (Stmt.Select)stmt;
        }

        #endregion

        #region Expr.Visitor Accept Dispatch

        [Fact]
        public void ColumnIdentifier_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            var col = (SelectColumn)((SelectExpression)stmt.Query).Columns[0];
            var visitor = new ExprTypeRecorder();
            col.Expression.Accept(visitor);
            Assert.Equal("ColumnIdentifier", visitor.LastType);
        }

        [Fact]
        public void ObjectIdentifier_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var tableRef = (TableReference)((SelectExpression)stmt.Query).From.TableSources[0];
            var visitor = new ExprTypeRecorder();
            tableRef.TableName.Accept(visitor);
            Assert.Equal("ObjectIdentifier", visitor.LastType);
        }

        [Fact]
        public void Wildcard_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var wildcard = (Expr.Wildcard)((SelectExpression)stmt.Query).Columns[0];
            var visitor = new ExprTypeRecorder();
            wildcard.Accept(visitor);
            Assert.Equal("Wildcard", visitor.LastType);
        }

        [Fact]
        public void QualifiedWildcard_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT T.* FROM T");
            var qualWildcard = (Expr.QualifiedWildcard)((SelectExpression)stmt.Query).Columns[0];
            var visitor = new ExprTypeRecorder();
            qualWildcard.Accept(visitor);
            Assert.Equal("QualifiedWildcard", visitor.LastType);
        }

        private class ExprTypeRecorder : Expr.Visitor<object?>
        {
            public string? LastType { get; private set; }
            public object? VisitBinaryExpr(Expr.Binary expr) { LastType = "Binary"; return null; }
            public object? VisitStringLiteralExpr(Expr.StringLiteral expr) { LastType = "StringLiteral"; return null; }
            public object? VisitIntLiteralExpr(Expr.IntLiteral expr) { LastType = "IntLiteral"; return null; }
            public object? VisitDecimalLiteralExpr(Expr.DecimalLiteral expr) { LastType = "DecimalLiteral"; return null; }
            public object? VisitNullLiteralExpr(Expr.NullLiteral expr) { LastType = "NullLiteral"; return null; }
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
            public object? VisitIifExpr(Expr.Iif expr) { LastType = "Iif"; return null; }
            public object? VisitAtTimeZoneExpr(Expr.AtTimeZone expr) { LastType = "AtTimeZone"; return null; }
            public object? VisitOpenXmlExpr(Expr.OpenXmlExpression expr) { LastType = "OpenXml"; return null; }
        }

        #endregion

        #region TableSource.Visitor Accept Dispatch

        [Fact]
        public void TableReference_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var tableSource = ((SelectExpression)stmt.Query).From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("TableReference", visitor.LastType);
        }

        [Fact]
        public void QualifiedJoin_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM A INNER JOIN B ON A.id = B.id");
            var tableSource = ((SelectExpression)stmt.Query).From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("QualifiedJoin", visitor.LastType);
        }

        [Fact]
        public void CrossJoin_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM A CROSS JOIN B");
            var tableSource = ((SelectExpression)stmt.Query).From.TableSources[0];
            var visitor = new TableSourceTypeRecorder();
            tableSource.Accept(visitor);
            Assert.Equal("CrossJoin", visitor.LastType);
        }

        [Fact]
        public void SubqueryReference_Accept_DispatchesCorrectly()
        {
            var stmt = ParseSelect("SELECT * FROM (SELECT 1 AS x) AS sub");
            var tableSource = ((SelectExpression)stmt.Query).From.TableSources[0];
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
            protected override void VisitStringLiteral(Expr.StringLiteral expr) { LiteralCount++; }
            protected override void VisitIntLiteral(Expr.IntLiteral expr) { LiteralCount++; }
            protected override void VisitDecimalLiteral(Expr.DecimalLiteral expr) { LiteralCount++; }
            protected override void VisitNullLiteral(Expr.NullLiteral expr) { LiteralCount++; }
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
            var where = (Predicate.Comparison)((SelectExpression)stmt.Query).Where;

            Assert.IsType<Expr.IntLiteral>(where.Right);
            where.Right = new Expr.Variable("@P0");

            Assert.Equal("SELECT * FROM T WHERE x = @P0", stmt.ToSource());
        }

        [Fact]
        public void Mutability_ReplaceStringLiteralWithVariable_UpdatesToSource()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE name = 'hello'");
            var where = (Predicate.Comparison)((SelectExpression)stmt.Query).Where;

            Assert.IsType<Expr.StringLiteral>(where.Right);
            Assert.Equal("hello", ((Expr.StringLiteral)where.Right).Value);

            where.Right = new Expr.Variable("@P0");

            Assert.Equal("SELECT * FROM T WHERE name = @P0", stmt.ToSource());
        }

        [Fact]
        public void Mutability_NewLiteral_ProducesValidSource()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 1");
            var where = (Predicate.Comparison)((SelectExpression)stmt.Query).Where;

            where.Right = new Expr.IntLiteral(99);

            Assert.Equal("SELECT * FROM T WHERE x = 99", stmt.ToSource());
        }

        #endregion

        #region LiteralParameterizer

        [Fact]
        public void LiteralParameterizer_ParameterizesIntLiteral()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 42");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x = @P0", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(42, parameters["@P0"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesStringLiteral()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE name = 'hello'");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE name = @P0", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal("hello", parameters["@P0"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesMultipleLiterals()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = 42 AND y = 'hello'");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND y = @P1", stmt.ToSource());
            Assert.Equal(2, parameters.Count);
            Assert.Equal(42, parameters["@P0"]);
            Assert.Equal("hello", parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_SkipsNullLiteral()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IS NULL");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x IS NULL", stmt.ToSource());
            Assert.Empty(parameters);
        }

        [Fact]
        public void LiteralParameterizer_AvoidsConflictWithExistingVariables()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x = @P0 AND y = 42");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND y = @P1", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(42, parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesLiteralsInFunctionArgs()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE LEN('test') > 0");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE LEN(@P0) > @P1", stmt.ToSource());
            Assert.Equal(2, parameters.Count);
            Assert.Equal("test", parameters["@P0"]);
            Assert.Equal(0, parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesLiteralsInBetween()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x BETWEEN 10 AND 20");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x BETWEEN @P0 AND @P1", stmt.ToSource());
            Assert.Equal(2, parameters.Count);
            Assert.Equal(10, parameters["@P0"]);
            Assert.Equal(20, parameters["@P1"]);
        }

        [Fact]
        public void LiteralParameterizer_ParameterizesLiteralsInInList()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IN (1, 2, 3)");
            stmt.Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x IN (@P0, @P1, @P2)", stmt.ToSource());
            Assert.Equal(3, parameters.Count);
            Assert.Equal(1, parameters["@P0"]);
            Assert.Equal(2, parameters["@P1"]);
            Assert.Equal(3, parameters["@P2"]);
        }

        #endregion

        #region Fluent API

        [Fact]
        public void FluentApi_AddCondition_ReturnsSameInstance()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            var returned = stmt.AddCondition("Active = 1");

            Assert.Same(stmt, returned);
        }

        [Fact]
        public void FluentApi_Parameterize_ReturnsSameInstance()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = 1");
            var returned = stmt.Parameterize(out _);

            Assert.Same(stmt, returned);
        }

        [Fact]
        public void FluentApi_ChainAddConditionThenParameterize()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = 1")
                .AddCondition("y = 2")
                .Parameterize(out var parameters);

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND y = @P1", stmt.ToSource());
            Assert.Equal(2, parameters.Count);
            Assert.Equal(1, parameters["@P0"]);
            Assert.Equal(2, parameters["@P1"]);
        }

        [Fact]
        public void FluentApi_ChainMultipleAddConditions()
        {
            string sql = Stmt.Parse("SELECT * FROM T")
                .AddCondition("TenantId = 1")
                .AddCondition("Active = 1")
                .ToSource();

            Assert.Equal("SELECT * FROM T WHERE TenantId = 1 AND Active = 1", sql);
        }

        [Fact]
        public void FluentApi_TransformThenQuery()
        {
            var tables = Stmt.Parse("SELECT * FROM T")
                .AddCondition("Active = 1")
                .CollectTableReferences();

            Assert.Single(tables.Tables);
            Assert.Equal("T", tables.Tables[0].TableName.ObjectName.Name);
        }

        #endregion

        #region AddCondition Parameterized

        [Fact]
        public void AddConditionParameterized_UnnamedNoCollision()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            stmt.AddCondition("Active = @P0", out var parameters, new object[] { 1 });

            Assert.Equal("SELECT * FROM T WHERE Active = @P0", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(1, parameters["@P0"]);
        }

        [Fact]
        public void AddConditionParameterized_NamedValueTuple_NoCollision()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            stmt.AddCondition("TenantId = @TenantId", out var parameters,
                new object[] { ("@TenantId", 42) });

            Assert.Equal("SELECT * FROM T WHERE TenantId = @TenantId", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(42, parameters["@TenantId"]);
        }

        [Fact]
        public void AddConditionParameterized_NamedKeyValuePair()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            stmt.AddCondition("Status = @Status", out var parameters,
                new object[] { new KeyValuePair<string, object>("@Status", "active") });

            Assert.Equal("SELECT * FROM T WHERE Status = @Status", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal("active", parameters["@Status"]);
        }

        [Fact]
        public void AddConditionParameterized_CollisionWithExisting()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = @P0");
            stmt.AddCondition("Active = @P0", out var parameters, new object[] { 1 });

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND Active = @P0_1", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(1, parameters["@P0_1"]);
        }

        [Fact]
        public void AddConditionParameterized_MultipleParams_MixedCollisions()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = @P0");
            stmt.AddCondition("a = @P0 AND b = @P1", out var parameters,
                new object[] { ("@P0", 10), ("@P1", 20) });

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND a = @P0_1 AND b = @P1", stmt.ToSource());
            Assert.Equal(2, parameters.Count);
            Assert.Equal(10, parameters["@P0_1"]);
            Assert.Equal(20, parameters["@P1"]);
        }

        [Fact]
        public void AddConditionParameterized_WithWhereClauseTargetAll()
        {
            var stmt = Stmt.Parse(
                "SELECT * FROM T UNION ALL SELECT * FROM S");
            stmt.AddCondition("Active = @P0", out var parameters,
                new object[] { 1 }, WhereClauseTarget.All);

            Assert.Contains("T WHERE Active = @P0", stmt.ToSource());
            Assert.Contains("S WHERE Active = @P0", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(1, parameters["@P0"]);
        }

        [Fact]
        public void AddConditionParameterized_SameVariableTwiceInCondition()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = @P0");
            stmt.AddCondition("a = @P0 OR b = @P0", out var parameters,
                new object[] { ("@P0", 99) });

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND (a = @P0_1 OR b = @P0_1)", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(99, parameters["@P0_1"]);
        }

        [Fact]
        public void AddConditionParameterized_ConditionVarNotInValues()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = @Existing");
            stmt.AddCondition("a = @Existing AND b = @New", out var parameters,
                new object[] { ("@New", 5) });

            Assert.Equal("SELECT * FROM T WHERE x = @Existing AND a = @Existing AND b = @New", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(5, parameters["@New"]);
        }

        [Fact]
        public void AddConditionParameterized_EmptyValuesArray()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            stmt.AddCondition("Active = 1", out var parameters, Array.Empty<object>());

            Assert.Equal("SELECT * FROM T WHERE Active = 1", stmt.ToSource());
            Assert.Empty(parameters);
        }

        [Fact]
        public void AddConditionParameterized_ChainThenParameterize()
        {
            var stmt = Stmt.Parse("SELECT * FROM T WHERE x = 1")
                .AddCondition("TenantId = @TenantId", out var p1,
                    new object[] { ("@TenantId", 42) })
                .Parameterize(out var p2);

            Assert.Equal("SELECT * FROM T WHERE x = @P0 AND TenantId = @TenantId", stmt.ToSource());
            Assert.Single(p1);
            Assert.Equal(42, p1["@TenantId"]);
            Assert.Single(p2);
            Assert.Equal(1, p2["@P0"]);
        }

        [Fact]
        public void AddConditionParameterized_ChainTwoAddConditions()
        {
            var stmt = Stmt.Parse("SELECT * FROM T")
                .AddCondition("TenantId = @TenantId", out var p1,
                    new object[] { ("@TenantId", 1) })
                .AddCondition("Active = @Active", out var p2,
                    new object[] { ("@Active", true) });

            Assert.Equal("SELECT * FROM T WHERE TenantId = @TenantId AND Active = @Active", stmt.ToSource());
            Assert.Single(p1);
            Assert.Equal(1, p1["@TenantId"]);
            Assert.Single(p2);
            Assert.Equal(true, p2["@Active"]);
        }

        [Fact]
        public void AddConditionParameterized_ErrorMissingAtPrefix()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            Assert.Throws<ArgumentException>(() =>
                stmt.AddCondition("x = @P0", out _, new object[] { ("P0", 1) }));
        }

        [Fact]
        public void AddConditionParameterized_ParamNotInCondition_SilentlyIgnored()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            stmt.AddCondition("Active = @P0", out var parameters,
                new object[] { ("@P0", 1), ("@Extra", 99) });

            Assert.Equal("SELECT * FROM T WHERE Active = @P0", stmt.ToSource());
            Assert.Single(parameters);
            Assert.Equal(1, parameters["@P0"]);
        }

        [Fact]
        public void AddConditionParameterized_ErrorDuplicateNames()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            Assert.Throws<ArgumentException>(() =>
                stmt.AddCondition("x = @P0", out _,
                    new object[] { ("@P0", 1), ("@P0", 2) }));
        }

        [Fact]
        public void AddConditionParameterized_ReturnsSameInstance()
        {
            var stmt = Stmt.Parse("SELECT * FROM T");
            var returned = stmt.AddCondition("Active = @P0", out _, new object[] { 1 });

            Assert.Same(stmt, returned);
        }

        #endregion

        #region TableReferenceCollector

        [Fact]
        public void TableReferenceCollector_FindsSingleTable()
        {
            var stmt = ParseSelect("SELECT * FROM Customers");
            var refs = stmt.CollectTableReferences();

            Assert.Single(refs.Tables);
            Assert.Equal("Customers", refs.Tables[0].TableName.ObjectName.Name);
            Assert.Empty(refs.Joins);
        }

        [Fact]
        public void TableReferenceCollector_FindsMultipleTables_WithJoin()
        {
            var stmt = ParseSelect("SELECT * FROM Orders INNER JOIN Customers ON Orders.CustId = Customers.Id");
            var refs = stmt.CollectTableReferences();

            Assert.Equal(2, refs.Tables.Count);
            Assert.Single(refs.Joins);

            var tableNames = refs.Tables.Select(t => t.TableName.ObjectName.Name).OrderBy(n => n).ToList();
            Assert.Contains("Customers", tableNames);
            Assert.Contains("Orders", tableNames);
        }

        [Fact]
        public void TableReferenceCollector_FindsTablesInMultipleJoins()
        {
            var stmt = ParseSelect(
                "SELECT * FROM A INNER JOIN B ON A.id = B.id LEFT JOIN C ON B.id = C.id");
            var refs = stmt.CollectTableReferences();

            Assert.Equal(3, refs.Tables.Count);
            Assert.Equal(2, refs.Joins.Count);
        }

        [Fact]
        public void TableReferenceCollector_FindsTablesInSubquery()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IN (SELECT id FROM S)");
            var refs = stmt.CollectTableReferences();

            Assert.Equal(2, refs.Tables.Count);
            var tableNames = refs.Tables.Select(t => t.TableName.ObjectName.Name).OrderBy(n => n).ToList();
            Assert.Contains("S", tableNames);
            Assert.Contains("T", tableNames);
        }

        #endregion

        #region ColumnReferenceCollector

        [Fact]
        public void ColumnReferenceCollector_FindsSingleUnqualifiedColumn()
        {
            var stmt = ParseSelect("SELECT a FROM T");
            var refs = stmt.CollectColumnReferences();

            Assert.Single(refs);
            Assert.Equal("a", refs[0].ColumnName.Name);
        }

        [Fact]
        public void ColumnReferenceCollector_FindsMultipleQualifiedColumns()
        {
            var stmt = ParseSelect("SELECT T.a, T.b FROM T");
            var refs = stmt.CollectColumnReferences();

            Assert.Equal(2, refs.Count);
            Assert.All(refs, r => Assert.NotNull(r.ObjectName));
        }

        [Fact]
        public void ColumnReferenceCollector_FindsColumnsInAllClauses()
        {
            var stmt = ParseSelect(
                "SELECT a FROM A INNER JOIN B ON A.id = B.id WHERE x = 1 ORDER BY a");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.All);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("a", names);
            Assert.Contains("id", names);
            Assert.Contains("x", names);
            Assert.True(refs.Count >= 5, "Should find columns in SELECT, ON, WHERE, and ORDER BY");
        }

        [Fact]
        public void ColumnReferenceCollector_FindsColumnsInsideSubqueries()
        {
            var stmt = ParseSelect("SELECT * FROM T WHERE x IN (SELECT id FROM S)");
            var refs = stmt.CollectColumnReferences(
                scope: ColumnReferenceScope.All,
                clauses: ColumnReferenceClause.All);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("x", names);
            Assert.Contains("id", names);
        }

        [Fact]
        public void ColumnReferenceCollector_WildcardProducesNoColumnRefs()
        {
            var stmt = ParseSelect("SELECT * FROM T");
            var refs = stmt.CollectColumnReferences();

            Assert.Empty(refs);
        }

        #endregion

        #region ColumnReferenceCollector Scope Tests

        [Fact]
        public void ColumnReferenceCollector_DefaultScope_ExcludesSubqueries()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x IN (SELECT id FROM S)");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.All);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("a", names);
            Assert.Contains("x", names);
            Assert.DoesNotContain("id", names);
        }

        [Fact]
        public void ColumnReferenceCollector_SubqueriesOnly_CollectsOnlySubqueryColumns()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x IN (SELECT id FROM S)");
            var refs = stmt.CollectColumnReferences(
                scope: ColumnReferenceScope.Subqueries,
                clauses: ColumnReferenceClause.All);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("id", names);
            Assert.DoesNotContain("a", names);
            Assert.DoesNotContain("x", names);
        }

        [Fact]
        public void ColumnReferenceCollector_CtesOnly_CollectsOnlyCteColumns()
        {
            var stmt = ParseSelect("WITH cte AS (SELECT id FROM S) SELECT a FROM cte");
            var refs = stmt.CollectColumnReferences(
                scope: ColumnReferenceScope.Ctes,
                clauses: ColumnReferenceClause.All);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("id", names);
            Assert.DoesNotContain("a", names);
        }

        [Fact]
        public void ColumnReferenceCollector_ScopeNone_ReturnsEmpty()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x = 1");
            var refs = stmt.CollectColumnReferences(
                scope: ColumnReferenceScope.None,
                clauses: ColumnReferenceClause.All);

            Assert.Empty(refs);
        }

        #endregion

        #region ColumnReferenceCollector Clause Tests

        [Fact]
        public void ColumnReferenceCollector_SelectClauseOnly()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x = 1");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.Select);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("a", names);
            Assert.DoesNotContain("x", names);
        }

        [Fact]
        public void ColumnReferenceCollector_WhereClauseOnly()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x = 1");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.Where);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("x", names);
            Assert.DoesNotContain("a", names);
        }

        [Fact]
        public void ColumnReferenceCollector_FromClauseOnly_IncludesJoinConditions()
        {
            var stmt = ParseSelect("SELECT a FROM A INNER JOIN B ON A.id = B.id");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.From);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Equal(2, refs.Count);
            Assert.All(names, n => Assert.Equal("id", n));
            Assert.DoesNotContain("a", names);
        }

        [Fact]
        public void ColumnReferenceCollector_OrderByClauseOnly()
        {
            var stmt = ParseSelect("SELECT a FROM T ORDER BY b");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.OrderBy);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Single(refs);
            Assert.Contains("b", names);
            Assert.DoesNotContain("a", names);
        }

        [Fact]
        public void ColumnReferenceCollector_CombinedClauses()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x = 1 ORDER BY b");
            var refs = stmt.CollectColumnReferences(
                clauses: ColumnReferenceClause.Select | ColumnReferenceClause.Where);

            var names = refs.Select(r => r.ColumnName.Name).ToList();
            Assert.Contains("a", names);
            Assert.Contains("x", names);
            Assert.DoesNotContain("b", names);
        }

        [Fact]
        public void ColumnReferenceCollector_ClauseNone_ReturnsEmpty()
        {
            var stmt = ParseSelect("SELECT a FROM T WHERE x = 1");
            var refs = stmt.CollectColumnReferences(clauses: ColumnReferenceClause.None);

            Assert.Empty(refs);
        }

        #endregion

        #region TempTableReplacer

        [Fact]
        public void TempTableReplacer_QualifiedColumns_SpecificColumnsInSelectInto()
        {
            var stmt = Stmt.Parse("SELECT u.Name, u.Email FROM dbo.Users u WHERE u.Age > 25");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Name, Email, Age INTO #Users FROM dbo.Users;\n" +
                "SELECT u.Name, u.Email FROM #Users u WHERE u.Age > 25",
                result);
        }

        [Fact]
        public void TempTableReplacer_SchemaQualifiedTable_PreservesFullQualification()
        {
            var stmt = Stmt.Parse("SELECT u.Id FROM dbo.Users u");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Id INTO #Users FROM dbo.Users;\n" +
                "SELECT u.Id FROM #Users u",
                result);
        }

        [Fact]
        public void TempTableReplacer_BracketedIdentifiers_CaseInsensitiveMatch()
        {
            var stmt = Stmt.Parse("SELECT u.Name FROM [dbo].[Users] u");
            string result = stmt.ReplaceWithTempTables("users").ToSource();

            Assert.Equal(
                "SELECT Name INTO #Users FROM [dbo].[Users];\n" +
                "SELECT u.Name FROM #Users u",
                result);
        }

        [Fact]
        public void TempTableReplacer_JoinMultipleTables_EachGetsOwnSelectInto()
        {
            var stmt = Stmt.Parse(
                "SELECT u.Name, o.Total FROM Users u INNER JOIN Orders o ON u.Id = o.UserId");
            string result = stmt.ReplaceWithTempTables("Users", "Orders").ToSource();

            Assert.Equal(
                "SELECT Name, Id INTO #Users FROM Users;\n" +
                "SELECT Total, UserId INTO #Orders FROM Orders;\n" +
                "SELECT u.Name, o.Total FROM #Users u INNER JOIN #Orders o ON u.Id = o.UserId",
                result);
        }

        [Fact]
        public void TempTableReplacer_SelfJoin_OneSelectInto()
        {
            var stmt = Stmt.Parse(
                "SELECT a.Name, b.Name FROM Users a INNER JOIN Users b ON a.Id = b.ParentId");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Name, Id, ParentId INTO #Users FROM Users;\n" +
                "SELECT a.Name, b.Name FROM #Users a INNER JOIN #Users b ON a.Id = b.ParentId",
                result);
        }

        [Fact]
        public void TempTableReplacer_BareStar_SelectStarInSelectInto()
        {
            var stmt = Stmt.Parse("SELECT * FROM Users");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT * INTO #Users FROM Users;\n" +
                "SELECT * FROM #Users",
                result);
        }

        [Fact]
        public void TempTableReplacer_QualifiedWildcard_SelectStarForThatTable()
        {
            var stmt = Stmt.Parse("SELECT u.* FROM Users u");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT * INTO #Users FROM Users;\n" +
                "SELECT u.* FROM #Users u",
                result);
        }

        [Fact]
        public void TempTableReplacer_UnqualifiedColumns_SelectStarFallback()
        {
            var stmt = Stmt.Parse("SELECT Name, Email FROM Users");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT * INTO #Users FROM Users;\n" +
                "SELECT Name, Email FROM #Users",
                result);
        }

        [Fact]
        public void TempTableReplacer_ColumnsFromAllClauses_IncludedInSelectInto()
        {
            var stmt = Stmt.Parse(
                "SELECT u.Name FROM Users u WHERE u.Age > 25 ORDER BY u.CreatedAt");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Name, Age, CreatedAt INTO #Users FROM Users;\n" +
                "SELECT u.Name FROM #Users u WHERE u.Age > 25 ORDER BY u.CreatedAt",
                result);
        }

        [Fact]
        public void TempTableReplacer_TableHints_PreservedOnTempTable()
        {
            var stmt = Stmt.Parse("SELECT u.Name FROM Users u WITH (NOLOCK)");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Name INTO #Users FROM Users;\n" +
                "SELECT u.Name FROM #Users u WITH (NOLOCK)",
                result);
        }

        [Fact]
        public void TempTableReplacer_NoMatch_ReturnsOriginalSource()
        {
            var stmt = Stmt.Parse("SELECT * FROM Users");
            string result = stmt.ReplaceWithTempTables("Products").ToSource();

            Assert.Equal("SELECT * FROM Users", result);
        }

        [Fact]
        public void TempTableReplacer_EmptyNames_ReturnsOriginalSource()
        {
            var stmt = Stmt.Parse("SELECT * FROM Users");
            string result = stmt.ReplaceWithTempTables().ToSource();

            Assert.Equal("SELECT * FROM Users", result);
        }

        [Fact]
        public void TempTableReplacer_CteMatched_MaterializedAndRemoved()
        {
            var stmt = Stmt.Parse("WITH cte AS (SELECT * FROM T) SELECT * FROM cte");
            string result = stmt.ReplaceWithTempTables("cte").ToSource();

            Assert.Equal(
                "WITH cte AS (SELECT * FROM T) SELECT * INTO #cte FROM cte;\n" +
                "SELECT * FROM #cte",
                result);
        }

        [Fact]
        public void TempTableReplacer_CteWithPrerequisites_IncludesPrerequisitesInSelectInto()
        {
            var stmt = Stmt.Parse(
                "WITH A AS (SELECT * FROM T), B AS (SELECT * FROM A WHERE x > 1) SELECT * FROM B");
            string result = stmt.ReplaceWithTempTables("B").ToSource();

            Assert.Equal(
                "WITH A AS (SELECT * FROM T), B AS (SELECT * FROM A WHERE x > 1) SELECT * INTO #B FROM B;\n" +
                "WITH A AS (SELECT * FROM T) SELECT * FROM #B",
                result);
        }

        [Fact]
        public void TempTableReplacer_MixedCteAndRegularTable()
        {
            var stmt = Stmt.Parse(
                "WITH cte AS (SELECT t.Id FROM TableA t) SELECT c.Id FROM cte c JOIN TableB b ON c.Id = b.CteId");
            string result = stmt.ReplaceWithTempTables("cte", "TableA").ToSource();

            Assert.Equal(
                "SELECT Id INTO #TableA FROM TableA;\n" +
                "WITH cte AS (SELECT t.Id FROM #TableA t) SELECT * INTO #cte FROM cte;\n" +
                "SELECT c.Id FROM #cte c JOIN TableB b ON c.Id = b.CteId",
                result);
        }

        [Fact]
        public void TempTableReplacer_AllCtesRemoved_NoWithClause()
        {
            var stmt = Stmt.Parse(
                "WITH A AS (SELECT * FROM T), B AS (SELECT * FROM A) SELECT * FROM B");
            string result = stmt.ReplaceWithTempTables("A", "B").ToSource();

            Assert.Equal(
                "WITH A AS (SELECT * FROM T) SELECT * INTO #A FROM A;\n" +
                "WITH A AS (SELECT * FROM T), B AS (SELECT * FROM #A) SELECT * INTO #B FROM B;\n" +
                "SELECT * FROM #B",
                result);
        }

        [Fact]
        public void TempTableReplacer_PartialCteRemoval_RemainingCtesKept()
        {
            var stmt = Stmt.Parse(
                "WITH A AS (SELECT * FROM T), B AS (SELECT * FROM A) SELECT * FROM A CROSS JOIN B");
            string result = stmt.ReplaceWithTempTables("B").ToSource();

            Assert.Equal(
                "WITH A AS (SELECT * FROM T), B AS (SELECT * FROM A) SELECT * INTO #B FROM B;\n" +
                "WITH A AS (SELECT * FROM T) SELECT * FROM A CROSS JOIN #B",
                result);
        }

        [Fact]
        public void TempTableReplacer_ColumnsFromJoinOn_IncludedInSelectInto()
        {
            var stmt = Stmt.Parse(
                "SELECT u.Name FROM Users u INNER JOIN Orders o ON u.Id = o.UserId WHERE o.Total > 100");
            string result = stmt.ReplaceWithTempTables("Users", "Orders").ToSource();

            Assert.Equal(
                "SELECT Name, Id INTO #Users FROM Users;\n" +
                "SELECT UserId, Total INTO #Orders FROM Orders;\n" +
                "SELECT u.Name FROM #Users u INNER JOIN #Orders o ON u.Id = o.UserId WHERE o.Total > 100",
                result);
        }

        [Fact]
        public void TempTableReplacer_DerivedTableAlias_MaterializedAndReplaced()
        {
            var stmt = Stmt.Parse("SELECT * FROM (SELECT Id, Name FROM Users) AS T");
            string result = stmt.ReplaceWithTempTables("T").ToSource();

            Assert.Equal(
                "SELECT Id, Name INTO #T FROM Users;\n" +
                "SELECT * FROM #T",
                result);
        }

        [Fact]
        public void TempTableReplacer_DerivedTableUnion_WrappedWithSelectInto()
        {
            var stmt = Stmt.Parse("SELECT * FROM (SELECT 1 AS x UNION SELECT 2 AS x) AS T");
            string result = stmt.ReplaceWithTempTables("T").ToSource();

            Assert.Equal(
                "SELECT * INTO #T FROM (SELECT 1 AS x UNION SELECT 2 AS x);\n" +
                "SELECT * FROM #T",
                result);
        }

        [Fact]
        public void TempTableReplacer_RowsetFunctionAlias_MaterializedAndReplaced()
        {
            var stmt = Stmt.Parse("SELECT * FROM OPENQUERY(LinkedServer, 'SELECT 1') AS T");
            string result = stmt.ReplaceWithTempTables("T").ToSource();

            Assert.Equal(
                "SELECT * INTO #T FROM OPENQUERY(LinkedServer, 'SELECT 1');\n" +
                "SELECT * FROM #T",
                result);
        }

        [Fact]
        public void TempTableReplacer_CrossApplySubquery_OnlyRegularTableReplaced()
        {
            var stmt = Stmt.Parse(
                "SELECT u.Name, d.Detail FROM Users u CROSS APPLY (SELECT TOP 1 Detail FROM Details WHERE Details.UserId = u.Id) d");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Name, Id INTO #Users FROM Users;\n" +
                "SELECT u.Name, d.Detail FROM #Users u CROSS APPLY (SELECT TOP 1 Detail FROM Details WHERE Details.UserId = u.Id) d",
                result);
        }

        [Fact]
        public void TempTableReplacer_RowsetFunctionWithRegularTable_ColumnsStaySeparate()
        {
            var stmt = Stmt.Parse(
                "SELECT u.Name, oq.Val FROM Users u INNER JOIN OPENQUERY(LinkedServer, 'SELECT Val FROM T') AS oq ON u.Id = oq.Id");
            string result = stmt.ReplaceWithTempTables("Users").ToSource();

            Assert.Equal(
                "SELECT Name, Id INTO #Users FROM Users;\n" +
                "SELECT u.Name, oq.Val FROM #Users u INNER JOIN OPENQUERY(LinkedServer, 'SELECT Val FROM T') AS oq ON u.Id = oq.Id",
                result);
        }

        [Fact]
        public void TempTableReplacer_TvfMatchedByFunctionName_MaterializedAndReplaced()
        {
            var stmt = Stmt.Parse(
                "SELECT f.Val FROM Users u INNER JOIN TableValuedFunction(1, 2) AS f ON u.Id = f.Id");
            string result = stmt.ReplaceWithTempTables("TableValuedFunction").ToSource();

            Assert.Equal(
                "SELECT * INTO #TableValuedFunction FROM TableValuedFunction(1, 2);\n" +
                "SELECT f.Val FROM Users u INNER JOIN #TableValuedFunction AS f ON u.Id = f.Id",
                result);
        }

        [Fact]
        public void TempTableReplacer_TvfWithoutAlias_MaterializedAndReplaced()
        {
            var stmt = Stmt.Parse("SELECT * FROM TableValuedFunction(1, 2)");
            string result = stmt.ReplaceWithTempTables("TableValuedFunction").ToSource();

            Assert.Equal(
                "SELECT * INTO #TableValuedFunction FROM TableValuedFunction(1, 2);\n" +
                "SELECT * FROM #TableValuedFunction",
                result);
        }

        [Fact]
        public void TempTableReplacer_SchemaQualifiedTvf_MaterializedAndReplaced()
        {
            var stmt = Stmt.Parse("SELECT f.Val FROM dbo.MyFunction(1, 2) AS f");
            string result = stmt.ReplaceWithTempTables("MyFunction").ToSource();

            Assert.Equal(
                "SELECT * INTO #MyFunction FROM dbo.MyFunction(1, 2);\n" +
                "SELECT f.Val FROM #MyFunction AS f",
                result);
        }

        [Fact]
        public void TempTableReplacer_TvfAndRegularTable_BothReplaced()
        {
            var stmt = Stmt.Parse(
                "SELECT u.Name, f.Val FROM Users u INNER JOIN GetDetails(u.Id) AS f ON u.Id = f.Id");
            string result = stmt.ReplaceWithTempTables("Users", "GetDetails").ToSource();

            Assert.Equal(
                "SELECT Name, Id INTO #Users FROM Users;\n" +
                "SELECT * INTO #GetDetails FROM GetDetails(u.Id);\n" +
                "SELECT u.Name, f.Val FROM #Users u INNER JOIN #GetDetails AS f ON u.Id = f.Id",
                result);
        }

        #endregion
    }
}

