using System;
using System.Collections.Generic;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class TempTableReplacer
    {
        /// <summary>
        /// Transforms a SELECT statement by materializing named table sources into temp tables.
        ///
        /// Given:  SELECT u.Name FROM (SELECT Id, Name FROM Users) AS T
        /// With:   ReplaceWithTempTables("T")
        /// Output: SELECT Id, Name INTO #T FROM Users;
        ///         SELECT u.Name FROM #T
        ///
        /// The algorithm works in four phases:
        ///
        /// 1. COLLECT  — Walk the AST to find all table sources whose name matches a target.
        ///               Matches fall into three categories: regular tables, CTEs, and
        ///               derived tables / rowset functions (matched by alias).
        ///
        /// 2. COLUMNS  — For regular tables, determine which columns to include in the
        ///               SELECT INTO. Scans column references and wildcards to build
        ///               a per-table column list (or falls back to SELECT *).
        ///
        /// 3. SNAPSHOT  — Capture original table name identifiers and build CTE SELECT INTO
        ///               statements BEFORE mutating the AST. This is necessary because
        ///               mutation rewrites table names to #temp, and the SELECT INTO
        ///               statements need the original names.
        ///
        /// 4. EMIT     — Assemble the output Script in order:
        ///               a) SELECT INTO for each regular table (with column narrowing)
        ///               b) SELECT * INTO for each matched CTE (with its prerequisite CTEs)
        ///               c) SELECT INTO for each derived table / rowset function
        ///               d) The original query, now referencing #temp tables
        /// </summary>
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

            // Index CTE names so we can distinguish CTE references from regular tables
            // when they share a name with a target.
            var cteNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Stmt.Select selectStmt = stmt as Stmt.Select;
            if (selectStmt?.CteStmt != null)
            {
                for (int i = 0; i < selectStmt.CteStmt.Ctes.Count; i++)
                {
                    cteNameToIndex[selectStmt.CteStmt.Ctes[i].Name] = i;
                }
            }

            // ── Phase 1: COLLECT ──────────────────────────────────────────────
            // Walk the entire AST once to find:
            //   - TableReferences whose object name matches a target
            //   - SubqueryReferences (derived tables) whose alias matches a target
            //   - RowsetFunctionReferences whose alias matches a target
            //   - All column identifiers, wildcards, and qualified wildcards (for phase 2)
            var collector = new Collector(targetSet);
            collector.Walk(stmt);

            if (!collector.HasMatches)
            {
                return new Script(new[] { stmt });
            }

            // Classify TableReference matches: if the name appears in the CTE list,
            // it's a CTE reference; otherwise it's a regular table.
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

            // ── Phase 2: COLUMNS ──────────────────────────────────────────────
            // For each regular table, figure out which columns the query actually uses
            // so the SELECT INTO can be narrowed (e.g., "SELECT Name, Id INTO #Users"
            // instead of "SELECT * INTO #Users"). This maps both table names and aliases
            // to a canonical key, then collects every column reference qualified with
            // that key. Falls back to SELECT * if:
            //   - The query uses a bare * (SELECT *)
            //   - The query uses a qualified wildcard (SELECT u.*)
            //   - No qualified column references were found for that table
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

            // ── Phase 3: SNAPSHOT ─────────────────────────────────────────────
            // Save original table name identifiers before mutation rewrites them.
            // The SELECT INTO statements need "FROM Users" (original), but after
            // mutation the AST will say "FROM #Users".
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

            // Build CTE SELECT INTO statements before mutation for the same reason:
            // each CTE's materialization query includes the CTE definitions up to and
            // including itself (e.g., "WITH A AS (...), B AS (...) SELECT * INTO #B FROM B").
            // These definitions reference the original CTE list which mutation will modify.
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

            // ── MUTATE ────────────────────────────────────────────────────────
            // Rewrite all matched TableReference nodes in-place: "Users" → "#Users".
            // This modifies the original AST so the final query references temp tables.
            foreach (TableReference tableRef in collector.MatchedTables)
            {
                string objName = tableRef.TableName.ObjectName.Name;
                tableRef.TableName = TempTableIdentifier(objName);
            }

            // Remove materialized CTE definitions from the final query's WITH clause.
            // If all CTEs were materialized, drop the WITH clause entirely.
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

            // ── Phase 4: EMIT ─────────────────────────────────────────────────
            // Assemble the output Script. Statement order matters — each SELECT INTO
            // must appear before any query that references its temp table.
            var allStatements = new List<Stmt>();

            // 4a. Regular tables: SELECT [columns] INTO #Table FROM Table
            //     Uses the narrowed column list from phase 2 when possible.
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

            // 4b. CTEs: WITH ... SELECT * INTO #cte FROM cte
            allStatements.AddRange(cteSelectIntos);

            // 4c. Derived tables: materialize the subquery, then swap the
            //     SubqueryReference node in the outer FROM with a TableReference to #alias.
            //     For simple SELECTs, we extract the inner query and add INTO directly.
            //     For set operations (UNION etc.), we wrap the subquery in a new
            //     SELECT * INTO #alias FROM (subquery) statement.
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

            // 4d. Rowset functions and table-valued functions: materialize via
            //     SELECT * INTO #name FROM function(...), then swap the node in the
            //     outer FROM with a TableReference to #name.
            //     When matched by function name (TVFs), preserve the original alias.
            //     When matched by alias (OPENQUERY etc.), use alias as temp table name.
            foreach (RowsetFunctionReference rowsetRef in collector.MatchedRowsetFunctions)
            {
                string functionName = rowsetRef.FunctionCall.Callee.ObjectName.Name;
                bool matchedByFunctionName = targetSet.Contains(functionName);

                string tempName;
                Alias preservedAlias;
                if (matchedByFunctionName)
                {
                    tempName = functionName;
                    preservedAlias = rowsetRef.Alias;
                }
                else
                {
                    tempName = rowsetRef.Alias.Name;
                    preservedAlias = null;
                }

                rowsetRef.Alias = null;
                allStatements.Add(SelectStarInto(tempName, rowsetRef));

                if (outerSelect?.From != null)
                {
                    var tempRef = new TableReference(TempTableIdentifier(tempName));
                    tempRef.Alias = preservedAlias;
                    ReplaceTableSource(outerSelect.From, rowsetRef, tempRef);
                }
            }

            // 4e. The original query, now referencing #temp tables
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

        // The FROM clause is a tree of TableSource nodes. Joins form interior nodes
        // with Left/Right children; leaf nodes are TableReference, SubqueryReference, etc.
        //
        // Example: "FROM Users u INNER JOIN Orders o ON ... CROSS APPLY (SELECT ...) d"
        //
        //   FromClause.TableSources[0]
        //       └─ ApplyJoin
        //            ├─ Left:  QualifiedJoin
        //            │           ├─ Left:  TableReference("Users")   ← leaf
        //            │           └─ Right: TableReference("Orders")  ← leaf
        //            └─ Right: SubqueryReference(...)                 ← leaf
        //
        // To replace a leaf (e.g., swap SubqueryReference for TableReference("#T")),
        // we walk this tree looking for the target by reference equality, then assign
        // the replacement into the parent's Left, Right, or Inner property.
        //
        // ReplaceInNode dispatches on the node type. For join nodes (QualifiedJoin,
        // CrossJoin, ApplyJoin), it delegates to ReplaceInBinaryJoin which checks
        // both children and recurses. The setter delegates (v => qj.Left = v) let
        // ReplaceInBinaryJoin write back into the parent without knowing its concrete type.
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
                else if (_targetNames.Contains(source.FunctionCall.Callee.ObjectName.Name))
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
