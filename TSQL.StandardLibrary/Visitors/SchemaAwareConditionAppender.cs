using System;
using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// Callback delegate: "Does table 'tableName' have ALL of these columns?"
    /// The walker calls this to check column existence.
    /// The user provides the implementation (e.g. queries INFORMATION_SCHEMA.COLUMNS).
    /// This keeps the parser library free of any external dependencies.
    /// </summary>
    public delegate bool ColumnExistenceChecker(string tableName, IReadOnlyList<string> columnNames);

    internal static class SchemaAwareConditionAppender
    {
        /// <summary>
        /// Appends a WHERE condition to SELECT statements, but only for tables that contain
        /// all referenced columns (verified via the <paramref name="columnExists"/> callback).
        /// Unprefixed column references in the condition are automatically prefixed with the
        /// table alias (or table name if no alias).
        /// If ALL columns in the condition are already prefixed, falls back to regular AddCondition behavior.
        /// </summary>
        public static void AddCondition(Stmt stmt, string condition,
            ColumnExistenceChecker columnExists,
            WhereClauseTarget target = WhereClauseTarget.All)
        {
            if (target == WhereClauseTarget.None)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(condition))
            {
                return;
            }

            // Collect column references from the condition
            Predicate parsedCondition = Predicate.ParsePredicate(condition);
            ConditionColumnCollector columnCollector = new ConditionColumnCollector();
            columnCollector.Walk(parsedCondition);

            // If ALL columns are already prefixed, fall back to regular AddCondition
            if (columnCollector.UnprefixedColumnNames.Count == 0)
            {
                WhereClauseAppender.AddCondition(stmt, condition, target);
                return;
            }

            bool hasMixedPrefixes = columnCollector.PrefixedColumnNames.Count > 0;

            SchemaAwareWalker walker = new SchemaAwareWalker(
                condition, columnCollector.UnprefixedColumnNames, columnExists, target, hasMixedPrefixes);
            walker.Walk(stmt);
        }


        // #####################################################################
        // ################### ConditionColumnCollector ########################
        // #####################################################################

        /// <summary>
        /// Walks a parsed predicate to collect all ColumnIdentifier nodes.
        /// Classifies them as prefixed (has ObjectName) or unprefixed (just ColumnName).
        /// </summary>
        /// TODO: some way to store this in a better way? what if we have two prefixed column names with the same column name?
        private class ConditionColumnCollector : SqlWalker
        {
            public List<string> UnprefixedColumnNames { get; } = new List<string>();
            public List<string> PrefixedColumnNames { get; } = new List<string>();

            protected override void VisitColumnIdentifier(Expr.ColumnIdentifier expr)
            {
                if (expr.ObjectName != null)
                {
                    PrefixedColumnNames.Add(expr.ColumnName.Name);
                }
                else
                {
                    string name = expr.ColumnName.Name;
                    if (!UnprefixedColumnNames.Contains(name))
                    {
                        UnprefixedColumnNames.Add(name);
                    }
                }
            }
        }


        // #####################################################################
        // ################### SelectLevelTableCollector #######################
        // #####################################################################

        /// <summary>
        /// Collects TableReference nodes from a single SelectExpression's FROM clause,
        /// without descending into subqueries. Walks through QualifiedJoin, CrossJoin,
        /// ApplyJoin, and ParenthesizedTableSource to find all physical table references
        /// at this query level.
        /// </summary>
        private static class SelectLevelTableCollector
        {
            public static List<(TableReference Table, string PhysicalName, string EffectiveName)>
                Collect(SelectExpression selectExpr)
            {
                List<(TableReference, string, string)> results = new List<(TableReference, string, string)>();
                if (selectExpr.From == null)
                {
                    return results;
                }

                foreach (TableSource tableSource in selectExpr.From.TableSources)
                {
                    CollectFromTableSource(tableSource, results);
                }

                return results;
            }

            private static void CollectFromTableSource(TableSource source,
                List<(TableReference Table, string PhysicalName, string EffectiveName)> results)
            {
                if (source is TableReference tableRef)
                {
                    string physicalName = tableRef.TableName.ObjectName.Name;
                    string effectiveName = tableRef.Alias != null ? tableRef.Alias.Lexeme : tableRef.TableName.ObjectName.Lexeme;
                    results.Add((tableRef, physicalName, effectiveName));
                }
                else if (source is QualifiedJoin qualifiedJoin)
                {
                    CollectFromTableSource(qualifiedJoin.Left, results);
                    CollectFromTableSource(qualifiedJoin.Right, results);
                }
                else if (source is CrossJoin crossJoin)
                {
                    CollectFromTableSource(crossJoin.Left, results);
                    CollectFromTableSource(crossJoin.Right, results);
                }
                else if (source is ApplyJoin applyJoin)
                {
                    CollectFromTableSource(applyJoin.Left, results);
                    CollectFromTableSource(applyJoin.Right, results);
                }
                else if (source is ParenthesizedTableSource parenSource)
                {
                    CollectFromTableSource(parenSource.Inner, results);
                }
                // SubqueryReference, TableVariableReference, PivotTableSource, etc. are skipped
                // since they don't represent physical tables we can check columns against
            }
        }


        // #####################################################################
        // ################## ConjunctDecomposer ###############################
        // #####################################################################

        /// <summary>
        /// Decomposes a predicate into its top-level AND conjuncts.
        /// (A AND B AND C) becomes [A, B, C].
        /// (A OR B) stays as [(A OR B)] — OR at the top level is not split.
        /// </summary>
        private static List<Predicate> FlattenTopLevelAnd(Predicate pred)
        {
            List<Predicate> result = new List<Predicate>();
            FlattenAndHelper(pred, result);
            return result;
        }

        private static void FlattenAndHelper(Predicate pred, List<Predicate> result)
        {
            if (pred is Predicate.And andPred)
            {
                FlattenAndHelper(andPred.Left, result);
                FlattenAndHelper(andPred.Right, result);
            }
            else
            {
                result.Add(pred);
            }
        }

        /// <summary>
        /// Returns true if the predicate contains any unprefixed column references
        /// (ColumnIdentifier nodes with no ObjectName).
        /// </summary>
        private static bool HasUnprefixedColumns(Predicate pred)
        {
            ConditionColumnCollector collector = new ConditionColumnCollector();
            collector.Walk(pred);
            return collector.UnprefixedColumnNames.Count > 0;
        }


        // #####################################################################
        // ######################## ColumnPrefixer #############################
        // #####################################################################

        /// <summary>
        /// Walks a predicate and replaces unprefixed ColumnIdentifier nodes with new ones
        /// that include an ObjectName prefix. Follows the LiteralReplacer pattern from
        /// LiteralParameterizer.cs.
        /// </summary>
        private class ColumnPrefixer : SqlWalker
        {
            private readonly string _prefix;
            private readonly HashSet<string> _unprefixedColumns;

            public ColumnPrefixer(string prefix, IReadOnlyList<string> unprefixedColumns)
            {
                _prefix = prefix;
                _unprefixedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string col in unprefixedColumns)
                {
                    _unprefixedColumns.Add(col);
                }
            }

            private Expr TryPrefix(Expr expr)
            {
                if (expr is Expr.ColumnIdentifier colId
                    && colId.ObjectName == null
                    && _unprefixedColumns.Contains(colId.ColumnName.Name))
                {
                    return new Expr.ColumnIdentifier(
                        new ObjectName(_prefix),
                        new ColumnName(colId.ColumnName.Name));
                }
                else
                {
                    Walk(expr);
                    return expr;
                }
            }

            #region Expr Parents

            protected override void VisitBinary(Expr.Binary expr)
            {
                expr.Left = TryPrefix(expr.Left);
                expr.Right = TryPrefix(expr.Right);
            }

            protected override void VisitUnary(Expr.Unary expr)
            {
                expr.Right = TryPrefix(expr.Right);
            }

            protected override void VisitGrouping(Expr.Grouping expr)
            {
                expr.Expression = TryPrefix(expr.Expression);
            }

            protected override void VisitFunctionCall(Expr.FunctionCall expr)
            {
                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    expr.Arguments[i] = (Expr)TryPrefix(expr.Arguments[i]);
                }
            }

            protected override void VisitSimpleCase(Expr.SimpleCase expr)
            {
                expr.Operand = TryPrefix(expr.Operand);
                foreach (Expr.SimpleCaseWhen when in expr.WhenClauses)
                {
                    when.Value = TryPrefix(when.Value);
                    when.Result = TryPrefix(when.Result);
                }
                if (expr.ElseResult != null)
                {
                    expr.ElseResult = TryPrefix(expr.ElseResult);
                }
            }

            protected override void VisitSearchedCase(Expr.SearchedCase expr)
            {
                foreach (Expr.SearchedCaseWhen when in expr.WhenClauses)
                {
                    Walk(when.Condition);
                    when.Result = TryPrefix(when.Result);
                }
                if (expr.ElseResult != null)
                {
                    expr.ElseResult = TryPrefix(expr.ElseResult);
                }
            }

            protected override void VisitCast(Expr.CastExpression expr)
            {
                expr.Expression = TryPrefix(expr.Expression);
            }

            protected override void VisitConvert(Expr.ConvertExpression expr)
            {
                expr.Expression = TryPrefix(expr.Expression);
                if (expr.Style != null)
                {
                    expr.Style = TryPrefix(expr.Style);
                }
            }

            protected override void VisitCollate(Expr.Collate expr)
            {
                expr.Expression = TryPrefix(expr.Expression);
            }

            protected override void VisitIif(Expr.Iif expr)
            {
                Walk(expr.Condition);
                expr.TrueValue = TryPrefix(expr.TrueValue);
                expr.FalseValue = TryPrefix(expr.FalseValue);
            }

            protected override void VisitAtTimeZone(Expr.AtTimeZone expr)
            {
                expr.Expression = TryPrefix(expr.Expression);
                expr.TimeZone = TryPrefix(expr.TimeZone);
            }

            protected override void VisitOpenXml(Expr.OpenXmlExpression expr)
            {
                VisitFunctionCall(expr);
            }

            #endregion

            #region Predicate Parents

            protected override void VisitComparison(Predicate.Comparison pred)
            {
                pred.Left = TryPrefix(pred.Left);
                pred.Right = TryPrefix(pred.Right);
            }

            protected override void VisitLike(Predicate.Like pred)
            {
                pred.Left = TryPrefix(pred.Left);
                pred.Pattern = TryPrefix(pred.Pattern);
                if (pred.EscapeExpr != null)
                {
                    pred.EscapeExpr = TryPrefix(pred.EscapeExpr);
                }
            }

            protected override void VisitBetween(Predicate.Between pred)
            {
                pred.Expr = TryPrefix(pred.Expr);
                pred.LowRangeExpr = TryPrefix(pred.LowRangeExpr);
                pred.HighRangeExpr = TryPrefix(pred.HighRangeExpr);
            }

            protected override void VisitNull(Predicate.Null pred)
            {
                pred.Expr = TryPrefix(pred.Expr);
            }

            protected override void VisitContains(Predicate.Contains pred)
            {
                TryPrefixFullTextColumns(pred.Columns);
                pred.SearchCondition = TryPrefix(pred.SearchCondition);
                if (pred.Language != null)
                {
                    pred.Language = TryPrefix(pred.Language);
                }
            }

            protected override void VisitFreetext(Predicate.Freetext pred)
            {
                TryPrefixFullTextColumns(pred.Columns);
                pred.SearchCondition = TryPrefix(pred.SearchCondition);
                if (pred.Language != null)
                {
                    pred.Language = TryPrefix(pred.Language);
                }
            }

            private void TryPrefixFullTextColumns(Predicate.FullTextColumns columns)
            {
                if (columns is Predicate.FullTextColumnNames columnNames)
                {
                    for (int i = 0; i < columnNames.Columns.Count; i++)
                    {
                        columnNames.Columns[i] = (Expr.ColumnIdentifier)TryPrefix(columnNames.Columns[i]);
                    }
                }
            }

            protected override void VisitIn(Predicate.In pred)
            {
                pred.Expr = TryPrefix(pred.Expr);
                if (pred.Subquery != null)
                {
                    Walk(pred.Subquery);
                }
                else if (pred.ValueList != null)
                {
                    for (int i = 0; i < pred.ValueList.Count; i++)
                    {
                        pred.ValueList[i] = (Expr)TryPrefix(pred.ValueList[i]);
                    }
                }
            }

            protected override void VisitQuantifier(Predicate.Quantifier pred)
            {
                pred.Left = TryPrefix(pred.Left);
                Walk(pred.Subquery);
            }

            #endregion
        }


        // #####################################################################
        // ###################### SchemaAwareWalker ############################
        // #####################################################################

        /// <summary>
        /// Main walker that mirrors WhereConditionWalker but checks column existence per table
        /// and prefixes condition columns with the appropriate table alias/name before adding.
        /// Uses WhereClauseTarget flags to control which query levels are processed.
        /// </summary>
        private class SchemaAwareWalker : SqlWalker
        {
            private readonly string _condition;
            private readonly IReadOnlyList<string> _unprefixedColumnNames;
            private readonly ColumnExistenceChecker _columnExists;
            private readonly WhereClauseTarget _target;
            private readonly bool _hasMixedPrefixes;

            public SchemaAwareWalker(string condition, IReadOnlyList<string> unprefixedColumnNames,
                ColumnExistenceChecker columnExists, WhereClauseTarget target, bool hasMixedPrefixes)
            {
                _condition = condition;
                _unprefixedColumnNames = unprefixedColumnNames;
                _columnExists = columnExists;
                _target = target;
                _hasMixedPrefixes = hasMixedPrefixes;
            }

            private bool HasFlag(WhereClauseTarget flag)
            {
                return (_target & flag) != 0;
            }

            protected override void VisitSelect(Stmt.Select stmt)
            {
                if (stmt.CteStmt != null)
                {
                    foreach (CteDefinition cte in stmt.CteStmt.Ctes)
                    {
                        HandleQueryExpression(cte.Query.Query, WhereClauseTarget.Ctes);
                    }
                }
                HandleQueryExpression(stmt.Query, WhereClauseTarget.OutermostQuery);
            }

            protected override void VisitSubqueryReference(SubqueryReference source)
            {
                HandleQueryExpression(source.Subquery.Query, WhereClauseTarget.FromSubqueries);
            }

            protected override void VisitIn(Predicate.In pred)
            {
                Walk(pred.Expr);
                if (pred.Subquery != null)
                {
                    HandleQueryExpression(pred.Subquery.Query, WhereClauseTarget.InSubqueries);
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
                HandleQueryExpression(pred.Subquery.Query, WhereClauseTarget.ExistsSubqueries);
            }

            protected override void VisitQuantifier(Predicate.Quantifier pred)
            {
                Walk(pred.Left);
                HandleQueryExpression(pred.Subquery.Query, WhereClauseTarget.ScalarSubqueries);
            }

            protected override void VisitSubquery(Expr.Subquery expr)
            {
                HandleQueryExpression(expr.Query, WhereClauseTarget.ScalarSubqueries);
            }

            private void HandleQueryExpression(QueryExpression queryExpr, WhereClauseTarget requiredFlag)
            {
                if (queryExpr is SelectExpression selectExpr)
                {
                    // Walk must happen before adding the condition so that the walk only
                    // traverses the original AST. If the condition is added first and it
                    // contains subqueries (EXISTS, IN, quantifier), walking the freshly-added
                    // WHERE predicate re-enters HandleQueryExpression and causes infinite recursion.
                    WalkSelectExpression(selectExpr);
                    if (HasFlag(requiredFlag))
                    {
                        ProcessSelectExpression(selectExpr);
                    }
                }
                else if (queryExpr is SetOperation setOp)
                {
                    HandleQueryExpression(setOp.Left, requiredFlag);
                    HandleQueryExpression(setOp.Right, requiredFlag);
                }
            }

            private void ProcessSelectExpression(SelectExpression selectExpr)
            {
                if (_hasMixedPrefixes)
                {
                    ProcessMixedCondition(selectExpr);
                }
                else
                {
                    ProcessFullyUnprefixedCondition(selectExpr);
                }
            }

            /// <summary>
            /// All columns in the condition are unprefixed.
            /// For each table that has the columns, add the full condition prefixed with that table.
            /// </summary>
            private void ProcessFullyUnprefixedCondition(SelectExpression selectExpr)
            {
                List<(TableReference Table, string PhysicalName, string EffectiveName)> tables = SelectLevelTableCollector.Collect(selectExpr);

                foreach ((TableReference table, string physicalName, string effectiveName) in tables)
                {
                    if (_columnExists(physicalName, _unprefixedColumnNames))
                    {
                        // Re-parse for each table (AddWhere mutates)
                        Predicate freshCondition = Predicate.ParsePredicate(_condition);
                        ColumnPrefixer prefixer = new ColumnPrefixer(effectiveName, _unprefixedColumnNames);
                        prefixer.Walk(freshCondition);
                        selectExpr.AddWhere(freshCondition);
                    }
                }
            }

            /// <summary>
            /// Condition has both already-prefixed and unprefixed column references.
            /// Decompose into top-level AND conjuncts, then:
            ///   - Conjuncts with only prefixed columns: add once
            ///   - Conjuncts with unprefixed columns: add once per matching table, with prefix
            /// </summary>
            private void ProcessMixedCondition(SelectExpression selectExpr)
            {
                List<(TableReference Table, string PhysicalName, string EffectiveName)> tables = SelectLevelTableCollector.Collect(selectExpr);

                // Decompose the condition into top-level AND conjuncts and classify them
                Predicate parsed = Predicate.ParsePredicate(_condition);
                List<Predicate> conjuncts = FlattenTopLevelAnd(parsed);

                List<string> prefixedOnlySources = new List<string>();
                List<string> unprefixedSources = new List<string>();

                foreach (Predicate conjunct in conjuncts)
                {
                    string source = conjunct.ToSource().TrimStart();
                    if (HasUnprefixedColumns(conjunct))
                    {
                        unprefixedSources.Add(source);
                    }
                    else
                    {
                        prefixedOnlySources.Add(source);
                    }
                }

                // Add prefixed-only conjuncts once (they don't depend on table matching)
                foreach (string source in prefixedOnlySources)
                {
                    Predicate fresh = Predicate.ParsePredicate(source);
                    selectExpr.AddWhere(fresh);
                }

                // Add unprefixed conjuncts per matching table
                if (unprefixedSources.Count > 0)
                {
                    string unprefixedCondition = string.Join(" AND ", unprefixedSources);

                    foreach ((TableReference table, string physicalName, string effectiveName) in tables)
                    {
                        if (_columnExists(physicalName, _unprefixedColumnNames))
                        {
                            Predicate freshCondition = Predicate.ParsePredicate(unprefixedCondition);
                            ColumnPrefixer prefixer = new ColumnPrefixer(effectiveName, _unprefixedColumnNames);
                            prefixer.Walk(freshCondition);
                            selectExpr.AddWhere(freshCondition);
                        }
                    }
                }
            }
        }
    }
}
