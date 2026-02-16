using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal class VariableNameCollector : SqlWalker
    {
        public HashSet<string> Names { get; } = new HashSet<string>();

        protected override void VisitVariable(Expr.Variable expr)
        {
            Names.Add(expr.Name.ToUpperInvariant());
        }
    }
}
