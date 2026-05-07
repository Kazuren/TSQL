namespace TSQL.StandardLibrary.Visitors
{
    public static class StmtExtensions
    {
        /// <summary>
        /// Appends a WHERE condition to SELECT statements within this statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"Active = 1"</c>).</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <param name="allowLeadingWhereKeyword">
        /// When true (the default), a leading WHERE keyword is silently consumed if present.
        /// This allows callers to pass either <c>WHERE x = 1</c> or <c>x = 1</c>.
        /// </param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery, bool allowLeadingWhereKeyword = true)
        {
            WhereClauseAppender.AddCondition(stmt, condition, target, allowLeadingWhereKeyword);
            return stmt;
        }

        /// <summary>
        /// Appends a WHERE condition to every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// <paramref name="traverse"/> controls which query-level categories the walker enters;
        /// <paramref name="mutate"/> controls which of those categories are eligible for the
        /// WHERE injection (in addition to the <paramref name="targetTable"/> match).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"Id = 5"</c>).</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the condition.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="allowLeadingWhereKeyword">
        /// When true (the default), a leading WHERE keyword is silently consumed if present.
        /// </param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All,
            bool allowLeadingWhereKeyword = true)
        {
            WhereClauseAppender.AddCondition(stmt, condition, targetTable, traverse, mutate, allowLeadingWhereKeyword);
            return stmt;
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements within this statement.
        /// Variables in the condition that collide with existing variables in the statement
        /// are automatically renamed. The final parameter dictionary is returned via <paramref name="parameters"/>.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables (e.g. <c>"TenantId = @TenantId"</c>).</param>
        /// <param name="values">Parameter values. Each element may be a raw value, a <c>(string name, object value)</c> tuple, or a <see cref="KeyValuePair{TKey,TValue}"/>.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping after any collision renaming.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> count does not match the number of variables in the condition.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition,
            IEnumerable<object> values,
            out IReadOnlyDictionary<string, object> parameters)
        {
            return AddCondition(stmt, condition, values, QueryScope.OutermostQuery, out parameters);
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements within this statement.
        /// Variables in the condition that collide with existing variables in the statement
        /// are automatically renamed. The final parameter dictionary is returned via <paramref name="parameters"/>.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables (e.g. <c>"TenantId = @TenantId"</c>).</param>
        /// <param name="values">Parameter values. Each element may be a raw value, a <c>(string name, object value)</c> tuple, or a <see cref="KeyValuePair{TKey,TValue}"/>.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping after any collision renaming.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> count does not match the number of variables in the condition.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition,
            IEnumerable<object> values,
            QueryScope target,
            out IReadOnlyDictionary<string, object> parameters)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(stmt, condition, values);
            parameters = resolvedParams;
            WhereClauseAppender.AddCondition(stmt, resolvedCondition, target);
            return stmt;
        }

        /// <summary>
        /// Appends a WHERE condition to SELECT statements, but only for tables where
        /// the <paramref name="shouldApply"/> callback returns true.
        /// Unprefixed column references are automatically prefixed with the table alias/name.
        /// If ALL columns are already prefixed, falls back to regular AddCondition behavior.
        /// Defaults to all query levels (outermost + subqueries + CTEs).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"TenantId = 1"</c>).</param>
        /// <param name="shouldApply">Callback that receives a <see cref="ConditionContext"/> and returns true to apply the condition to that table.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddConditionWhen(this Stmt stmt, string condition,
            ShouldApply shouldApply,
            QueryScope target = QueryScope.All)
        {
            ConditionalConditionAppender.AddCondition(stmt, condition, shouldApply, target);
            return stmt;
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements, but only for tables
        /// where the <paramref name="shouldApply"/> callback returns true.
        /// Variables in the condition that collide with existing variables in the statement
        /// are automatically renamed. The final parameter dictionary is returned via <paramref name="parameters"/>.
        /// Unprefixed column references are automatically prefixed with the table alias/name.
        /// Defaults to all query levels (outermost + subqueries + CTEs).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables (e.g. <c>"TenantId = @TenantId"</c>).</param>
        /// <param name="values">Parameter values. Each element may be a raw value, a <c>(string name, object value)</c> tuple, or a <see cref="KeyValuePair{TKey,TValue}"/>.</param>
        /// <param name="shouldApply">Callback that receives a <see cref="ConditionContext"/> and returns true to apply the condition to that table.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping after any collision renaming.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> count does not match the number of variables in the condition.</exception>
        public static Stmt AddConditionWhen(this Stmt stmt, string condition,
            IEnumerable<object> values,
            ShouldApply shouldApply,
            out IReadOnlyDictionary<string, object> parameters)
        {
            return AddConditionWhen(stmt, condition, values, shouldApply, QueryScope.All, out parameters);
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements, but only for tables
        /// where the <paramref name="shouldApply"/> callback returns true.
        /// Variables in the condition that collide with existing variables in the statement
        /// are automatically renamed. The final parameter dictionary is returned via <paramref name="parameters"/>.
        /// Unprefixed column references are automatically prefixed with the table alias/name.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables (e.g. <c>"TenantId = @TenantId"</c>).</param>
        /// <param name="values">Parameter values. Each element may be a raw value, a <c>(string name, object value)</c> tuple, or a <see cref="KeyValuePair{TKey,TValue}"/>.</param>
        /// <param name="shouldApply">Callback that receives a <see cref="ConditionContext"/> and returns true to apply the condition to that table.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping after any collision renaming.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> count does not match the number of variables in the condition.</exception>
        public static Stmt AddConditionWhen(this Stmt stmt, string condition,
            IEnumerable<object> values,
            ShouldApply shouldApply,
            QueryScope target,
            out IReadOnlyDictionary<string, object> parameters)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(stmt, condition, values);
            parameters = resolvedParams;
            ConditionalConditionAppender.AddCondition(stmt, resolvedCondition, shouldApply, target);
            return stmt;
        }

        /// <summary>
        /// Prepends a column (parsed from a SQL source fragment) to the outermost SELECT in this statement.
        /// For UNION/INTERSECT/EXCEPT, adds to the first SELECT (both sides are modified for symmetry).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="columnSource">Free-form SQL column source (expression optionally followed by an alias).</param>
        /// <param name="target">Which query scopes to modify. Defaults to outermost query.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="columnSource"/> is not valid SQL.</exception>
        public static Stmt AddSelectColumn(this Stmt stmt, string columnSource, QueryScope target = QueryScope.OutermostQuery)
        {
            var walker = new ActionScopedWalker(QueryScope.All, target,
                selectExpr => selectExpr.PrependColumn(columnSource));
            walker.Walk(stmt);
            return stmt;
        }

        /// <summary>
        /// Prepends a column (parsed from a SQL source fragment) to every SELECT within this
        /// statement whose FROM clause references <paramref name="targetTable"/>, subject to
        /// traversal and mutation scope. <paramref name="traverse"/> controls which query-level
        /// categories the walker enters; <paramref name="mutate"/> controls which of those
        /// categories are eligible for the column injection.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="columnSource">Free-form SQL column source (expression optionally followed by an alias).</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the column.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="columnSource"/> is not valid SQL.</exception>
        public static Stmt AddSelectColumn(this Stmt stmt, string columnSource, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            var walker = new TableScopedWalker(targetTable, traverse, mutate,
                selectExpr => selectExpr.PrependColumn(columnSource));
            walker.Walk(stmt);
            return stmt;
        }

        /// <summary>
        /// Sets a <c>SELECT INTO</c> clause on the outermost SELECT of this statement.
        /// If the outermost query is a UNION/INTERSECT/EXCEPT, the INTO is placed on the
        /// first SELECT (the only position T-SQL permits). If an INTO already exists, it is
        /// replaced.
        /// </summary>
        /// <param name="stmt">The SELECT statement to modify.</param>
        /// <param name="tableName">Target table name. May be a simple name (<c>"#tmp"</c>) or
        /// a dotted name up to four parts (<c>"server.db.schema.table"</c>).</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tableName"/> has more than four dotted parts.</exception>
        public static Stmt.Select AddInto(this Stmt.Select stmt, string tableName)
        {
            SelectExpression outermost = FindOutermostSelectExpression(stmt.Query);
            outermost.Into = Expr.ObjectIdentifier.Parse(tableName);
            return stmt;
        }

        private static SelectExpression FindOutermostSelectExpression(QueryExpression queryExpr)
        {
            if (queryExpr is SelectExpression selectExpr)
            {
                return selectExpr;
            }
            if (queryExpr is SetOperation setOp)
            {
                return FindOutermostSelectExpression(setOp.Left);
            }
            if (queryExpr is ParenthesizedQuery parenQuery)
            {
                return FindOutermostSelectExpression(parenQuery.Inner);
            }
            throw new ArgumentException("Cannot add INTO to this query expression; no SelectExpression found.");
        }


        /// <summary>
        /// Parameterizes all non-NULL literals in this statement, replacing them with
        /// @P0, @P1, etc. Handles parameter name collisions automatically. The parameter dictionary is returned via <paramref name="parameters"/>.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="parameters">Receives the generated parameter name-to-value mapping.</param>
        /// <param name="reservedNames">Parameter names to avoid even if they don't appear in the SQL text
        /// (e.g. caller-provided parameters that will be assigned later). Compared case-insensitively.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        public static Stmt Parameterize(this Stmt stmt, out IReadOnlyDictionary<string, object> parameters,
            IEnumerable<string> reservedNames = null)
        {
            parameters = LiteralParameterizer.Parameterize(stmt, reservedNames);
            return stmt;
        }

        /// <summary>
        /// Replaces all matching table references with temp table references (#TableName)
        /// and prepends SELECT INTO statements to materialize the data.
        /// Regular tables get column-specific SELECT INTOs (only columns referenced across all clauses).
        /// CTE matches get materialized via SELECT * INTO with prerequisite CTEs.
        /// Matched CTE definitions are removed from the output query.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="tableNames">Names of tables or CTEs to materialize as temp tables.</param>
        /// <returns>A <see cref="Script"/> containing the SELECT INTO statements followed by the modified query.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        public static Script ReplaceWithTempTables(this Stmt stmt, params string[] tableNames)
        {
            return TempTableReplacer.Replace(stmt, tableNames);
        }

        /// <summary>
        /// Collects all table references and qualified joins found in this statement.
        /// </summary>
        /// <param name="stmt">The statement to inspect.</param>
        /// <returns>A <see cref="TableReferences"/> containing the collected tables and joins.</returns>
        /// <remarks>This method does not modify the statement.</remarks>
        public static TableReferences CollectTableReferences(this Stmt stmt)
        {
            return TableReferenceCollector.Collect(stmt);
        }

        /// <summary>
        /// Collects the SELECT columns from this statement in declaration order.
        /// Returns all <see cref="SelectItem"/> entries: <see cref="SelectColumn"/> (with expression and alias),
        /// <see cref="Expr.Wildcard"/> (<c>*</c>), and <see cref="Expr.QualifiedWildcard"/> (<c>T.*</c>).
        /// </summary>
        /// <param name="stmt">The statement to inspect.</param>
        /// <param name="scope">Which query levels to traverse (outermost, CTEs, subqueries).</param>
        /// <returns>The collected SELECT items in declaration order.</returns>
        /// <remarks>This method does not modify the statement.</remarks>
        public static IReadOnlyList<SelectItem> CollectSelectColumns(
            this Stmt stmt,
            QueryScope scope = QueryScope.OutermostQuery)
        {
            return SelectColumnCollector.Collect(stmt, scope);
        }

        /// <summary>
        /// Collects column references found in this statement.
        /// Use <paramref name="scope"/> to control which query levels are traversed
        /// and <paramref name="clauses"/> to control which SQL clauses are collected from.
        /// Both must be satisfied for a column to be included. Wildcards are excluded.
        /// </summary>
        /// <param name="stmt">The statement to inspect.</param>
        /// <param name="scope">Which query levels to traverse (outermost, CTEs, subqueries).</param>
        /// <param name="clauses">Which SQL clauses to collect from (SELECT, WHERE, GROUP BY, etc.).</param>
        /// <returns>The collected column identifiers.</returns>
        /// <remarks>This method does not modify the statement.</remarks>
        public static System.Collections.Generic.IReadOnlyList<Expr.ColumnIdentifier> CollectColumnReferences(
            this Stmt stmt,
            QueryScope scope = QueryScope.OutermostQuery,
            ColumnReferenceClause clauses = ColumnReferenceClause.Select)
        {
            return ColumnReferenceCollector.Collect(stmt, scope, clauses);
        }

        /// <summary>
        /// Appends a HAVING condition to SELECT statements within this statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"COUNT(*) > 10"</c>).</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <param name="allowLeadingHavingKeyword">
        /// When true (the default), a leading HAVING keyword is silently consumed if present.
        /// This allows callers to pass either <c>HAVING COUNT(*) > 5</c> or <c>COUNT(*) > 5</c>.
        /// </param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddHaving(this Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery, bool allowLeadingHavingKeyword = true)
        {
            HavingAppender.AddHaving(stmt, condition, target, allowLeadingHavingKeyword);
            return stmt;
        }

        /// <summary>
        /// Appends a HAVING condition to every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"COUNT(*) > 10"</c>).</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the condition.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="allowLeadingHavingKeyword">
        /// When true (the default), a leading HAVING keyword is silently consumed if present.
        /// </param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddHaving(this Stmt stmt, string condition, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All,
            bool allowLeadingHavingKeyword = true)
        {
            HavingAppender.AddHaving(stmt, condition, targetTable, traverse, mutate, allowLeadingHavingKeyword);
            return stmt;
        }

        /// <summary>
        /// Appends GROUP BY items to SELECT statements within this statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="groupByItems">GROUP BY items to append (e.g. <c>"Category, Region"</c>).</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="groupByItems"/> is not valid SQL.</exception>
        public static Stmt AddGroupBy(this Stmt stmt, string groupByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            GroupByAppender.AddGroupBy(stmt, groupByItems, target);
            return stmt;
        }

        /// <summary>
        /// Appends GROUP BY items to every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="groupByItems">GROUP BY items to append (e.g. <c>"Category, Region"</c>).</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the GROUP BY items.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="groupByItems"/> is not valid SQL.</exception>
        public static Stmt AddGroupBy(this Stmt stmt, string groupByItems, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            GroupByAppender.AddGroupBy(stmt, groupByItems, targetTable, traverse, mutate);
            return stmt;
        }

        /// <summary>
        /// Replaces the GROUP BY clause in SELECT statements within this statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="groupByItems">GROUP BY items to set (e.g. <c>"NewCategory"</c>). Pass empty string to clear.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="groupByItems"/> is not valid SQL.</exception>
        public static Stmt ReplaceGroupBy(this Stmt stmt, string groupByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            GroupByAppender.ReplaceGroupBy(stmt, groupByItems, target);
            return stmt;
        }

        /// <summary>
        /// Replaces the GROUP BY clause in every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="groupByItems">GROUP BY items to set (e.g. <c>"NewCategory"</c>). Pass empty string to clear.</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the GROUP BY replacement.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="groupByItems"/> is not valid SQL.</exception>
        public static Stmt ReplaceGroupBy(this Stmt stmt, string groupByItems, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            GroupByAppender.ReplaceGroupBy(stmt, groupByItems, targetTable, traverse, mutate);
            return stmt;
        }

        /// <summary>
        /// Appends ORDER BY items to SELECT statements within this statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="orderByItems">ORDER BY items to append (e.g. <c>"Name ASC, CreatedDate DESC"</c>).</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="orderByItems"/> is not valid SQL.</exception>
        public static Stmt AddOrderBy(this Stmt stmt, string orderByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            OrderByAppender.AddOrderBy(stmt, orderByItems, target);
            return stmt;
        }

        /// <summary>
        /// Appends ORDER BY items to every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="orderByItems">ORDER BY items to append (e.g. <c>"Name ASC"</c>).</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the ORDER BY items.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="orderByItems"/> is not valid SQL.</exception>
        public static Stmt AddOrderBy(this Stmt stmt, string orderByItems, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            OrderByAppender.AddOrderBy(stmt, orderByItems, targetTable, traverse, mutate);
            return stmt;
        }

        /// <summary>
        /// Replaces the ORDER BY clause in SELECT statements within this statement.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="orderByItems">ORDER BY items to set (e.g. <c>"NewColumn DESC"</c>). Pass empty string to clear.</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="orderByItems"/> is not valid SQL.</exception>
        public static Stmt ReplaceOrderBy(this Stmt stmt, string orderByItems, QueryScope target = QueryScope.OutermostQuery)
        {
            OrderByAppender.ReplaceOrderBy(stmt, orderByItems, target);
            return stmt;
        }

        /// <summary>
        /// Replaces the ORDER BY clause in every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="orderByItems">ORDER BY items to set (e.g. <c>"NewColumn DESC"</c>). Pass empty string to clear.</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the ORDER BY replacement.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="orderByItems"/> is not valid SQL.</exception>
        public static Stmt ReplaceOrderBy(this Stmt stmt, string orderByItems, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            OrderByAppender.ReplaceOrderBy(stmt, orderByItems, targetTable, traverse, mutate);
            return stmt;
        }

        /// <summary>
        /// Appends a JOIN clause to SELECT statements within this statement.
        /// The join is added to the rightmost table source in the FROM clause.
        /// Use <see cref="QueryScope"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="joinFragment">JOIN clause to append (e.g. <c>"INNER JOIN Orders o ON o.CustomerId = c.Id"</c>).</param>
        /// <param name="target">Which query scopes to modify. Traverses all scopes but only mutates matching ones.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="joinFragment"/> is not a valid JOIN clause.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the query has no FROM clause.</exception>
        public static Stmt AddJoin(this Stmt stmt, string joinFragment, QueryScope target = QueryScope.OutermostQuery)
        {
            JoinAppender.AddJoin(stmt, joinFragment, target);
            return stmt;
        }

        /// <summary>
        /// Appends a JOIN clause to every SELECT within this statement whose FROM clause
        /// references <paramref name="targetTable"/>, subject to traversal and mutation scope.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="joinFragment">JOIN clause to append (e.g. <c>"INNER JOIN Orders o ON o.CustomerId = c.Id"</c>).</param>
        /// <param name="targetTable">Table name whose referencing SELECT(s) receive the JOIN.
        /// Comparison is case-insensitive and honors dotted schema-qualified names.</param>
        /// <param name="traverse">Which query-level categories the walker enters.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <param name="mutate">Which query-level categories are eligible for mutation.
        /// Defaults to <see cref="QueryScope.All"/>.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="joinFragment"/> is not a valid JOIN clause.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the query has no FROM clause.</exception>
        public static Stmt AddJoin(this Stmt stmt, string joinFragment, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            JoinAppender.AddJoin(stmt, joinFragment, targetTable, traverse, mutate);
            return stmt;
        }
    }
}
