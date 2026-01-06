namespace TSQL.StandardLibrary.Visitors
{
    public class SourceCodeVisitor : Expr.Visitor<string>, Stmt.Visitor<string>
    {
        public string VisitSelectStmt(Stmt.Select stmt)
        {
            return stmt.ToSource();
        }

        public string VisitBinaryExpr(Expr.Binary expr)
        {
            return expr.ToSource();
        }

        public string VisitColumnIdentifierExpr(Expr.ColumnIdentifier expr)
        {
            return expr.ToSource();
        }

        public string VisitFunctionCallExpr(Expr.FunctionCall expr)
        {
            return expr.ToSource();
        }

        public string VisitGroupingExpr(Expr.Grouping expr)
        {
            return expr.ToSource();
        }

        public string VisitLiteralExpr(Expr.Literal expr)
        {
            return expr.ToSource();
        }

        public string VisitSubqueryExpr(Expr.Subquery expr)
        {
            return expr.ToSource();
        }

        public string VisitUnaryExpr(Expr.Unary expr)
        {
            return expr.ToSource();
        }
    }
}
