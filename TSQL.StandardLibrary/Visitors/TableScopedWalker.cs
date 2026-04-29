using System;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// A <see cref="ScopedQueryWalker"/> that additionally filters by target table name.
    /// Only SelectExpressions whose FROM clause references the target table are acted on.
    /// </summary>
    internal class TableScopedWalker : ScopedQueryWalker
    {
        private readonly string _targetTable;
        private readonly Action<SelectExpression> _action;

        public TableScopedWalker(string targetTable,
            QueryScope traverse, QueryScope mutate,
            Action<SelectExpression> action)
            : base(traverse, mutate)
        {
            _targetTable = targetTable;
            _action = action;
        }

        protected override void OnSelect(SelectExpression selectExpr)
        {
            if (selectExpr.ContainsTableReference(_targetTable))
            {
                _action(selectExpr);
            }
        }
    }
}
