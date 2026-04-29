using System;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class JoinAppender
    {
        /// <summary>
        /// Appends a JOIN clause to SELECT statements within the given statement.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="joinFragment">JOIN clause to append.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        public static void AddJoin(Stmt stmt, string joinFragment, QueryScope target = QueryScope.OutermostQuery)
        {
            if (target == QueryScope.None || string.IsNullOrEmpty(joinFragment))
            {
                return;
            }

            JoinWalker walker = new JoinWalker(joinFragment, target);
            walker.Walk(stmt);
        }

        public static void AddJoin(Stmt stmt, string joinFragment, string targetTable,
            QueryScope traverse, QueryScope mutate)
        {
            if (string.IsNullOrEmpty(joinFragment))
            {
                return;
            }

            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => ApplyJoin(selectExpr, joinFragment));
            walker.Walk(stmt);
        }

        private static void ApplyJoin(SelectExpression selectExpr, string joinFragment)
        {
            if (selectExpr.From == null || selectExpr.From.TableSources.Count == 0)
            {
                throw new InvalidOperationException("Cannot add JOIN to a query without a FROM clause.");
            }

            int lastIndex = selectExpr.From.TableSources.Count - 1;
            TableSource existingSource = selectExpr.From.TableSources[lastIndex];

            QualifiedJoin newJoin = QualifiedJoin.CreateJoin(existingSource, joinFragment);
            selectExpr.From.TableSources[lastIndex] = newJoin;
        }


        private class JoinWalker : ScopedQueryWalker
        {
            private readonly string _joinFragment;

            public JoinWalker(string joinFragment, QueryScope target)
                : base(QueryScope.All, target)
            {
                _joinFragment = joinFragment;
            }

            protected override void OnMatch(SelectExpression selectExpr)
            {
                ApplyJoin(selectExpr, _joinFragment);
            }
        }
    }
}
