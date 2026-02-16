using System;
using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    [Flags]
    public enum ColumnReferenceScope
    {
        None = 0,
        OutermostQuery = 1,
        Ctes = 2,
        Subqueries = 4,
        All = OutermostQuery | Ctes | Subqueries
    }

    [Flags]
    public enum ColumnReferenceClause
    {
        None = 0,
        Select = 1,
        From = 2,
        Where = 4,
        GroupBy = 8,
        Having = 16,
        OrderBy = 32,
        All = Select | From | Where | GroupBy | Having | OrderBy
    }

    internal class ColumnReferenceCollector : SqlWalker
    {
        private readonly List<Expr.ColumnIdentifier> _columns = new List<Expr.ColumnIdentifier>();
        private readonly ColumnReferenceScope _scope;
        private readonly ColumnReferenceClause _clauses;

        private bool _collecting;
        private ColumnReferenceClause _currentClause;

        private ColumnReferenceCollector(ColumnReferenceScope scope, ColumnReferenceClause clauses)
        {
            _scope = scope;
            _clauses = clauses;
        }

        internal static IReadOnlyList<Expr.ColumnIdentifier> Collect(
            Stmt stmt,
            ColumnReferenceScope scope,
            ColumnReferenceClause clauses)
        {
            ColumnReferenceCollector collector = new ColumnReferenceCollector(scope, clauses);
            collector.Walk(stmt);
            return collector._columns;
        }

        protected override void VisitColumnIdentifier(Expr.ColumnIdentifier expr)
        {
            if (_collecting && (_clauses & _currentClause) != 0)
            {
                _columns.Add(expr);
            }
        }

        #region Scope Gating

        protected override void VisitSelect(Stmt.Select stmt)
        {
            if (stmt.CteStmt != null)
            {
                bool savedCollecting = _collecting;
                _collecting = (_scope & ColumnReferenceScope.Ctes) != 0;

                foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                {
                    WalkQueryExpressionWithClauses(cte.Query.Query);
                }

                _collecting = savedCollecting;
            }

            bool outerSaved = _collecting;
            _collecting = (_scope & ColumnReferenceScope.OutermostQuery) != 0;
            WalkQueryExpressionWithClauses(stmt.Query);
            _collecting = outerSaved;
        }

        protected override void VisitSubquery(Expr.Subquery expr)
        {
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            WalkQueryExpressionWithClauses(expr.Query);
            _collecting = savedCollecting;
        }

        protected override void VisitSubqueryReference(SubqueryReference source)
        {
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            WalkQueryExpressionWithClauses(source.Subquery.Query);
            _collecting = savedCollecting;
        }

        protected override void VisitIn(Predicate.In pred)
        {
            Walk(pred.Expr);
            if (pred.Subquery != null)
            {
                bool savedCollecting = _collecting;
                _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
                WalkQueryExpressionWithClauses(pred.Subquery.Query);
                _collecting = savedCollecting;
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
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            WalkQueryExpressionWithClauses(pred.Subquery.Query);
            _collecting = savedCollecting;
        }

        protected override void VisitQuantifier(Predicate.Quantifier pred)
        {
            Walk(pred.Left);
            bool savedCollecting = _collecting;
            _collecting = (_scope & ColumnReferenceScope.Subqueries) != 0;
            WalkQueryExpressionWithClauses(pred.Subquery.Query);
            _collecting = savedCollecting;
        }

        #endregion

        #region Clause-Tracking Walk Methods

        private void WalkQueryExpressionWithClauses(QueryExpression queryExpr)
        {
            if (queryExpr is SelectExpression selectExpr)
            {
                WalkSelectExpressionWithClauses(selectExpr);
            }
            else if (queryExpr is SetOperation setOp)
            {
                WalkQueryExpressionWithClauses(setOp.Left);
                WalkQueryExpressionWithClauses(setOp.Right);
            }

            if (queryExpr.OrderBy != null)
            {
                ColumnReferenceClause savedClause = _currentClause;
                _currentClause = ColumnReferenceClause.OrderBy;

                foreach (OrderByItem item in queryExpr.OrderBy.Items)
                {
                    Walk(item.Expression);
                }

                if (queryExpr.OrderBy.OffsetCount != null)
                {
                    Walk(queryExpr.OrderBy.OffsetCount);
                }

                if (queryExpr.OrderBy.FetchCount != null)
                {
                    Walk(queryExpr.OrderBy.FetchCount);
                }

                _currentClause = savedClause;
            }
        }

        private void WalkSelectExpressionWithClauses(SelectExpression selectExpr)
        {
            ColumnReferenceClause savedClause = _currentClause;

            // TOP clause — treat as part of Select
            if (selectExpr.Top != null)
            {
                _currentClause = ColumnReferenceClause.Select;
                Walk(selectExpr.Top.Expression);
            }

            // SELECT columns
            _currentClause = ColumnReferenceClause.Select;
            foreach (SelectItem item in selectExpr.Columns)
            {
                if (item is SelectColumn col)
                {
                    Walk(col.Expression);
                }
                else if (item is Expr.Wildcard wildcard)
                {
                    Walk(wildcard);
                }
                else if (item is Expr.QualifiedWildcard qualifiedWildcard)
                {
                    Walk(qualifiedWildcard);
                }
            }

            // FROM clause (includes JOIN ON conditions)
            if (selectExpr.From != null)
            {
                _currentClause = ColumnReferenceClause.From;
                foreach (TableSource tableSource in selectExpr.From.TableSources)
                {
                    Walk(tableSource);
                }
            }

            // WHERE clause
            if (selectExpr.Where != null)
            {
                _currentClause = ColumnReferenceClause.Where;
                Walk(selectExpr.Where);
            }

            // GROUP BY clause
            if (selectExpr.GroupBy != null)
            {
                _currentClause = ColumnReferenceClause.GroupBy;
                WalkGroupByItems(selectExpr.GroupBy.Items);
            }

            // HAVING clause
            if (selectExpr.Having != null)
            {
                _currentClause = ColumnReferenceClause.Having;
                Walk(selectExpr.Having);
            }

            _currentClause = savedClause;
        }

        private void WalkGroupByItems(SyntaxElementList<GroupByItem> items)
        {
            foreach (GroupByItem item in items)
            {
                if (item is GroupByExpression groupByExpr)
                {
                    Walk(groupByExpr.Expression);
                }
                else if (item is GroupByRollup rollup)
                {
                    WalkGroupByItems(rollup.Items);
                }
                else if (item is GroupByCube cube)
                {
                    WalkGroupByItems(cube.Items);
                }
                else if (item is GroupByGroupingSets sets)
                {
                    WalkGroupByItems(sets.Items);
                }
                else if (item is GroupByComposite composite)
                {
                    foreach (Expr expr in composite.Expressions)
                    {
                        Walk(expr);
                    }
                }
            }
        }

        #endregion
    }
}
