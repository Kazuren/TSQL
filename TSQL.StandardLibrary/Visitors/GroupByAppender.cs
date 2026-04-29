using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class GroupByAppender
    {
        /// <summary>
        /// Appends GROUP BY items to SELECT statements within the given statement.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="groupByItems">GROUP BY items to append.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        public static void AddGroupBy(Stmt stmt, string groupByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(groupByItems))
            {
                return;
            }

            GroupByWalker walker = new GroupByWalker(groupByItems, target, replace: false);
            walker.Walk(stmt);
        }

        public static void AddGroupBy(Stmt stmt, string groupByItems, string targetTable,
            QueryScope traverse, QueryScope mutate)
        {
            if (string.IsNullOrEmpty(groupByItems))
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyGroupBy(selectExpr, groupByItems, replace: false));
            walker.Walk(stmt);
        }

        /// <summary>
        /// Replaces the GROUP BY clause in SELECT statements within the given statement.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="groupByItems">GROUP BY items to set. Pass empty string to clear.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        public static void ReplaceGroupBy(Stmt stmt, string groupByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None)
            {
                return;
            }

            GroupByWalker walker = new GroupByWalker(groupByItems, target, replace: true);
            walker.Walk(stmt);
        }

        public static void ReplaceGroupBy(Stmt stmt, string groupByItems, string targetTable,
            QueryScope traverse, QueryScope mutate)
        {
            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyGroupBy(selectExpr, groupByItems, replace: true));
            walker.Walk(stmt);
        }

        private static void ApplyGroupBy(SelectExpression selectExpr, string groupByItems, bool replace)
        {
            if (replace)
            {
                selectExpr.ClearGroupBy();
            }

            if (!string.IsNullOrEmpty(groupByItems))
            {
                SyntaxElementList<GroupByItem> items = GroupByItem.ParseGroupByItems(groupByItems);
                selectExpr.AddGroupBy(items);
            }
        }


        private class GroupByWalker : ScopedQueryWalker
        {
            private readonly string _groupByItems;
            private readonly bool _replace;

            public GroupByWalker(string groupByItems, QueryScope target, bool replace)
                : base(QueryScope.All, target)
            {
                _groupByItems = groupByItems;
                _replace = replace;
            }

            protected override void OnMatch(SelectExpression selectExpr)
            {
                ApplyGroupBy(selectExpr, _groupByItems, _replace);
            }
        }
    }
}
