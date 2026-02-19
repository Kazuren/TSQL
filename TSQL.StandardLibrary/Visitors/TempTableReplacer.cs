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

            if (collector.MatchedTables.Count == 0)
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

                    var selectExpr = new SelectExpression();
                    selectExpr.Columns.Add(new Expr.Wildcard());
                    selectExpr.Into = new Expr.ObjectIdentifier(new ObjectName("#" + cteDef.Name));
                    selectExpr.From = new FromClause();
                    selectExpr.From.TableSources.Add(
                        new TableReference(new Expr.ObjectIdentifier(new ObjectName(cteDef.Name))));

                    var cteSelect = new Stmt.Select(selectExpr);
                    cteSelect.CteStmt = cte;
                    cteSelectIntos.Add(cteSelect);
                }
            }

            // Mutate all matched table references
            foreach (TableReference tableRef in collector.MatchedTables)
            {
                string objName = tableRef.TableName.ObjectName.Name;
                tableRef.TableName = new Expr.ObjectIdentifier(new ObjectName("#" + objName));
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

                selectExpr.Into = new Expr.ObjectIdentifier(new ObjectName("#" + objectName));
                selectExpr.From = new FromClause();
                selectExpr.From.TableSources.Add(new TableReference(originalTableName));

                allStatements.Add(new Stmt.Select(selectExpr));
            }

            // CTE SELECT INTOs
            foreach (Stmt cteSelectInto in cteSelectIntos)
            {
                allStatements.Add(cteSelectInto);
            }

            // Modified query
            allStatements.Add(stmt);

            return new Script(allStatements);
        }

        private class Collector : SqlWalker
        {
            private readonly HashSet<string> _targetNames;

            public List<TableReference> MatchedTables { get; } = new List<TableReference>();
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
