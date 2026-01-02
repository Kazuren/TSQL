using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    public abstract class Stmt : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor) where T : Stmt;

        public interface Visitor<T> where T : Stmt
        {
            T VisitSelectStmt(Stmt.Select stmt);
            T VisitCteStmt(Stmt.Cte stmt);
        }

        public class Select : Stmt
        {

            public SelectExpression SelectExpression;

            public Select(SelectExpression selectExpression)
            {
                SelectExpression = selectExpression;
            }

            public override T Accept<T>(Stmt.Visitor<T> visitor)
            {
                return visitor.VisitSelectStmt(this);
            }

            public override string ToSource()
            {
                return SelectExpression.ToSource();
            }
        }

        public class Cte : Stmt
        {
            public List<CteDefinition> Ctes { get; set; } = new List<CteDefinition>();
            public SelectExpression MainQuery { get; set; }

            public override T Accept<T>(Stmt.Visitor<T> visitor)
            {
                return visitor.VisitCteStmt(this);
            }

            public override string ToSource()
            {
                throw new System.NotImplementedException();
            }
        }
    }

    public class CteDefinition : SyntaxElement
    {
        public string Name { get; set; }
        public List<string> ColumnNames { get; set; }
        public Stmt.Select Query { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }

    public class SelectColumn : SyntaxElement
    {
        public Expr Expression { get; set; }
        public string Alias { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }

    public class TopClause : SyntaxElement
    {
        public Expr Expression { get; }

        public TopClause(Expr expr)
        {
            Expression = expr;
        }

        internal Token _topKeyword;

        public override string ToSource()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(_topKeyword?.ToSource() ?? "TOP");
            sb.Append(Expression.ToSource());

            return sb.ToString();
        }
    }

    public class FromClause : SyntaxElement
    {
        public TableSource TableSource { get; set; }
        public string Alias { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }

    public class JoinClause : SyntaxElement
    {
        public string JoinType { get; set; } // INNER, LEFT, RIGHT, FULL, CROSS
        public TableSource TableSource { get; set; }
        public string Alias { get; set; }
        public Expr OnCondition { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }


    public class OrderByItem : SyntaxElement
    {
        public Expr Expression { get; set; }
        public bool Descending { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }

    public abstract class TableSource : SyntaxElement
    {
        public string Alias { get; set; }
    }

    public class TableReference : TableSource
    {
        public string TableName { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }

    public class SubqueryReference : TableSource
    {
        public Expr.Subquery Subquery { get; set; }

        public override string ToSource()
        {
            throw new System.NotImplementedException();
        }
    }

}
