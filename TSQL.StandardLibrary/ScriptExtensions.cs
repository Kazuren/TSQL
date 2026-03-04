using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    public static class ScriptExtensions
    {
        // #####################################################################
        // ########################## AddCondition ############################
        // #####################################################################

        /// <summary>
        /// Appends a WHERE condition to all SELECT statements in this script.
        /// Non-SELECT statements are left unchanged.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="condition">A SQL predicate to append (e.g. <c>"Active = 1"</c>).</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script AddCondition(this Script script, string condition,
            WhereClauseTarget target = WhereClauseTarget.OutermostQuery)
        {
            foreach (Stmt stmt in script.Statements)
            {
                WhereClauseAppender.AddCondition(stmt, condition, target);
            }
            return script;
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to all SELECT statements in this script.
        /// Variable collision detection considers all statements in the script.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables.</param>
        /// <param name="values">Parameter values.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script AddCondition(this Script script, string condition,
            IEnumerable<object> values,
            out IReadOnlyDictionary<string, object> parameters)
        {
            return AddCondition(script, condition, values, WhereClauseTarget.OutermostQuery, out parameters);
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to all SELECT statements in this script.
        /// Variable collision detection considers all statements in the script.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables.</param>
        /// <param name="values">Parameter values.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script AddCondition(this Script script, string condition,
            IEnumerable<object> values,
            WhereClauseTarget target,
            out IReadOnlyDictionary<string, object> parameters)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(script.Statements, condition, values);
            parameters = resolvedParams;
            foreach (Stmt stmt in script.Statements)
            {
                WhereClauseAppender.AddCondition(stmt, resolvedCondition, target);
            }
            return script;
        }

        // #####################################################################
        // #################### AddSchemaAwareCondition #######################
        // #####################################################################

        /// <summary>
        /// Appends a WHERE condition to SELECT statements in this script, but only for tables
        /// that contain all referenced columns. Non-SELECT statements are left unchanged.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="condition">A SQL predicate to append.</param>
        /// <param name="columnExists">Callback that returns true if a table contains all of the given columns.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script AddSchemaAwareCondition(this Script script, string condition,
            ColumnExistenceChecker columnExists,
            WhereClauseTarget target = WhereClauseTarget.All)
        {
            foreach (Stmt stmt in script.Statements)
            {
                SchemaAwareConditionAppender.AddCondition(stmt, condition, columnExists, target);
            }
            return script;
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements in this script,
        /// but only for tables that contain all referenced columns.
        /// Variable collision detection considers all statements in the script.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables.</param>
        /// <param name="values">Parameter values.</param>
        /// <param name="columnExists">Callback that returns true if a table contains all of the given columns.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script AddSchemaAwareCondition(this Script script, string condition,
            IEnumerable<object> values,
            ColumnExistenceChecker columnExists,
            out IReadOnlyDictionary<string, object> parameters)
        {
            return AddSchemaAwareCondition(script, condition, values, columnExists,
                WhereClauseTarget.All, out parameters);
        }

        /// <summary>
        /// Appends a WHERE condition with parameter values to SELECT statements in this script,
        /// but only for tables that contain all referenced columns.
        /// Variable collision detection considers all statements in the script.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="condition">A SQL predicate containing @-prefixed variables.</param>
        /// <param name="values">Parameter values.</param>
        /// <param name="columnExists">Callback that returns true if a table contains all of the given columns.</param>
        /// <param name="target">Which query levels receive the condition.</param>
        /// <param name="parameters">Receives the resolved parameter name-to-value mapping.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script AddSchemaAwareCondition(this Script script, string condition,
            IEnumerable<object> values,
            ColumnExistenceChecker columnExists,
            WhereClauseTarget target,
            out IReadOnlyDictionary<string, object> parameters)
        {
            (string resolvedCondition, IReadOnlyDictionary<string, object> resolvedParams)
                = ConditionParameterResolver.Resolve(script.Statements, condition, values);
            parameters = resolvedParams;
            foreach (Stmt stmt in script.Statements)
            {
                SchemaAwareConditionAppender.AddCondition(stmt, resolvedCondition, columnExists, target);
            }
            return script;
        }

        // #####################################################################
        // ########################## Parameterize ############################
        // #####################################################################

        /// <summary>
        /// Parameterizes all non-NULL literals across all statements in this script.
        /// A single parameter counter is shared, producing unique names (@P0, @P1, ...)
        /// across the whole script.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="parameters">Receives the generated parameter name-to-value mapping.</param>
        /// <returns>The same <paramref name="script"/> instance, for chaining.</returns>
        public static Script Parameterize(this Script script,
            out IReadOnlyDictionary<string, object> parameters)
        {
            parameters = LiteralParameterizer.Parameterize(script.Statements);
            return script;
        }

        // #####################################################################
        // ########################### Collectors #############################
        // #####################################################################

        /// <summary>
        /// Collects all table references and qualified joins found across all statements in this script.
        /// </summary>
        /// <param name="script">The script to inspect.</param>
        /// <returns>A <see cref="TableReferences"/> containing the merged tables and joins.</returns>
        public static TableReferences CollectTableReferences(this Script script)
        {
            List<TableReference> allTables = new List<TableReference>();
            List<QualifiedJoin> allJoins = new List<QualifiedJoin>();

            foreach (Stmt stmt in script.Statements)
            {
                TableReferences refs = TableReferenceCollector.Collect(stmt);
                allTables.AddRange(refs.Tables);
                allJoins.AddRange(refs.Joins);
            }

            return new TableReferences(allTables, allJoins);
        }

        /// <summary>
        /// Collects column references found across all statements in this script.
        /// </summary>
        /// <param name="script">The script to inspect.</param>
        /// <param name="scope">Which query levels to traverse.</param>
        /// <param name="clauses">Which SQL clauses to collect from.</param>
        /// <returns>The collected column identifiers from all statements.</returns>
        public static IReadOnlyList<Expr.ColumnIdentifier> CollectColumnReferences(
            this Script script,
            ColumnReferenceScope scope = ColumnReferenceScope.OutermostQuery,
            ColumnReferenceClause clauses = ColumnReferenceClause.Select)
        {
            List<Expr.ColumnIdentifier> allColumns = new List<Expr.ColumnIdentifier>();

            foreach (Stmt stmt in script.Statements)
            {
                IReadOnlyList<Expr.ColumnIdentifier> stmtColumns =
                    ColumnReferenceCollector.Collect(stmt, scope, clauses);
                allColumns.AddRange(stmtColumns);
            }

            return allColumns;
        }

        // #####################################################################
        // ##################### ReplaceWithTempTables ########################
        // #####################################################################

        /// <summary>
        /// Replaces matching table references with temp tables in each statement,
        /// then flattens all resulting Scripts into one combined Script.
        /// </summary>
        /// <param name="script">The script to modify.</param>
        /// <param name="tableNames">Names of tables or CTEs to materialize as temp tables.</param>
        /// <returns>A combined <see cref="Script"/> containing all SELECT INTO statements and modified queries.</returns>
        public static Script ReplaceWithTempTables(this Script script, params string[] tableNames)
        {
            List<Stmt> allStatements = new List<Stmt>();

            foreach (Stmt stmt in script.Statements)
            {
                Script result = TempTableReplacer.Replace(stmt, tableNames);
                allStatements.AddRange(result.Statements);
            }

            return new Script(allStatements);
        }
    }
}
