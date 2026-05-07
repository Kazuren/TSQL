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
        /// <param name="allowLeadingWhereKeyword">
        /// When true (the default), a leading WHERE keyword is silently consumed if present.
        /// This allows callers to pass either <c>WHERE x = 1</c> or <c>x = 1</c>.
        /// </param>
        public static void AddCondition(Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery, bool allowLeadingWhereKeyword = true)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            WhereConditionWalker walker = new WhereConditionWalker(condition, target, allowLeadingWhereKeyword);
            walker.Walk(stmt);
        }

        public static void AddCondition(Stmt stmt, string condition, string targetTable,
            QueryScope traverse, QueryScope mutate, bool allowLeadingWhereKeyword = true)
        {
            if (mutate == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyWhere(selectExpr, condition, allowLeadingWhereKeyword));
            walker.Walk(stmt);
        }

        private static void ApplyWhere(SelectExpression selectExpr, string condition, bool allowLeadingWhereKeyword)
        {
            Predicate predicate = allowLeadingWhereKeyword
                ? Predicate.ParseWhereCondition(condition)
                : Predicate.ParsePredicate(condition);
            selectExpr.AddWhere(predicate);
        }


        private class WhereConditionWalker : ScopedQueryWalker
        {
            private readonly string _condition;
            private readonly bool _allowLeadingWhereKeyword;

            public WhereConditionWalker(string condition, QueryScope target, bool allowLeadingWhereKeyword)
                : base(QueryScope.All, target)
            {
                _condition = condition;
                _allowLeadingWhereKeyword = allowLeadingWhereKeyword;
            }

            protected override void OnSelect(SelectExpression selectExpr)
            {
                ApplyWhere(selectExpr, _condition, _allowLeadingWhereKeyword);
            }
        }
    }
}
