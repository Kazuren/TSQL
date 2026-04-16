using System;
using System.Collections.Generic;
using TSQL.AST;

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
        /// <param name="target">Which query levels receive the condition.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition, QueryScope target = QueryScope.OutermostQuery)
        {
            WhereClauseAppender.AddCondition(stmt, condition, target);
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
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition, string targetTable,
            QueryScope traverse = QueryScope.All,
            QueryScope mutate = QueryScope.All)
        {
            var walker = new TableScopedWalker(targetTable, traverse, mutate, selectExpr =>
            {
                AST.Predicate predicate = AST.Predicate.ParsePredicate(condition);
                selectExpr.AddWhere(predicate);
            });
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
        /// Appends a WHERE condition to SELECT statements, but only for tables that contain
        /// all referenced columns (verified via the <paramref name="columnExists"/> callback).
        /// Unprefixed column references are automatically prefixed with the table alias/name.
        /// If ALL columns are already prefixed, falls back to regular AddCondition behavior.
        /// Defaults to all query levels (outermost + subqueries + CTEs).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"TenantId = 1"</c>).</param>
        /// <param name="columnExists">Callback that returns true if a table contains all of the given columns.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddSchemaAwareCondition(this Stmt stmt, string condition,
            ColumnExistenceChecker columnExists,
            QueryScope target = QueryScope.All)
        {
            SchemaAwareConditionAppender.AddCondition(stmt, condition, columnExists, target);
            return stmt;
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements, but only for tables
        /// that contain all referenced columns (verified via the <paramref name="columnExists"/> callback).
        /// Variables in the condition that collide with existing variables in the statement
        /// are automatically renamed. The final parameter dictionary is returned via <paramref name="parameters"/>.
        /// Unprefixed column references are automatically prefixed with the table alias/name.
        /// Defaults to all query levels (outermost + subqueries + CTEs).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables (e.g. <c>"TenantId = @TenantId"</c>).</param>
        /// <param name="values">Parameter values. Each element may be a raw value, a <c>(string name, object value)</c> tuple, or a <see cref="KeyValuePair{TKey,TValue}"/>.</param>
        /// <param name="columnExists">Callback that returns true if a table contains all of the given columns.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping after any collision renaming.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> count does not match the number of variables in the condition.</exception>
        public static Stmt AddSchemaAwareCondition(this Stmt stmt, string condition,
            IEnumerable<object> values,
            ColumnExistenceChecker columnExists,
            out IReadOnlyDictionary<string, object> parameters)
        {
            return AddSchemaAwareCondition(stmt, condition, values, columnExists, QueryScope.All, out parameters);
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements, but only for tables
        /// that contain all referenced columns (verified via the <paramref name="columnExists"/> callback).
        /// Variables in the condition that collide with existing variables in the statement
        /// are automatically renamed. The final parameter dictionary is returned via <paramref name="parameters"/>.
        /// Unprefixed column references are automatically prefixed with the table alias/name.
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables (e.g. <c>"TenantId = @TenantId"</c>).</param>
        /// <param name="values">Parameter values. Each element may be a raw value, a <c>(string name, object value)</c> tuple, or a <see cref="KeyValuePair{TKey,TValue}"/>.</param>
        /// <param name="columnExists">Callback that returns true if a table contains all of the given columns.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping after any collision renaming.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> count does not match the number of variables in the condition.</exception>
        public static Stmt AddSchemaAwareCondition(this Stmt stmt, string condition,
            IEnumerable<object> values,
            ColumnExistenceChecker columnExists,
            QueryScope target,
            out IReadOnlyDictionary<string, object> parameters)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(stmt, condition, values);
            parameters = resolvedParams;
            SchemaAwareConditionAppender.AddCondition(stmt, resolvedCondition, columnExists, target);
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
    }
}
