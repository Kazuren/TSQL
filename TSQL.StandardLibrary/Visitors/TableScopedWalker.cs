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
        private readonly WhereClauseTarget _traverse;
        private readonly WhereClauseTarget _mutate;
        private readonly Action<SelectExpression> _action;

        public TableScopedWalker(string targetTable,
            WhereClauseTarget traverse, WhereClauseTarget mutate,
            Action<SelectExpression> action)
        {
            _targetTable = targetTable;
            _traverse = traverse;
            _mutate = mutate;
            _action = action;
        }

        private bool CanTraverse(WhereClauseTarget flag)
        {
            return (_traverse & flag) != 0;
        }

        private bool CanMutate(WhereClauseTarget flag)
        {
            return (_mutate & flag) != 0;
        }

        protected override void VisitSelect(Stmt.Select stmt)
        {
            if (stmt.CteStmt != null && CanTraverse(WhereClauseTarget.Ctes))
            {
                foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                {
                    HandleQueryExpression(cte.Query.Query, WhereClauseTarget.Ctes);
                }
            }
            if (CanTraverse(WhereClauseTarget.OutermostQuery))
            {
                HandleQueryExpression(stmt.Query, WhereClauseTarget.OutermostQuery);
            }
        }

        protected override void VisitSubqueryReference(SubqueryReference source)
        {
            if (CanTraverse(WhereClauseTarget.FromSubqueries))
            {
                HandleQueryExpression(source.Subquery.Query, WhereClauseTarget.FromSubqueries);
            }
        }

        protected override void VisitIn(Predicate.In pred)
        {
            Walk(pred.Expr);
            if (pred.Subquery != null)
            {
                if (CanTraverse(WhereClauseTarget.InSubqueries))
                {
                    HandleQueryExpression(pred.Subquery.Query, WhereClauseTarget.InSubqueries);
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
            if (CanTraverse(WhereClauseTarget.ExistsSubqueries))
            {
                HandleQueryExpression(pred.Subquery.Query, WhereClauseTarget.ExistsSubqueries);
            }
        }

        protected override void VisitQuantifier(Predicate.Quantifier pred)
        {
            Walk(pred.Left);
            if (CanTraverse(WhereClauseTarget.ScalarSubqueries))
            {
                HandleQueryExpression(pred.Subquery.Query, WhereClauseTarget.ScalarSubqueries);
            }
        }

        protected override void VisitSubquery(Expr.Subquery expr)
        {
            if (CanTraverse(WhereClauseTarget.ScalarSubqueries))
            {
                HandleQueryExpression(expr.Query, WhereClauseTarget.ScalarSubqueries);
            }
        }

        private void HandleQueryExpression(QueryExpression queryExpr, WhereClauseTarget category)
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

        /// <summary>
        /// Maps <see cref="ColumnReferenceScope"/> flags to the corresponding
        /// <see cref="WhereClauseTarget"/> flags so the walker can use a single enum internally.
        /// </summary>
        internal static WhereClauseTarget MapScope(ColumnReferenceScope scope)
        {
            WhereClauseTarget result = WhereClauseTarget.None;
            if ((scope & ColumnReferenceScope.OutermostQuery) != 0) result |= WhereClauseTarget.OutermostQuery;
            if ((scope & ColumnReferenceScope.Ctes) != 0) result |= WhereClauseTarget.Ctes;
            if ((scope & ColumnReferenceScope.Subqueries) != 0) result |= WhereClauseTarget.AllSubqueries;
            return result;
        }
    }
}
