using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    public class TableReferences
    {
        public IReadOnlyList<TableReference> Tables { get; }
        public IReadOnlyList<QualifiedJoin> Joins { get; }

        internal TableReferences(IReadOnlyList<TableReference> tables, IReadOnlyList<QualifiedJoin> joins)
        {
            Tables = tables;
            Joins = joins;
        }
    }

    internal class TableReferenceCollector : SqlWalker
    {
        private readonly List<TableReference> _tables = new List<TableReference>();
        private readonly List<QualifiedJoin> _joins = new List<QualifiedJoin>();

        internal static TableReferences Collect(Stmt stmt)
        {
            var collector = new TableReferenceCollector();
            collector.Walk(stmt);
            return new TableReferences(collector._tables, collector._joins);
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
