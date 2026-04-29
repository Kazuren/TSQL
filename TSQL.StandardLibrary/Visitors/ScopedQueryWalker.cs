using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// Base walker that dispatches to <see cref="OnMatch"/> for every SelectExpression
    /// whose query-level category passes the <c>mutate</c> scope check. The <c>traverse</c>
    /// scope controls which categories the walker enters at all. Children are always walked
    /// before <see cref="OnMatch"/> is called (post-order), so mutations don't interfere
    /// with the traversal.
    /// </summary>
    internal abstract class ScopedQueryWalker : SqlWalker
    {
        private readonly QueryScope _traverse;
        private readonly QueryScope _mutate;

        protected ScopedQueryWalker(QueryScope traverse, QueryScope mutate)
        {
            _traverse = traverse;
            _mutate = mutate;
        }

        /// <summary>
        /// Called for every SelectExpression whose category passes the <c>mutate</c> scope check.
        /// The SelectExpression's children have already been walked.
        /// </summary>
        protected abstract void OnMatch(SelectExpression selectExpr);

        /// <summary>
        /// Called once for each QueryExpression at a category boundary (CTE, outermost, subquery).
        /// Unlike <see cref="OnMatch"/>, this is called for the entire QueryExpression
        /// (which may be a SetOperation for UNION queries) rather than for each SelectExpression branch.
        /// Default implementation does nothing.
        /// </summary>
        protected virtual void OnQueryExpressionMatch(QueryExpression queryExpr) { }

        private bool CanTraverse(QueryScope flag)
        {
            return (_traverse & flag) != 0;
        }

        protected bool CanMutate(QueryScope flag)
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
            // Walk and call OnMatch for SelectExpression leaves
            WalkQueryExpressionRecursive(queryExpr, category);

            // Call QueryExpression callback at the top level (once, not for each branch)
            if (CanMutate(category))
            {
                OnQueryExpressionMatch(queryExpr);
            }
        }

        private void WalkQueryExpressionRecursive(QueryExpression queryExpr, QueryScope category)
        {
            if (queryExpr is SelectExpression selectExpr)
            {
                WalkSelectExpression(selectExpr);
                if (CanMutate(category))
                {
                    OnMatch(selectExpr);
                }
            }
            else if (queryExpr is SetOperation setOp)
            {
                WalkQueryExpressionRecursive(setOp.Left, category);
                WalkQueryExpressionRecursive(setOp.Right, category);
            }
            else if (queryExpr is ParenthesizedQuery parenQuery)
            {
                WalkQueryExpressionRecursive(parenQuery.Inner, category);
            }
        }
    }
}
