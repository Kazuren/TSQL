using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// Collects all table references and qualified joins found in a SQL statement.
    /// Useful for discovering which tables a query touches and what joins it uses.
    /// </summary>
    public class TableReferenceCollector : SqlWalker
    {
        private readonly List<TableReference> _tables = new List<TableReference>();
        private readonly List<QualifiedJoin> _joins = new List<QualifiedJoin>();

        public IReadOnlyList<TableReference> Tables => _tables;
        public IReadOnlyList<QualifiedJoin> Joins => _joins;

        public static TableReferenceCollector Collect(Stmt stmt)
        {
            var collector = new TableReferenceCollector();
            collector.Walk(stmt);
            return collector;
        }

        protected override void VisitTableReference(TableReference source)
        {
            _tables.Add(source);
        }

        protected override void VisitQualifiedJoin(QualifiedJoin source)
        {
            _joins.Add(source);
            base.VisitQualifiedJoin(source);
        }
    }
}
