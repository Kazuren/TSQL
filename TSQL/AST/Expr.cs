using System;
using System.Collections.Generic;

namespace TSQL
{
    public abstract partial class Expr : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor);

        public interface Visitor<T>
        {
            T VisitBinaryExpr(Binary expr);
            T VisitLiteralExpr(Literal expr);
            T VisitColumnIdentifierExpr(ColumnIdentifier expr);
            T VisitUnaryExpr(Unary expr);
            T VisitGroupingExpr(Grouping expr);
            T VisitSubqueryExpr(Subquery expr);
            T VisitFunctionCallExpr(FunctionCall expr);
            T VisitVariableExpr(Variable expr);
        }


        public abstract class SqlIdentifier : Expr { }
        public class ObjectIdentifier : SqlIdentifier
        {
            public ServerName ServerName { get; }
            public DatabaseName DatabaseName { get; }
            public SchemaName SchemaName { get; }
            public ObjectName ObjectName { get; }

            internal Token _serverToDatabaseDot;
            internal Token _databaseToSchemaDot;
            internal Token _schemaToObjectDot;

            public ObjectIdentifier(ServerName serverName, DatabaseName databaseName, SchemaName schemaName, ObjectName objectName)
            {
                ServerName = serverName;
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
            }

            public ObjectIdentifier(DatabaseName databaseName, SchemaName schemaName, ObjectName objectName)
            {
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
            }

            public ObjectIdentifier(SchemaName schemaName, ObjectName objectName)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
            }

            public ObjectIdentifier(ObjectName objectName)
            {
                ObjectName = objectName;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                throw new System.NotImplementedException();
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (ServerName != null)
                {
                    foreach (Token token in ServerName.DescendantTokens())
                    {
                        yield return token;
                    }
                }

                if (ServerName != null)
                {
                    yield return _serverToDatabaseDot;
                }

                if (DatabaseName != null)
                {
                    foreach (Token token in DatabaseName.DescendantTokens())
                    {
                        yield return token;
                    }
                }

                if (ServerName != null || DatabaseName != null)
                {
                    yield return _databaseToSchemaDot;
                }

                if (SchemaName != null)
                {
                    foreach (Token token in SchemaName.DescendantTokens())
                    {
                        yield return token;
                    }
                }

                if (ServerName != null || DatabaseName != null || SchemaName != null)
                {
                    yield return _schemaToObjectDot;
                }

                foreach (Token token in ObjectName.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
        public class Wildcard : SqlIdentifier, SelectItem
        {
            public Token WildcardToken { get; }

            public Wildcard(Token wildcardToken)
            {
                WildcardToken = wildcardToken;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return WildcardToken;
            }
        }

        public class QualifiedWildcard : SqlIdentifier, SelectItem
        {
            public DatabaseName DatabaseName { get; }
            public SchemaName SchemaName { get; }
            public ObjectName ObjectName { get; }
            public Token WildcardToken { get; }

            internal Token _databaseToSchemaDot;
            internal Token _schemaToObjectDot;
            internal Token _objectToStarDot;

            #region Constructors
            public QualifiedWildcard(DatabaseName databaseName, SchemaName schemaName, ObjectName objectName, Token wildcardToken)
            {
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
                WildcardToken = wildcardToken;
            }

            public QualifiedWildcard(DatabaseName databaseName, ObjectName objectName, Token wildcardToken)
            {
                DatabaseName = databaseName;
                ObjectName = objectName;
                WildcardToken = wildcardToken;
            }

            public QualifiedWildcard(SchemaName schemaName, ObjectName objectName, Token wildcardToken)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
                WildcardToken = wildcardToken;
            }

            public QualifiedWildcard(ObjectName objectName, Token wildcardToken)
            {
                ObjectName = objectName;
                WildcardToken = wildcardToken;
            }
            #endregion


            public override T Accept<T>(Visitor<T> visitor)
            {
                throw new System.NotImplementedException();
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (DatabaseName != null && SchemaName != null && ObjectName != null)
                {
                    foreach (Token token in DatabaseName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _databaseToSchemaDot;

                    foreach (Token token in SchemaName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _schemaToObjectDot;

                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToStarDot;

                    yield return WildcardToken;
                }
                else if (SchemaName != null && ObjectName != null)
                {
                    foreach (Token token in SchemaName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _schemaToObjectDot;

                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToStarDot;

                    yield return WildcardToken;
                }
                else if (DatabaseName != null && SchemaName == null && ObjectName != null)
                {
                    foreach (Token token in DatabaseName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _databaseToSchemaDot;
                    yield return _schemaToObjectDot;

                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToStarDot;

                    yield return WildcardToken;
                }
                else if (ObjectName != null)
                {
                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToStarDot;

                    yield return WildcardToken;
                }
            }
        }

        public class ColumnIdentifier : SqlIdentifier
        {
            public DatabaseName DatabaseName { get; }
            public SchemaName SchemaName { get; }
            public ObjectName ObjectName { get; }
            public ColumnName ColumnName { get; }

            internal Token _databaseToSchemaDot;
            internal Token _schemaToObjectDot;
            internal Token _objectToColumnDot;

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

            public override IEnumerable<Token> DescendantTokens()
            {
                if (DatabaseName != null && SchemaName != null && ObjectName != null)
                {
                    foreach (Token token in DatabaseName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _databaseToSchemaDot;

                    foreach (Token token in SchemaName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _schemaToObjectDot;

                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToColumnDot;

                    foreach (Token token in ColumnName.DescendantTokens())
                    {
                        yield return token;
                    }
                }
                else if (SchemaName != null && ObjectName != null)
                {
                    foreach (Token token in SchemaName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _schemaToObjectDot;

                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToColumnDot;

                    foreach (Token token in ColumnName.DescendantTokens())
                    {
                        yield return token;
                    }
                }
                else if (DatabaseName != null && SchemaName == null && ObjectName != null)
                {
                    foreach (Token token in DatabaseName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _databaseToSchemaDot;
                    yield return _schemaToObjectDot;

                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToColumnDot;

                    foreach (Token token in ColumnName.DescendantTokens())
                    {
                        yield return token;
                    }
                }
                else if (ObjectName != null)
                {
                    foreach (Token token in ObjectName.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _objectToColumnDot;

                    foreach (Token token in ColumnName.DescendantTokens())
                    {
                        yield return token;
                    }
                }
                else
                {
                    foreach (Token token in ColumnName.DescendantTokens())
                    {
                        yield return token;
                    }
                }
            }
        }

        public class FunctionCall : Expr
        {
            public ObjectIdentifier Callee { get; set; }
            public SyntaxElementList<Expr> Arguments { get; set; }

            internal Token _leftParen;
            internal Token _rightParen;

            public FunctionCall(ObjectIdentifier callee, SyntaxElementList<Expr> args)
            {
                Callee = callee;
                Arguments = args;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitFunctionCallExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Callee.DescendantTokens())
                {
                    yield return token;
                }

                yield return _leftParen;

                foreach (Token token in Arguments.DescendantTokens())
                {
                    yield return token;
                }

                yield return _rightParen;
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
            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                {
                    yield return token;
                }

                yield return Operator;

                foreach (Token token in Right.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        public class Variable : Expr
        {
            public string Name { get => _token.Lexeme; }
            private readonly Token _token;

            internal Variable(Token token)
            {
                _token = token;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitVariableExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _token;
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
                    _token = new ConcreteToken(TokenType.STRING, "", value);
                }
                else if (value is int)
                {
                    _token = new ConcreteToken(TokenType.WHOLE_NUMBER, "", value);
                }
                else if (value is double || value is decimal || value is float)
                {
                    _token = new ConcreteToken(TokenType.DECIMAL, "", value);
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
            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _token;
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
            public override IEnumerable<Token> DescendantTokens()
            {
                yield return Operator;
                foreach (Token token in Right.DescendantTokens())
                {
                    yield return token;
                }
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

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _leftParen;

                foreach (Token token in Expression.DescendantTokens())
                {
                    yield return token;
                }

                yield return _rightParen;
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
                return visitor.VisitSubqueryExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _leftParen;

                foreach (Token token in SelectExpression.DescendantTokens())
                {
                    yield return token;
                }

                yield return _rightParen;
            }
        }
    }

    public class SelectExpression : SyntaxElement
    {
        public bool Distinct { get; set; }
        public TopClause Top { get; set; }
        public SyntaxElementList<SelectItem> Columns { get; set; } = new SyntaxElementList<SelectItem>();
        public FromClause From { get; set; }
        public SyntaxElementList<JoinClause> Joins { get; set; } = new SyntaxElementList<JoinClause>();
        public Expr Where { get; set; }
        public SyntaxElementList<Expr> GroupBy { get; set; } = new SyntaxElementList<Expr>();
        public Expr Having { get; set; }
        public SyntaxElementList<OrderByItem> OrderBy { get; set; } = new SyntaxElementList<OrderByItem>();

        // Original tokens
        internal Token _selectKeyword;
        internal Token _distinctKeyword;
        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _selectKeyword;

            if (_distinctKeyword != null)
            {
                yield return _distinctKeyword;
            }

            if (Top != null)
            {
                foreach (Token token in Top.DescendantTokens())
                {
                    yield return token;
                }
            }

            foreach (Token token in Columns.DescendantTokens())
            {
                yield return token;
            }

            if (From != null)
            {
                foreach (Token token in From.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }

    public class ServerName : SyntaxElement
    {
        public string Name { get; }
        private readonly Token _token;

        public ServerName(string name)
        {
            Name = name;
        }

        internal ServerName(Token token)
        {
            Name = token.Literal.ToString();
            _token = token;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }
    }
    public class DatabaseName : SyntaxElement
    {
        public string Name { get; }
        private readonly Token _token;

        public DatabaseName(string name)
        {
            Name = name;
        }

        internal DatabaseName(Token token)
        {
            Name = token.Literal.ToString();
            _token = token;
        }
        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }
    }
    public class SchemaName : SyntaxElement
    {
        public string Name { get; }
        private readonly Token _token;

        public SchemaName(string name)
        {
            Name = name;
        }

        internal SchemaName(Token token)
        {
            Name = token.Literal.ToString();
            _token = token;
        }
        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }
    }
    public class ObjectName : SyntaxElement
    {
        public string Name { get; }
        private readonly Token _token;

        public ObjectName(string name)
        {
            Name = name;
        }

        internal ObjectName(Token token)
        {
            Name = token.Literal.ToString();
            _token = token;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }
    }
    public class ColumnName : SyntaxElement
    {
        public string Name { get; }
        private readonly Token _token;

        public ColumnName(string name)
        {
            Name = name;
        }

        internal ColumnName(Token token)
        {
            if (token.Type == TokenType.IDENTIFIER)
            {
                // Use Lexeme instead of Literal - the identifier text is the lexeme
                Name = token.Lexeme;
            }
            else if (token.Type == TokenType.STAR)
            {
                Name = token.Lexeme;
            }

            _token = token;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }
    }
}
