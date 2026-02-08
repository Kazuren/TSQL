using System.Collections.Generic;
using TSQL.AST;

namespace TSQL
{
    /// <summary>
    /// Base class for recursive AST walkers. Override specific Visit methods
    /// to handle nodes of interest. Default implementations walk into children.
    /// Call base.VisitXxx() to continue walking into child nodes.
    /// </summary>
    public class SqlWalker :
        Expr.Visitor<object>,
        Predicate.Visitor<object>,
        TableSource.Visitor<object>,
        Stmt.Visitor<object>
    {
        #region Public Entry Points

        public void Walk(Stmt stmt)
        {
            stmt.Accept((Stmt.Visitor<object>)this);
        }

        public void Walk(Expr expr)
        {
            expr.Accept((Expr.Visitor<object>)this);
        }

        public void Walk(Predicate predicate)
        {
            predicate.Accept((Predicate.Visitor<object>)this);
        }

        public void Walk(TableSource tableSource)
        {
            tableSource.Accept((TableSource.Visitor<object>)this);
        }

        #endregion

        #region Stmt Visit Methods

        protected virtual void VisitSelect(Stmt.Select stmt)
        {
            if (stmt.CteStmt != null)
            {
                foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                {
                    Walk(cte.Query);
                }
            }
            WalkSelectExpression(stmt.SelectExpression);
        }

        #endregion

        #region Expr Visit Methods

        protected virtual void VisitBinary(Expr.Binary expr)
        {
            Walk(expr.Left);
            Walk(expr.Right);
        }

        protected virtual void VisitLiteral(Expr.Literal expr) { }

        protected virtual void VisitColumnIdentifier(Expr.ColumnIdentifier expr) { }

        protected virtual void VisitObjectIdentifier(Expr.ObjectIdentifier expr) { }

        protected virtual void VisitWildcard(Expr.Wildcard expr) { }

        protected virtual void VisitQualifiedWildcard(Expr.QualifiedWildcard expr) { }

        protected virtual void VisitUnary(Expr.Unary expr)
        {
            Walk(expr.Right);
        }

        protected virtual void VisitGrouping(Expr.Grouping expr)
        {
            Walk(expr.Expression);
        }

        protected virtual void VisitSubquery(Expr.Subquery expr)
        {
            WalkSelectExpression(expr.SelectExpression);
        }

        protected virtual void VisitFunctionCall(Expr.FunctionCall expr)
        {
            foreach (Expr arg in expr.Arguments)
            {
                Walk(arg);
            }
        }

        protected virtual void VisitVariable(Expr.Variable expr) { }

        protected virtual void VisitWindowFunction(Expr.WindowFunction expr)
        {
            VisitFunctionCall(expr.Function);
            if (expr.Over.PartitionBy != null)
            {
                foreach (Expr partExpr in expr.Over.PartitionBy)
                {
                    Walk(partExpr);
                }
            }
            if (expr.Over.OrderBy != null)
            {
                foreach (OrderByItem orderItem in expr.Over.OrderBy)
                {
                    Walk(orderItem.Expression);
                }
            }
        }

        protected virtual void VisitSimpleCase(Expr.SimpleCase expr)
        {
            Walk(expr.Operand);
            foreach (Expr.SimpleCaseWhen when in expr.WhenClauses)
            {
                Walk(when.Value);
                Walk(when.Result);
            }
            if (expr.ElseResult != null)
            {
                Walk(expr.ElseResult);
            }
        }

        protected virtual void VisitSearchedCase(Expr.SearchedCase expr)
        {
            foreach (Expr.SearchedCaseWhen when in expr.WhenClauses)
            {
                Walk(when.Condition);
                Walk(when.Result);
            }
            if (expr.ElseResult != null)
            {
                Walk(expr.ElseResult);
            }
        }

        protected virtual void VisitCast(Expr.CastExpression expr)
        {
            Walk(expr.Expression);
        }

        protected virtual void VisitConvert(Expr.ConvertExpression expr)
        {
            Walk(expr.Expression);
            if (expr.Style != null)
            {
                Walk(expr.Style);
            }
        }

        #endregion

        #region Predicate Visit Methods

        protected virtual void VisitComparison(Predicate.Comparison pred)
        {
            Walk(pred.Left);
            Walk(pred.Right);
        }

        protected virtual void VisitLike(Predicate.Like pred)
        {
            Walk(pred.Left);
            Walk(pred.Pattern);
            if (pred.EscapeExpr != null)
            {
                Walk(pred.EscapeExpr);
            }
        }

        protected virtual void VisitBetween(Predicate.Between pred)
        {
            Walk(pred.Expr);
            Walk(pred.LowRangeExpr);
            Walk(pred.HighRangeExpr);
        }

        protected virtual void VisitNull(Predicate.Null pred)
        {
            Walk(pred.Expr);
        }

        protected virtual void VisitContains(Predicate.Contains pred)
        {
            Walk(pred.Column);
            Walk(pred.SearchCondition);
        }

        protected virtual void VisitIn(Predicate.In pred)
        {
            Walk(pred.Expr);
            if (pred.Subquery != null)
            {
                Walk(pred.Subquery);
            }
            else if (pred.ValueList != null)
            {
                foreach (Expr expr in pred.ValueList)
                {
                    Walk(expr);
                }
            }
        }

        protected virtual void VisitQuantifier(Predicate.Quantifier pred)
        {
            Walk(pred.Left);
            Walk(pred.Subquery);
        }

        protected virtual void VisitExists(Predicate.Exists pred)
        {
            Walk(pred.Subquery);
        }

        protected virtual void VisitPredicateGrouping(Predicate.Grouping pred)
        {
            Walk(pred.Predicate);
        }

        protected virtual void VisitAnd(Predicate.And pred)
        {
            Walk(pred.Left);
            Walk(pred.Right);
        }

        protected virtual void VisitOr(Predicate.Or pred)
        {
            Walk(pred.Left);
            Walk(pred.Right);
        }

        protected virtual void VisitNot(Predicate.Not pred)
        {
            Walk(pred.Predicate);
        }

        #endregion

        #region TableSource Visit Methods

        protected virtual void VisitTableReference(TableReference source) { }

        protected virtual void VisitSubqueryReference(SubqueryReference source)
        {
            Walk(source.Subquery);
        }

        protected virtual void VisitTableVariableReference(TableVariableReference source) { }

        protected virtual void VisitQualifiedJoin(QualifiedJoin source)
        {
            Walk(source.Left);
            Walk(source.Right);
            Walk(source.OnCondition);
        }

        protected virtual void VisitCrossJoin(CrossJoin source)
        {
            Walk(source.Left);
            Walk(source.Right);
        }

        protected virtual void VisitApplyJoin(ApplyJoin source)
        {
            Walk(source.Left);
            Walk(source.Right);
        }

        protected virtual void VisitParenthesizedTableSource(ParenthesizedTableSource source)
        {
            Walk(source.Inner);
        }

        protected virtual void VisitPivotTableSource(PivotTableSource source)
        {
            Walk(source.Source);
        }

        protected virtual void VisitUnpivotTableSource(UnpivotTableSource source)
        {
            Walk(source.Source);
        }

        protected virtual void VisitValuesTableSource(ValuesTableSource source)
        {
            foreach (ValuesRow row in source.Rows)
            {
                foreach (Expr val in row.Values)
                {
                    Walk(val);
                }
            }
        }

        protected virtual void VisitRowsetFunctionReference(RowsetFunctionReference source)
        {
            Walk(source.FunctionCall);
        }

        #endregion

        #region Helper Methods

        protected void WalkSelectExpression(SelectExpression selectExpr)
        {
            if (selectExpr.Top != null)
            {
                Walk(selectExpr.Top.Expression);
            }

            foreach (SelectItem item in selectExpr.Columns)
            {
                if (item is SelectColumn col)
                {
                    Walk(col.Expression);
                }
                else if (item is Expr.Wildcard wildcard)
                {
                    Walk(wildcard);
                }
                else if (item is Expr.QualifiedWildcard qualifiedWildcard)
                {
                    Walk(qualifiedWildcard);
                }
            }

            if (selectExpr.From != null)
            {
                foreach (TableSource tableSource in selectExpr.From.TableSources)
                {
                    Walk(tableSource);
                }
            }

            if (selectExpr.Where != null)
            {
                Walk(selectExpr.Where);
            }

            if (selectExpr.GroupBy != null)
            {
                WalkGroupByItems(selectExpr.GroupBy.Items);
            }

            if (selectExpr.Having != null)
            {
                Walk(selectExpr.Having);
            }

            if (selectExpr.OrderBy != null)
            {
                foreach (OrderByItem item in selectExpr.OrderBy)
                {
                    Walk(item.Expression);
                }
            }
        }

        private void WalkGroupByItems(SyntaxElementList<GroupByItem> items)
        {
            foreach (GroupByItem item in items)
            {
                if (item is GroupByExpression groupByExpr)
                {
                    Walk(groupByExpr.Expression);
                }
                else if (item is GroupByRollup rollup)
                {
                    WalkGroupByItems(rollup.Items);
                }
                else if (item is GroupByCube cube)
                {
                    WalkGroupByItems(cube.Items);
                }
                else if (item is GroupByGroupingSets sets)
                {
                    WalkGroupByItems(sets.Items);
                }
                else if (item is GroupByComposite composite)
                {
                    foreach (Expr expr in composite.Expressions)
                    {
                        Walk(expr);
                    }
                }
            }
        }

        #endregion

        #region Explicit Interface Implementations

        object Stmt.Visitor<object>.VisitSelectStmt(Stmt.Select stmt) { VisitSelect(stmt); return null; }

        object Expr.Visitor<object>.VisitBinaryExpr(Expr.Binary expr) { VisitBinary(expr); return null; }
        object Expr.Visitor<object>.VisitLiteralExpr(Expr.Literal expr) { VisitLiteral(expr); return null; }
        object Expr.Visitor<object>.VisitColumnIdentifierExpr(Expr.ColumnIdentifier expr) { VisitColumnIdentifier(expr); return null; }
        object Expr.Visitor<object>.VisitObjectIdentifierExpr(Expr.ObjectIdentifier expr) { VisitObjectIdentifier(expr); return null; }
        object Expr.Visitor<object>.VisitWildcardExpr(Expr.Wildcard expr) { VisitWildcard(expr); return null; }
        object Expr.Visitor<object>.VisitQualifiedWildcardExpr(Expr.QualifiedWildcard expr) { VisitQualifiedWildcard(expr); return null; }
        object Expr.Visitor<object>.VisitUnaryExpr(Expr.Unary expr) { VisitUnary(expr); return null; }
        object Expr.Visitor<object>.VisitGroupingExpr(Expr.Grouping expr) { VisitGrouping(expr); return null; }
        object Expr.Visitor<object>.VisitSubqueryExpr(Expr.Subquery expr) { VisitSubquery(expr); return null; }
        object Expr.Visitor<object>.VisitFunctionCallExpr(Expr.FunctionCall expr) { VisitFunctionCall(expr); return null; }
        object Expr.Visitor<object>.VisitVariableExpr(Expr.Variable expr) { VisitVariable(expr); return null; }
        object Expr.Visitor<object>.VisitWindowFunctionExpr(Expr.WindowFunction expr) { VisitWindowFunction(expr); return null; }
        object Expr.Visitor<object>.VisitSimpleCaseExpr(Expr.SimpleCase expr) { VisitSimpleCase(expr); return null; }
        object Expr.Visitor<object>.VisitSearchedCaseExpr(Expr.SearchedCase expr) { VisitSearchedCase(expr); return null; }
        object Expr.Visitor<object>.VisitCastExpr(Expr.CastExpression expr) { VisitCast(expr); return null; }
        object Expr.Visitor<object>.VisitConvertExpr(Expr.ConvertExpression expr) { VisitConvert(expr); return null; }

        object Predicate.Visitor<object>.VisitComparisonPredicate(Predicate.Comparison pred) { VisitComparison(pred); return null; }
        object Predicate.Visitor<object>.VisitLikePredicate(Predicate.Like pred) { VisitLike(pred); return null; }
        object Predicate.Visitor<object>.VisitBetweenPredicate(Predicate.Between pred) { VisitBetween(pred); return null; }
        object Predicate.Visitor<object>.VisitNullPredicate(Predicate.Null pred) { VisitNull(pred); return null; }
        object Predicate.Visitor<object>.VisitContainsPredicate(Predicate.Contains pred) { VisitContains(pred); return null; }
        object Predicate.Visitor<object>.VisitInPredicate(Predicate.In pred) { VisitIn(pred); return null; }
        object Predicate.Visitor<object>.VisitQuantifierPredicate(Predicate.Quantifier pred) { VisitQuantifier(pred); return null; }
        object Predicate.Visitor<object>.VisitExistsPredicate(Predicate.Exists pred) { VisitExists(pred); return null; }
        object Predicate.Visitor<object>.VisitGroupingPredicate(Predicate.Grouping pred) { VisitPredicateGrouping(pred); return null; }
        object Predicate.Visitor<object>.VisitAndPredicate(Predicate.And pred) { VisitAnd(pred); return null; }
        object Predicate.Visitor<object>.VisitOrPredicate(Predicate.Or pred) { VisitOr(pred); return null; }
        object Predicate.Visitor<object>.VisitNotPredicate(Predicate.Not pred) { VisitNot(pred); return null; }

        object TableSource.Visitor<object>.VisitTableReference(TableReference source) { VisitTableReference(source); return null; }
        object TableSource.Visitor<object>.VisitSubqueryReference(SubqueryReference source) { VisitSubqueryReference(source); return null; }
        object TableSource.Visitor<object>.VisitTableVariableReference(TableVariableReference source) { VisitTableVariableReference(source); return null; }
        object TableSource.Visitor<object>.VisitQualifiedJoin(QualifiedJoin source) { VisitQualifiedJoin(source); return null; }
        object TableSource.Visitor<object>.VisitCrossJoin(CrossJoin source) { VisitCrossJoin(source); return null; }
        object TableSource.Visitor<object>.VisitApplyJoin(ApplyJoin source) { VisitApplyJoin(source); return null; }
        object TableSource.Visitor<object>.VisitParenthesizedTableSource(ParenthesizedTableSource source) { VisitParenthesizedTableSource(source); return null; }
        object TableSource.Visitor<object>.VisitPivotTableSource(PivotTableSource source) { VisitPivotTableSource(source); return null; }
        object TableSource.Visitor<object>.VisitUnpivotTableSource(UnpivotTableSource source) { VisitUnpivotTableSource(source); return null; }
        object TableSource.Visitor<object>.VisitValuesTableSource(ValuesTableSource source) { VisitValuesTableSource(source); return null; }
        object TableSource.Visitor<object>.VisitRowsetFunctionReference(RowsetFunctionReference source) { VisitRowsetFunctionReference(source); return null; }

        #endregion
    }
}
