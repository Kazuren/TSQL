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
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddCondition(this Stmt stmt, string condition,
            out IReadOnlyDictionary<string, object> parameters,
            IEnumerable<object> values,
            WhereClauseTarget target = WhereClauseTarget.OutermostQuery)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(stmt, condition, values);
            parameters = resolvedParams;
            WhereClauseAppender.AddCondition(stmt, resolvedCondition, target);
            return stmt;
        }

        /// <summary>
        /// Parameterizes all non-NULL literals in this statement, replacing them with
        /// @P0, @P1, etc. Handling paramater name collisions automatically. The parameter dictionary is returned via <paramref name="parameters"/>.
        /// </summary>
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
        /// <remarks>This method mutates the statement in place.</remarks>
        /// <exception cref="ParseError">Thrown when <paramref name="condition"/> is not a valid SQL predicate.</exception>
        public static Stmt AddSchemaAwareCondition(this Stmt stmt, string condition,
            ColumnExistenceChecker columnExists,
            out IReadOnlyDictionary<string, object> parameters,
            IEnumerable<object> values,
            WhereClauseTarget target = WhereClauseTarget.All)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(stmt, condition, values);
            parameters = resolvedParams;
            SchemaAwareConditionAppender.AddCondition(stmt, resolvedCondition, columnExists, target);
            return stmt;
        }

        /// <summary>
        /// Collects all table references and qualified joins found in this statement.
        /// </summary>
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
