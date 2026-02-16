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
