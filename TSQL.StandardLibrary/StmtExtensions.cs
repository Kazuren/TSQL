namespace TSQL.StandardLibrary.Visitors
{
    public static class StmtExtensions
    {
        /// <summary>
        /// Appends a WHERE condition to SELECT statements within this statement.
        /// Use <see cref="WhereClauseTarget"/> flags to control which queries are modified.
        /// Defaults to outermost query only (both sides of UNION, etc.).
        /// </summary>
        public static void AddCondition(this Stmt stmt, string condition, WhereClauseTarget target = WhereClauseTarget.OutermostQuery)
        {
            WhereClauseAppender.AddCondition(stmt, condition, target);
        }

        /// <summary>
        /// Parameterizes all non-NULL literals in this statement, replacing them with
        /// @P0, @P1, etc. Returns the parameterized SQL and a dictionary of parameter values.
        /// </summary>
        public static ParameterizedQuery Parameterize(this Stmt stmt)
        {
            return LiteralParameterizer.Parameterize(stmt);
        }

        /// <summary>
        /// Collects all table references and qualified joins found in this statement.
        /// </summary>
        public static TableReferences CollectTableReferences(this Stmt stmt)
        {
            return TableReferenceCollector.Collect(stmt);
        }
    }
}
