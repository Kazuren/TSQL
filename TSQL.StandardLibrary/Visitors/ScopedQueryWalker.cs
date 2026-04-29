using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// Base walker that dispatches to <see cref="OnSelect"/> for every SelectExpression
    /// whose query-level category passes the <c>mutate</c> scope check. The <c>traverse</c>
    /// scope controls which categories the walker enters at all. Children are always walked
    /// before <see cref="OnSelect"/> is called (post-order), so mutations don't interfere
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
        /// <remarks>
        /// Called once per SELECT branch. Use for clause-level mutations (WHERE, GROUP BY, HAVING, JOIN).
        /// <code>
        /// WITH Cte AS (
        ///     SELECT a FROM T1   ← OnSelect (Ctes)
        ///     UNION
        ///     SELECT b FROM T2   ← OnSelect (Ctes)
        /// )
        /// SELECT * FROM Cte      ← OnSelect (OutermostQuery)
        /// WHERE x IN (
        ///     SELECT y FROM T3   ← OnSelect (InSubqueries)
        /// )
        /// ORDER BY 1
        /// </code>
        /// </remarks>
        protected virtual void OnSelect(SelectExpression selectExpr) { }

        /// <summary>
        /// Called once for each QueryExpression at a category boundary (CTE, outermost, subquery).
        /// Unlike <see cref="OnSelect"/>, this is called for the entire QueryExpression
        /// (which may be a SetOperation for UNION queries) rather than for each SelectExpression branch.
        /// </summary>
        /// <remarks>
        /// Called once per query expression. Use for query-level mutations (ORDER BY) that apply to combined results.
        /// <code>
        /// WITH Cte AS (
        ///     SELECT a FROM T1
        ///     UNION
        ///     SELECT b FROM T2   ← OnQuery (Ctes) - once for entire UNION
        /// )
        /// SELECT * FROM Cte
        /// WHERE x IN (
        ///     SELECT y FROM T3   ← OnQuery (InSubqueries)
        /// )
        /// ORDER BY 1             ← OnQuery (OutermostQuery)
        /// </code>
        /// </remarks>
        protected virtual void OnQuery(QueryExpression queryExpr) { }

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
            // Walk and call OnSelect for SelectExpression leaves
            WalkQueryExpressionRecursive(queryExpr, category);

            // Call QueryExpression callback at the top level (once, not for each branch)
            if (CanMutate(category))
            {
                OnQuery(queryExpr);
            }
        }

        private void WalkQueryExpressionRecursive(QueryExpression queryExpr, QueryScope category)
        {
            if (queryExpr is SelectExpression selectExpr)
            {
                WalkSelectExpression(selectExpr);
                if (CanMutate(category))
                {
                    OnSelect(selectExpr);
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
