using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal class SelectColumnCollector : SqlWalker
    {
        private readonly List<SelectItem> _columns = new List<SelectItem>();
        private readonly ColumnReferenceScope _scope;

        private bool _collecting;

        private SelectColumnCollector(ColumnReferenceScope scope)
        {
            _scope = scope;
        }

        internal static IReadOnlyList<SelectItem> Collect(Stmt stmt, ColumnReferenceScope scope)
        {
            SelectColumnCollector collector = new SelectColumnCollector(scope);
            collector.Walk(stmt);
            return collector._columns;
        }

        #region Scope Gating

        protected override void VisitSelect(Stmt.Select stmt)
        {
            if (stmt.CteStmt != null)
            {
                bool savedCollecting = _collecting;
                _collecting = (_scope & ColumnReferenceScope.Ctes) != 0;

                foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                {
                    CollectFromQueryExpression(cte.Query.Query);
                }

                _collecting = savedCollecting;
            }

            bool outerSaved = _collecting;
            _collecting = (_scope & ColumnReferenceScope.OutermostQuery) != 0;
            CollectFromQueryExpression(stmt.Query);
            _collecting = outerSaved;

            // Walk the full statement to find subqueries in WHERE, FROM, etc.
            base.VisitSelect(stmt);
        }

        protected override void VisitSubquery(Expr.Subquery expr)
        {
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            CollectFromQueryExpression(expr.Query);
            _collecting = savedCollecting;
        }

        protected override void VisitSubqueryReference(SubqueryReference source)
        {
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            CollectFromQueryExpression(source.Subquery.Query);
            _collecting = savedCollecting;
        }

        protected override void VisitIn(Predicate.In pred)
        {
            if (pred.Subquery != null)
            {
                bool savedCollecting = _collecting;
                _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
                CollectFromQueryExpression(pred.Subquery.Query);
                _collecting = savedCollecting;
            }
        }

        protected override void VisitExists(Predicate.Exists pred)
        {
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            CollectFromQueryExpression(pred.Subquery.Query);
            _collecting = savedCollecting;
        }

        #endregion

        #region Collection

        private void CollectFromQueryExpression(QueryExpression queryExpr)
        {
            if (queryExpr is SelectExpression selectExpr)
            {
                CollectFromSelectExpression(selectExpr);
            }
            else if (queryExpr is SetOperation setOp)
            {
                // For UNION/EXCEPT/INTERSECT, result columns come from the first operand.
                CollectFromQueryExpression(setOp.Left);
            }
            else if (queryExpr is ParenthesizedQuery parenQuery)
            {
                CollectFromQueryExpression(parenQuery.Inner);
            }
        }

        private void CollectFromSelectExpression(SelectExpression selectExpr)
        {
            if (!_collecting)
            {
                return;
            }

            foreach (SelectItem item in selectExpr.Columns)
            {
                _columns.Add(item);
            }
        }

        #endregion
    }
}
