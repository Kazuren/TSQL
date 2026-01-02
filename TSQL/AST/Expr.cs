using System;
using System.Text;

namespace TSQL
{
    public abstract partial class Expr : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor) where T : Expr;

        public interface Visitor<T> where T : Expr
        {
            T VisitBinaryExpr(Binary expr);
            T VisitLiteralExpr(Literal expr);
            T VisitColumnIdentifierExpr(ColumnIdentifier expr);
            T VisitUnaryExpr(Unary expr);
            T VisitGroupingExpr(Grouping expr);
            T VisitSubquery(Subquery expr);
        }


        public abstract class SqlIdentifier : Expr { }
        public class ObjectIdentifier : SqlIdentifier
        {
            public string ServerName { get; }
            public string DatabaseName { get; }
            public string SchemaName { get; }
            public string ObjectName { get; }

            public ObjectIdentifier(string serverName, string databaseName, string schemaName, string objectName)
            {
                ServerName = serverName;
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
            }

            public ObjectIdentifier(string databaseName, string schemaName, string objectName)
            {
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
            }

            public ObjectIdentifier(string schemaName, string objectName)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
            }

            public ObjectIdentifier(string objectName)
            {
                ObjectName = objectName;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                throw new System.NotImplementedException();
            }

            public override string ToSource()
            {
                throw new System.NotImplementedException();
            }
        }

        public class ColumnIdentifier : SqlIdentifier
        {
            public Nullable<DatabaseName> DatabaseName { get; }
            public Nullable<SchemaName> SchemaName { get; }
            public Nullable<ObjectName> ObjectName { get; }
            public ColumnName ColumnName { get; }

            #region Constructors
            public ColumnIdentifier(DatabaseName databaseName, SchemaName schemaName, ObjectName objectName, ColumnName columnName)
            {
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
                ColumnName = columnName;
            }

            public ColumnIdentifier(DatabaseName databaseName, ObjectName objectName, ColumnName columnName)
            {
                DatabaseName = databaseName;
                ObjectName = objectName;
                ColumnName = columnName;
            }

            public ColumnIdentifier(SchemaName schemaName, ObjectName objectName, ColumnName columnName)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
                ColumnName = columnName;
            }

            public ColumnIdentifier(ObjectName objectName, ColumnName columnName)
            {
                ObjectName = objectName;
                ColumnName = columnName;
            }

            public ColumnIdentifier(ColumnName columnName)
            {
                ColumnName = columnName;
            }
            #endregion

            public override T Accept<T>(Visitor<T> visitor)
            {
                throw new System.NotImplementedException();
            }

            public override string ToSource()
            {
                throw new System.NotImplementedException();
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

            public override string ToSource()
            {
                return $"{Left.ToSource()}{Operator.ToSource()}{Right.ToSource()}";
            }
        }

        public class Literal : Expr
        {
            public object Value { get => _token.Literal; }

            private readonly Token _token;
            public Literal(object value)
            {
                if (value is string)
                {
                    _token = new Token(TokenType.STRING, "", value, 0);
                }
                else if (value is int)
                {
                    _token = new Token(TokenType.WHOLE_NUMBER, "", value, 0);
                }
                else if (value is double || value is decimal || value is float)
                {
                    _token = new Token(TokenType.DECIMAL, "", value, 0);
                }

                throw new ArgumentException($"Expected literal to be either a string, int, double, float or decimal but got: {value.GetType().FullName}", nameof(value));
            }
            internal Literal(Token token)
            {
                _token = token;
            }

            public override T Accept<T>(Expr.Visitor<T> visitor)
            {
                return visitor.VisitLiteralExpr(this);
            }

            public override string ToSource()
            {
                if (_token != null)
                {
                    return _token.ToSource();
                }
                else
                {
                    if (_token.Type == TokenType.STRING)
                    {
                        return $"'{_token.Literal}'";
                    }
                    else
                    {
                        return _token.Literal.ToString();
                    }
                }
            }
        }

        public class Unary : Expr
        {

            public Token Operator { get; }
            public Expr Right { get; }

            public Unary(Token @operator, Expr right)
            {
                Operator = @operator;
                Right = right;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitUnaryExpr(this);
            }

            public override string ToSource()
            {
                return $"{Operator.ToSource()}{Right.ToSource()}";
            }
        }

        public class Grouping : Expr
        {
            public Expr Expression { get; }

            internal Token _leftParen;
            internal Token _rightParen;

            public Grouping(Expr expression)
            {
                Expression = expression;
            }

            internal Grouping(Expr expression, Token leftParen, Token rightParen)
            {
                Expression = expression;
                _leftParen = leftParen;
                _rightParen = rightParen;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitGroupingExpr(this);
            }

            public override string ToSource()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(_leftParen?.ToSource() ?? "(");
                sb.Append(Expression.ToSource());
                sb.Append(_rightParen?.ToSource() ?? ")");

                return sb.ToString();
            }
        }

        public class Subquery : Expr
        {
            public SelectExpression SelectExpression { get; }
            internal Token _leftParen;
            internal Token _rightParen;

            public Subquery(SelectExpression selectExpression, Token leftParen, Token rightParen)
            {
                SelectExpression = selectExpression;
                _leftParen = leftParen;
                _rightParen = rightParen;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitSubquery(this);
            }

            public override string ToSource()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(_leftParen?.ToSource() ?? "(");
                sb.Append(SelectExpression.ToSource());
                sb.Append(_rightParen?.ToSource() ?? ")");

                return sb.ToString();
            }
        }
    }

    public class SelectExpression : SyntaxElement
    {
        public bool Distinct { get; set; }
        public TopClause Top { get; set; }
        public SyntaxElementList<SelectColumn> Columns { get; set; } = new SyntaxElementList<SelectColumn>();
        public FromClause From { get; set; }
        public SyntaxElementList<JoinClause> Joins { get; set; } = new SyntaxElementList<JoinClause>();
        public Expr Where { get; set; }
        public SyntaxElementList<Expr> GroupBy { get; set; } = new SyntaxElementList<Expr>();
        public Expr Having { get; set; }
        public SyntaxElementList<OrderByItem> OrderBy { get; set; } = new SyntaxElementList<OrderByItem>();

        // Original tokens
        internal Token _selectKeyword;
        internal Token _distinctKeyword;

        public override string ToSource()
        {
            StringBuilder sb = new StringBuilder();

            if (_selectKeyword != null)
            {
                sb.Append(_selectKeyword.ToSource());
            }
            else
            {
                sb.Append("SELECT");
            }

            if (Distinct)
                sb.Append(" DISTINCT");

            if (Top != null)
                sb.Append(Top.ToSource());

            sb.Append(" ");
            sb.Append(Columns.ToSource());

            if (From != null)
            {
                sb.Append(" FROM ");
                sb.Append(From.ToSource());
            }

            foreach (var join in Joins)
            {
                sb.Append(" ");
                sb.Append(join.ToSource());
            }

            if (Where != null)
            {
                sb.Append(" WHERE ");
                sb.Append(Where.ToSource());
            }

            if (GroupBy?.Count > 0)
            {
                sb.Append(" GROUP BY ");
                sb.Append(GroupBy.ToSource()); // Handles commas!
            }

            if (Having != null)
            {
                sb.Append(" HAVING ");
                sb.Append(Having.ToSource());
            }

            if (OrderBy?.Count > 0)
            {
                sb.Append(" ORDER BY ");
            }

            return sb.ToString();
        }
    }

    public readonly struct DatabaseName
    {
        public string Name { get; }
        public DatabaseName(string name)
        {
            Name = name;
        }
    }
    public readonly struct SchemaName
    {
        public string Name { get; }
        public SchemaName(string name)
        {
            Name = name;
        }
    }
    public readonly struct ObjectName
    {
        public string Name { get; }
        public ObjectName(string name)
        {
            Name = name;
        }
    }
    public readonly struct ColumnName
    {
        public string Name { get; }
        public ColumnName(string name)
        {
            Name = name;
        }
    }
}
