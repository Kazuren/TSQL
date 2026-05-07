using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class HavingAppender
    {
        /// <summary>
        /// Appends a HAVING condition to SELECT statements within the given statement.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <param name="allowLeadingHavingKeyword">
        /// When true (the default), a leading HAVING keyword is silently consumed if present.
        /// This allows callers to pass either <c>HAVING COUNT(*) > 5</c> or <c>COUNT(*) > 5</c>.
        /// </param>
        public static void AddHaving(Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery, bool allowLeadingHavingKeyword = true)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            HavingWalker walker = new HavingWalker(condition, target, allowLeadingHavingKeyword);
            walker.Walk(stmt);
        }

        public static void AddHaving(Stmt stmt, string condition, string targetTable,
            QueryScope traverse, QueryScope mutate, bool allowLeadingHavingKeyword = true)
        {
            if (mutate == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyHaving(selectExpr, condition, allowLeadingHavingKeyword));
            walker.Walk(stmt);
        }

        private static void ApplyHaving(SelectExpression selectExpr, string condition, bool allowLeadingHavingKeyword)
        {
            Predicate predicate = allowLeadingHavingKeyword
                ? Predicate.ParseHavingCondition(condition)
                : Predicate.ParsePredicate(condition);
            selectExpr.AddHaving(predicate);
        }


        private class HavingWalker : ScopedQueryWalker
        {
            private readonly string _condition;
            private readonly bool _allowLeadingHavingKeyword;

            public HavingWalker(string condition, QueryScope target, bool allowLeadingHavingKeyword)
                : base(QueryScope.All, target)
            {
                _condition = condition;
                _allowLeadingHavingKeyword = allowLeadingHavingKeyword;
            }

            protected override void OnSelect(SelectExpression selectExpr)
            {
                ApplyHaving(selectExpr, _condition, _allowLeadingHavingKeyword);
            }
        }
    }
}
