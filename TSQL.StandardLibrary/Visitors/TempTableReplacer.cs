using System;
using System.Collections.Generic;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class TempTableReplacer
    {
        public static Script Replace(Stmt stmt, string[] tableNames)
        {
            if (tableNames == null || tableNames.Length == 0)
            {
                return new Script(new[] { stmt });
            }

            var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in tableNames)
            {
                targetSet.Add(name);
            }

            // Record CTE names and indices before walking
            var cteNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Stmt.Select selectStmt = stmt as Stmt.Select;
            if (selectStmt?.CteStmt != null)
            {
                for (int i = 0; i < selectStmt.CteStmt.Ctes.Count; i++)
                {
                    cteNameToIndex[selectStmt.CteStmt.Ctes[i].Name] = i;
                }
            }

            // Phase 1: Walk the AST to collect references
            var collector = new Collector(targetSet);
            collector.Walk(stmt);

            if (!collector.HasMatches)
            {
                return new Script(new[] { stmt });
            }

            // Classify matches into CTE vs regular table
            var regularMatches = new List<TableReference>();
            var cteMatchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (TableReference tableRef in collector.MatchedTables)
            {
                string objectName = tableRef.TableName.ObjectName.Name;
                if (cteNameToIndex.ContainsKey(objectName))
                {
                    cteMatchedNames.Add(objectName);
                }
                else
                {
                    regularMatches.Add(tableRef);
                }
            }

            // Phase 2: Column mapping for regular tables
            var qualifierToTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (TableReference tableRef in regularMatches)
            {
                string objName = tableRef.TableName.ObjectName.Name;
                string key = objName.ToUpperInvariant();
                qualifierToTable[objName] = key;
                if (tableRef.Alias != null)
                {
                    qualifierToTable[tableRef.Alias.Name] = key;
                }
            }

            var starTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tableColumnList = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var tableColumnDedup = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            if (!collector.HasBareStar)
            {
                foreach (Expr.QualifiedWildcard qw in collector.QualifiedWildcards)
                {
                    if (qw.ObjectName != null && qualifierToTable.TryGetValue(qw.ObjectName.Name, out string tableKey))
                    {
                        starTables.Add(tableKey);
                    }
                }

                foreach (Expr.ColumnIdentifier col in collector.ColumnIdentifiers)
                {
                    if (col.ObjectName != null && qualifierToTable.TryGetValue(col.ObjectName.Name, out string tableKey))
                    {
                        if (!tableColumnDedup.TryGetValue(tableKey, out var seen))
                        {
                            seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            tableColumnDedup[tableKey] = seen;
                            tableColumnList[tableKey] = new List<string>();
                        }
                        if (seen.Add(col.ColumnName.Name))
                        {
                            tableColumnList[tableKey].Add(col.ColumnName.Lexeme);
                        }
                    }
                }
            }

            // Phase 3: Capture original ObjectIdentifiers BEFORE mutation
            var regularInfo = new Dictionary<string, (Expr.ObjectIdentifier OriginalTableName, string ObjectName)>(StringComparer.OrdinalIgnoreCase);
            var regularOrder = new List<string>();

            foreach (TableReference tableRef in regularMatches)
            {
                string objName = tableRef.TableName.ObjectName.Name;
                string key = objName.ToUpperInvariant();
                if (!regularInfo.ContainsKey(key))
                {
                    regularInfo[key] = (tableRef.TableName, objName);
                    regularOrder.Add(key);
                }
            }

            // Build CTE SELECT INTO ASTs BEFORE mutation
            // (so we capture CteDefinition references while they're still in the original list)
            var cteSelectIntos = new List<Stmt>();
            if (selectStmt?.CteStmt != null && cteMatchedNames.Count > 0)
            {
                for (int i = 0; i < selectStmt.CteStmt.Ctes.Count; i++)
                {
                    CteDefinition cteDef = selectStmt.CteStmt.Ctes[i];
                    if (!cteMatchedNames.Contains(cteDef.Name))
                    {
                        continue;
                    }

                    var cte = new Cte();
                    for (int j = 0; j <= i; j++)
                    {
                        cte.Ctes.Add(selectStmt.CteStmt.Ctes[j]);
                    }

                    var cteSelect = SelectStarInto(cteDef.Name,
                        new TableReference(new Expr.ObjectIdentifier(new ObjectName(cteDef.Name))));
                    cteSelect.CteStmt = cte;
                    cteSelectIntos.Add(cteSelect);
                }
            }

            // Mutate all matched table references
            foreach (TableReference tableRef in collector.MatchedTables)
            {
                string objName = tableRef.TableName.ObjectName.Name;
                tableRef.TableName = TempTableIdentifier(objName);
            }

            // Remove matched CTE definitions from the final query
            if (selectStmt?.CteStmt != null && cteMatchedNames.Count > 0)
            {
                bool allMatched = true;
                for (int i = 0; i < selectStmt.CteStmt.Ctes.Count; i++)
                {
                    if (!cteMatchedNames.Contains(selectStmt.CteStmt.Ctes[i].Name))
                    {
                        allMatched = false;
                        break;
                    }
                }

                if (allMatched)
                {
                    selectStmt.CteStmt = null;
                }
                else
                {
                    var newCtes = new SyntaxElementList<CteDefinition>();
                    for (int i = 0; i < selectStmt.CteStmt.Ctes.Count; i++)
                    {
                        if (!cteMatchedNames.Contains(selectStmt.CteStmt.Ctes[i].Name))
                        {
                            newCtes.Add(selectStmt.CteStmt.Ctes[i]);
                        }
                    }
                    selectStmt.CteStmt.Ctes = newCtes;
                }
            }

            // Phase 4: Build output as list of statements
            var allStatements = new List<Stmt>();

            // Regular table SELECT INTOs
            foreach (string key in regularOrder)
            {
                var (originalTableName, objectName) = regularInfo[key];
                bool useStar = collector.HasBareStar || starTables.Contains(key) || !tableColumnList.ContainsKey(key);

                var selectExpr = new SelectExpression();

                if (useStar)
                {
                    selectExpr.Columns.Add(new Expr.Wildcard());
                }
                else
                {
                    List<string> columns = tableColumnList[key];
                    for (int i = 0; i < columns.Count; i++)
                    {
                        selectExpr.Columns.Add(new SelectColumn(
                            new Expr.ColumnIdentifier(new ColumnName(columns[i])), null));
                    }
                }

                selectExpr.Into = TempTableIdentifier(objectName);
                selectExpr.From = new FromClause();
                selectExpr.From.TableSources.Add(new TableReference(originalTableName));

                allStatements.Add(new Stmt.Select(selectExpr));
            }

            allStatements.AddRange(cteSelectIntos);

            // Derived table (SubqueryReference) SELECT INTOs
            SelectExpression outerSelect = selectStmt?.Query as SelectExpression;
            foreach (SubqueryReference subqRef in collector.MatchedSubqueries)
            {
                string aliasName = subqRef.Alias.Name;

                if (subqRef.Subquery.Query is SelectExpression innerSelect)
                {
                    innerSelect.Into = TempTableIdentifier(aliasName);
                    allStatements.Add(new Stmt.Select(innerSelect));
                }
                else
                {
                    subqRef.Alias = null;
                    allStatements.Add(SelectStarInto(aliasName, subqRef));
                }

                if (outerSelect?.From != null)
                {
                    var tempRef = new TableReference(TempTableIdentifier(aliasName));
                    ReplaceTableSource(outerSelect.From, subqRef, tempRef);
                }
            }

            // Rowset function SELECT INTOs
            foreach (RowsetFunctionReference rowsetRef in collector.MatchedRowsetFunctions)
            {
                string aliasName = rowsetRef.Alias.Name;
                rowsetRef.Alias = null;
                allStatements.Add(SelectStarInto(aliasName, rowsetRef));

                if (outerSelect?.From != null)
                {
                    var tempRef = new TableReference(TempTableIdentifier(aliasName));
                    ReplaceTableSource(outerSelect.From, rowsetRef, tempRef);
                }
            }

            // Modified query
            allStatements.Add(stmt);

            return new Script(allStatements);
        }

        private static Expr.ObjectIdentifier TempTableIdentifier(string name) =>
            new Expr.ObjectIdentifier(new ObjectName("#" + name));

        private static Stmt.Select SelectStarInto(string tempName, TableSource source)
        {
            var selectExpr = new SelectExpression();
            selectExpr.Columns.Add(new Expr.Wildcard());
            selectExpr.Into = TempTableIdentifier(tempName);
            selectExpr.From = new FromClause();
            selectExpr.From.TableSources.Add(source);
            return new Stmt.Select(selectExpr);
        }

        private static bool ReplaceTableSource(FromClause from, TableSource target, TableSource replacement)
        {
            for (int i = 0; i < from.TableSources.Count; i++)
            {
                if (ReferenceEquals(from.TableSources[i], target))
                {
                    from.TableSources[i] = replacement;
                    return true;
                }

                if (ReplaceInNode(from.TableSources[i], target, replacement))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReplaceInNode(TableSource node, TableSource target, TableSource replacement)
        {
            switch (node)
            {
                case QualifiedJoin qj:
                    return ReplaceInBinaryJoin(qj.Left, qj.Right, v => qj.Left = v, v => qj.Right = v, target, replacement);
                case CrossJoin cj:
                    return ReplaceInBinaryJoin(cj.Left, cj.Right, v => cj.Left = v, v => cj.Right = v, target, replacement);
                case ApplyJoin aj:
                    return ReplaceInBinaryJoin(aj.Left, aj.Right, v => aj.Left = v, v => aj.Right = v, target, replacement);
                case ParenthesizedTableSource pts:
                    if (ReferenceEquals(pts.Inner, target))
                    {
                        pts.Inner = replacement;
                        return true;
                    }
                    return ReplaceInNode(pts.Inner, target, replacement);
                default:
                    return false;
            }
        }

        private static bool ReplaceInBinaryJoin(
            TableSource left, TableSource right,
            Action<TableSource> setLeft, Action<TableSource> setRight,
            TableSource target, TableSource replacement)
        {
            if (ReferenceEquals(left, target))
            {
                setLeft(replacement);
                return true;
            }
            if (ReferenceEquals(right, target))
            {
                setRight(replacement);
                return true;
            }
            return ReplaceInNode(left, target, replacement)
                || ReplaceInNode(right, target, replacement);
        }

        private class Collector : SqlWalker
        {
            private readonly HashSet<string> _targetNames;

            public List<TableReference> MatchedTables { get; } = new List<TableReference>();
            public List<SubqueryReference> MatchedSubqueries { get; } = new List<SubqueryReference>();
            public List<RowsetFunctionReference> MatchedRowsetFunctions { get; } = new List<RowsetFunctionReference>();

            public bool HasMatches =>
                MatchedTables.Count > 0
                || MatchedSubqueries.Count > 0
                || MatchedRowsetFunctions.Count > 0;

            public List<Expr.ColumnIdentifier> ColumnIdentifiers { get; } = new List<Expr.ColumnIdentifier>();
            public List<Expr.QualifiedWildcard> QualifiedWildcards { get; } = new List<Expr.QualifiedWildcard>();
            public bool HasBareStar { get; private set; }

            public Collector(HashSet<string> targetNames)
            {
                _targetNames = targetNames;
            }

            protected override void VisitTableReference(TableReference source)
            {
                if (_targetNames.Contains(source.TableName.ObjectName.Name))
                {
                    MatchedTables.Add(source);
                }
            }

            protected override void VisitSubqueryReference(SubqueryReference source)
            {
                if (source.Alias != null && _targetNames.Contains(source.Alias.Name))
                {
                    MatchedSubqueries.Add(source);
                }
                base.VisitSubqueryReference(source);
            }

            protected override void VisitRowsetFunctionReference(RowsetFunctionReference source)
            {
                if (source.Alias != null && _targetNames.Contains(source.Alias.Name))
                {
                    MatchedRowsetFunctions.Add(source);
                }
                base.VisitRowsetFunctionReference(source);
            }

            protected override void VisitColumnIdentifier(Expr.ColumnIdentifier expr)
            {
                ColumnIdentifiers.Add(expr);
            }

            protected override void VisitWildcard(Expr.Wildcard expr)
            {
                HasBareStar = true;
            }

            protected override void VisitQualifiedWildcard(Expr.QualifiedWildcard expr)
            {
                QualifiedWildcards.Add(expr);
            }
        }
    }
}
