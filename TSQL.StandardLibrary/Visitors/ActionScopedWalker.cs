using System;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// A <see cref="ScopedQueryWalker"/> that invokes an action on every matching SelectExpression.
    /// Unlike <see cref="TableScopedWalker"/>, no table filter is applied - all matching scopes are acted on.
    /// </summary>
    internal class ActionScopedWalker : ScopedQueryWalker
    {
        private readonly Action<SelectExpression> _action;

        public ActionScopedWalker(QueryScope traverse, QueryScope mutate, Action<SelectExpression> action)
            : base(traverse, mutate)
        {
            _action = action;
        }

        protected override void OnSelect(SelectExpression selectExpr)
        {
            _action(selectExpr);
        }
    }
}
