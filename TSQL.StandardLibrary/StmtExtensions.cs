using System;
using System.Collections.Generic;

namespace TSQL.StandardLibrary.Visitors
{
    public static class StmtExtensions
    {
        /// <summary>
        /// Appends a WHERE condition to SELECT statements within this statement.
        /// Use <see cref="WhereClauseTarget"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        /// <param name="stmt">The statement to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"Active = 1"</c>).</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition, WhereClauseTarget target = WhereClauseTarget.OutermostQuery)
        {
            WhereClauseAppender.AddCondition(stmt, condition, target);
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
            return AddCondition(stmt, condition, values, WhereClauseTarget.OutermostQuery, out parameters);
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
            WhereClauseTarget target,
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
        /// <returns>The same <paramref name="stmt"/> instance, for chaining.</returns>
        /// <remarks>This method mutates the statement in place.</remarks>
        public static Stmt Parameterize(this Stmt stmt, out IReadOnlyDictionary<string, object> parameters)
        {
            parameters = LiteralParameterizer.Parameterize(stmt);
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
            WhereClauseTarget target = WhereClauseTarget.All)
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
            return AddSchemaAwareCondition(stmt, condition, values, columnExists, WhereClauseTarget.All, out parameters);
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
            WhereClauseTarget target,
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
            ColumnReferenceScope scope = ColumnReferenceScope.OutermostQuery,
            ColumnReferenceClause clauses = ColumnReferenceClause.Select)
        {
            return ColumnReferenceCollector.Collect(stmt, scope, clauses);
        }
    }
}
