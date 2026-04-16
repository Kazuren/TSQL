using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class WhereClauseAppender
    {
        /// <summary>
        /// Appends a WHERE condition to SELECT statements within the given statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified:
        /// outermost query, CTEs, FROM/IN/EXISTS subqueries, scalar subqueries, or any combination.
        /// </summary>
        public static void AddCondition(Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None)
            {
                return;
            }

            WhereConditionWalker walker = new WhereConditionWalker(condition, target);
            walker.Walk(stmt);
        }


        private class WhereConditionWalker : SqlWalker
        {
            private readonly string _condition;
            private readonly QueryScope _target;

            public WhereConditionWalker(string condition, QueryScope target)
            {
                _condition = condition;
                _target = target;
            }

            private bool HasFlag(QueryScope flag)
            {
                return (_target & flag) != 0;
            }

            protected override void VisitSelect(Stmt.Select stmt)
            {
                if (stmt.CteStmt != null)
                {
                    foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                    {
                        HandleQueryExpression(cte.Query.Query, QueryScope.Ctes);
                    }
                }
                HandleQueryExpression(stmt.Query, QueryScope.OutermostQuery);
            }

            protected override void VisitSubqueryReference(SubqueryReference source)
            {
                HandleQueryExpression(source.Subquery.Query, QueryScope.FromSubqueries);
            }

            protected override void VisitIn(Predicate.In pred)
            {
                Walk(pred.Expr);
                if (pred.Subquery != null)
                {
                    HandleQueryExpression(pred.Subquery.Query, QueryScope.InSubqueries);
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
                HandleQueryExpression(pred.Subquery.Query, QueryScope.ExistsSubqueries);
            }

            protected override void VisitQuantifier(Predicate.Quantifier pred)
            {
                Walk(pred.Left);
                HandleQueryExpression(pred.Subquery.Query, QueryScope.ScalarSubqueries);
            }

            protected override void VisitSubquery(Expr.Subquery expr)
            {
                HandleQueryExpression(expr.Query, QueryScope.ScalarSubqueries);
            }

            private void HandleQueryExpression(QueryExpression queryExpr, QueryScope requiredFlag)
            {
                if (queryExpr is SelectExpression selectExpr)
                {
                    // Walk must happen before adding the condition so that the walk only
                    // traverses the original AST. If the condition is added first and it
                    // contains subqueries (EXISTS, IN, quantifier), walking the freshly-added
                    // WHERE predicate re-enters HandleQueryExpression and causes infinite recursion.
                    WalkSelectExpression(selectExpr);
                    if (HasFlag(requiredFlag))
                    {
                        Predicate predicate = Predicate.ParsePredicate(_condition);
                        selectExpr.AddWhere(predicate);
                    }
                }
                else if (queryExpr is SetOperation setOp)
                {
                    HandleQueryExpression(setOp.Left, requiredFlag);
                    HandleQueryExpression(setOp.Right, requiredFlag);
                }
                else if (queryExpr is ParenthesizedQuery parenQuery)
                {
                    HandleQueryExpression(parenQuery.Inner, requiredFlag);
                }
            }
        }
    }
}
