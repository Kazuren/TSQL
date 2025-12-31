using System.Collections.Generic;

namespace TSQL
{
    public abstract class Expr
    {
        public abstract T Accept<T>(Visitor<T> visitor);

        public interface Visitor<T>
        {
            T VisitColumnExpr(Column expr);
            T VisitBinaryExpr(Binary expr);
            T VisitLiteralExpr(Literal expr);
            T VisitSubqueryExpr(Subquery expr);
            T VisitFunctionExpr(Function expr);
        }

        public class Column : Expr
        {
            public string TableAlias { get; set; }
            public string ColumnName { get; set; }

            public override T Accept<T>(Expr.Visitor<T> visitor)
            {
                return visitor.VisitColumnExpr(this);
            }
        }

        public class Binary : Expr
        {
            public Expr Left { get; set; }
            public Token Operator { get; set; }
            public Expr Right { get; set; }

            public override T Accept<T>(Expr.Visitor<T> visitor)
            {
                return visitor.VisitBinaryExpr(this);
            }
        }
        public class Literal : Expr
        {
            public object Value { get; set; }

            public override T Accept<T>(Expr.Visitor<T> visitor)
            {
                return visitor.VisitLiteralExpr(this);
            }
        }

        public class Subquery : Expr
        {
            public Stmt.Select Query { get; set; }

            public override T Accept<T>(Expr.Visitor<T> visitor)
            {
                return visitor.VisitSubqueryExpr(this);
            }
        }

        public class Function : Expr
        {
            public string Name { get; set; }
            public List<Expr> Arguments { get; set; } = new List<Expr>();

            public override T Accept<T>(Expr.Visitor<T> visitor)
            {
                return visitor.VisitFunctionExpr(this);
            }
        }
    }
}
