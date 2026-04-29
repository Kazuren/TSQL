using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class OrderByAppender
    {
        /// <summary>
        /// Appends ORDER BY items to SELECT statements within the given statement.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="orderByItems">ORDER BY items to append.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        public static void AddOrderBy(Stmt stmt, string orderByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(orderByItems))
            {
                return;
            }

            OrderByWalker walker = new OrderByWalker(orderByItems, target, replace: false);
            walker.Walk(stmt);
        }

        public static void AddOrderBy(Stmt stmt, string orderByItems, string targetTable,
            QueryScope traverse, QueryScope mutate)
        {
            if (mutate == QueryScope.None || string.IsNullOrEmpty(orderByItems))
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyOrderBy(selectExpr, orderByItems, replace: false));
            walker.Walk(stmt);
        }

        /// <summary>
        /// Replaces the ORDER BY clause in SELECT statements within the given statement.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="orderByItems">ORDER BY items to set. Pass empty string to clear.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        public static void ReplaceOrderBy(Stmt stmt, string orderByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None)
            {
                return;
            }

            OrderByWalker walker = new OrderByWalker(orderByItems, target, replace: true);
            walker.Walk(stmt);
        }

        public static void ReplaceOrderBy(Stmt stmt, string orderByItems, string targetTable,
            QueryScope traverse, QueryScope mutate)
        {
            if (mutate == QueryScope.None)
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyOrderBy(selectExpr, orderByItems, replace: true));
            walker.Walk(stmt);
        }

        private static void ApplyOrderBy(QueryExpression queryExpr, string orderByItems, bool replace)
        {
            if (replace)
            {
                queryExpr.ClearOrderBy();
            }

            if (!string.IsNullOrEmpty(orderByItems))
            {
                SyntaxElementList<OrderByItem> items = OrderByItem.ParseOrderByItems(orderByItems);
                queryExpr.AddOrderBy(items);
            }
        }


        private class OrderByWalker : ScopedQueryWalker
        {
            private readonly string _orderByItems;
            private readonly bool _replace;

            public OrderByWalker(string orderByItems, QueryScope target, bool replace)
                : base(QueryScope.All, target)
            {
                _orderByItems = orderByItems;
                _replace = replace;
            }

            protected override void OnQueryExpressionMatch(QueryExpression queryExpr)
            {
                ApplyOrderBy(queryExpr, _orderByItems, _replace);
            }
        }
    }
}
