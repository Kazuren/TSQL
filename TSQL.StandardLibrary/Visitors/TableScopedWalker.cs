using System;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// Walks the AST and invokes a mutation action on every SelectExpression whose
    /// FROM clause references the target table, subject to traverse and mutate scope.
    /// Used by both table-scoped condition injection and column injection.
    /// </summary>
    internal class TableScopedWalker : SqlWalker
    {
        private readonly string _targetTable;
        private readonly QueryScope _traverse;
        private readonly QueryScope _mutate;
        private readonly Action<SelectExpression> _action;

        public TableScopedWalker(string targetTable,
            QueryScope traverse, QueryScope mutate,
            Action<SelectExpression> action)
        {
            _targetTable = targetTable;
            _traverse = traverse;
            _mutate = mutate;
            _action = action;
        }

        private bool CanTraverse(QueryScope flag)
        {
            return (_traverse & flag) != 0;
        }

        private bool CanMutate(QueryScope flag)
        {
            return (_mutate & flag) != 0;
        }

        protected override void VisitSelect(Stmt.Select stmt)
        {
            if (stmt.CteStmt != null && CanTraverse(QueryScope.Ctes))
            {
                foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                {
                    HandleQueryExpression(cte.Query.Query, QueryScope.Ctes);
                }
            }
            if (CanTraverse(QueryScope.OutermostQuery))
            {
                HandleQueryExpression(stmt.Query, QueryScope.OutermostQuery);
            }
        }

        protected override void VisitSubqueryReference(SubqueryReference source)
        {
            if (CanTraverse(QueryScope.FromSubqueries))
            {
                HandleQueryExpression(source.Subquery.Query, QueryScope.FromSubqueries);
            }
        }

        protected override void VisitIn(Predicate.In pred)
        {
            Walk(pred.Expr);
            if (pred.Subquery != null)
            {
                if (CanTraverse(QueryScope.InSubqueries))
                {
                    HandleQueryExpression(pred.Subquery.Query, QueryScope.InSubqueries);
                }
            }
            else if (pred.ValueList != null)
            {
                foreach (Expr expr in pred.ValueList)
                {
                    Walk(expr);
                }
            }
        }

        protected override void VisitExists(Predicate.Exists pred)
        {
            if (CanTraverse(QueryScope.ExistsSubqueries))
            {
                HandleQueryExpression(pred.Subquery.Query, QueryScope.ExistsSubqueries);
            }
        }

        protected override void VisitQuantifier(Predicate.Quantifier pred)
        {
            Walk(pred.Left);
            if (CanTraverse(QueryScope.ScalarSubqueries))
            {
                HandleQueryExpression(pred.Subquery.Query, QueryScope.ScalarSubqueries);
            }
        }

        protected override void VisitSubquery(Expr.Subquery expr)
        {
            if (CanTraverse(QueryScope.ScalarSubqueries))
            {
                HandleQueryExpression(expr.Query, QueryScope.ScalarSubqueries);
            }
        }

        private void HandleQueryExpression(QueryExpression queryExpr, QueryScope category)
        {
            if (queryExpr is SelectExpression selectExpr)
            {
                // Walk before mutating so the traversal sees only the original AST.
                WalkSelectExpression(selectExpr);
                if (CanMutate(category) && selectExpr.ContainsTableReference(_targetTable))
                {
                    _action(selectExpr);
                }
            }
            else if (queryExpr is SetOperation setOp)
            {
                HandleQueryExpression(setOp.Left, category);
                HandleQueryExpression(setOp.Right, category);
            }
            else if (queryExpr is ParenthesizedQuery parenQuery)
            {
                HandleQueryExpression(parenQuery.Inner, category);
            }
        }

    }
}
