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
        public static void AddHaving(Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            HavingWalker walker = new HavingWalker(condition, target);
            walker.Walk(stmt);
        }

        public static void AddHaving(Stmt stmt, string condition, string targetTable,
            QueryScope traverse, QueryScope mutate)
        {
            if (mutate == QueryScope.None || string.IsNullOrEmpty(condition))
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyHaving(selectExpr, condition));
            walker.Walk(stmt);
        }

        private static void ApplyHaving(SelectExpression selectExpr, string condition)
        {
            Predicate predicate = Predicate.ParsePredicate(condition);
            selectExpr.AddHaving(predicate);
        }


        private class HavingWalker : ScopedQueryWalker
        {
            private readonly string _condition;

            public HavingWalker(string condition, QueryScope target)
                : base(QueryScope.All, target)
            {
                _condition = condition;
            }

            protected override void OnSelect(SelectExpression selectExpr)
            {
                ApplyHaving(selectExpr, _condition);
            }
        }
    }
}
