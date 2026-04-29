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
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        public static void AddCondition(Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            WhereConditionWalker walker = new WhereConditionWalker(condition, target);
            walker.Walk(stmt);
        }


        private class WhereConditionWalker : ScopedQueryWalker
        {
            private readonly string _condition;

            public WhereConditionWalker(string condition, QueryScope target)
                : base(QueryScope.All, target)
            {
                _condition = condition;
            }

            protected override void OnMatch(SelectExpression selectExpr)
            {
                Predicate predicate = Predicate.ParsePredicate(_condition);
                selectExpr.AddWhere(predicate);
            }
        }
    }
}
