using System.Collections.Generic;

namespace TSQL
{
    public abstract class Stmt
    {
        public abstract T Accept<T>(Visitor<T> visitor);

        private readonly List<Token> _tokens = new List<Token>();

        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
            T VisitCteStmt(Stmt.Cte stmt);
        }

        public class Select : Stmt
        {
            public bool Distinct { get; set; }
            public int? Top { get; set; }
            public List<SelectColumn> Columns { get; set; } = new List<SelectColumn>();
            public FromClause From { get; set; }
            public List<JoinClause> Joins { get; set; } = new List<JoinClause>();
            public Expr Where { get; set; }
            public List<Expr> GroupBy { get; set; }
            public Expr Having { get; set; }
            public List<OrderByItem> OrderBy { get; set; }

            public override T Accept<T>(Stmt.Visitor<T> visitor)
            {
                return visitor.VisitSelectStmt(this);
            }
        }

        public class Cte : Stmt
        {
            public List<CteDefinition> Ctes { get; set; } = new List<CteDefinition>();
            public Stmt.Select MainQuery { get; set; }

            public override T Accept<T>(Stmt.Visitor<T> visitor)
            {
                return visitor.VisitCteStmt(this);
            }
        }
    }

    public class CteDefinition
    {
        public string Name { get; set; }
        public List<string> ColumnNames { get; set; }
        public Stmt.Select Query { get; set; }
    }

    public class SelectColumn
    {
        public Expr Expression { get; set; }
        public string Alias { get; set; }
    }

    public class FromClause
    {
        public string TableName { get; set; }
        public string Alias { get; set; }
        public Stmt.Select Subquery { get; set; }
    }

    public class JoinClause
    {
        public string JoinType { get; set; } // INNER, LEFT, RIGHT, FULL, CROSS
        public string TableName { get; set; }
        public string Alias { get; set; }
        public Stmt.Select Subquery { get; set; }
        public Expr OnCondition { get; set; }
    }


    public class OrderByItem
    {
        public Expr Expression { get; set; }
        public bool Descending { get; set; }
    }
}
