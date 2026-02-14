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
            T VisitObjectIdentifierExpr(ObjectIdentifier expr);
            T VisitWildcardExpr(Wildcard expr);
            T VisitQualifiedWildcardExpr(QualifiedWildcard expr);
            T VisitUnaryExpr(Unary expr);
            T VisitGroupingExpr(Grouping expr);
            T VisitSubqueryExpr(Subquery expr);
            T VisitFunctionCallExpr(FunctionCall expr);
            T VisitVariableExpr(Variable expr);
            T VisitWindowFunctionExpr(WindowFunction expr);
            T VisitSimpleCaseExpr(SimpleCase expr);
            T VisitSearchedCaseExpr(SearchedCase expr);
            T VisitCastExpr(CastExpression expr);
            T VisitConvertExpr(ConvertExpression expr);
            T VisitCollateExpr(Collate expr);
            T VisitIifExpr(Iif expr);
            T VisitAtTimeZoneExpr(AtTimeZone expr);
            T VisitOpenXmlExpr(OpenXmlExpression expr);
        }


        public abstract class SqlIdentifier : Expr
        {
            /// <summary>
            /// Yields tokens for the qualified prefix: [database.]schema.]object.
            /// Handles all combinations of present/missing parts, including skipped schema (database..object).
            /// </summary>
            protected static IEnumerable<Token> YieldQualifiedPrefix(
                DatabaseName database, Token dbToSchemaDot,
                SchemaName schema, Token schemaToObjDot,
                ObjectName obj, Token objToLeafDot)
            {
                if (database != null && schema != null && obj != null)
                {
                    foreach (Token t in database.DescendantTokens()) yield return t;
                    yield return dbToSchemaDot;
                    foreach (Token t in schema.DescendantTokens()) yield return t;
                    yield return schemaToObjDot;
                    foreach (Token t in obj.DescendantTokens()) yield return t;
                    yield return objToLeafDot;
                }
                else if (schema != null && obj != null)
                {
                    foreach (Token t in schema.DescendantTokens()) yield return t;
                    yield return schemaToObjDot;
                    foreach (Token t in obj.DescendantTokens()) yield return t;
                    yield return objToLeafDot;
                }
                else if (database != null && schema == null && obj != null)
                {
                    foreach (Token t in database.DescendantTokens()) yield return t;
                    yield return dbToSchemaDot;
                    yield return schemaToObjDot;
                    foreach (Token t in obj.DescendantTokens()) yield return t;
                    yield return objToLeafDot;
                }
                else if (obj != null)
                {
                    foreach (Token t in obj.DescendantTokens()) yield return t;
                    yield return objToLeafDot;
                }
            }
        }
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
                return visitor.VisitObjectIdentifierExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (ServerName != null)
                {
                    foreach (Token token in ServerName.DescendantTokens())
                        yield return token;
                    yield return _serverToDatabaseDot;
                }

                if (DatabaseName != null)
                {
                    foreach (Token token in DatabaseName.DescendantTokens())
                        yield return token;
                }

                if (ServerName != null || DatabaseName != null)
                    yield return _databaseToSchemaDot;

                if (SchemaName != null)
                {
                    foreach (Token token in SchemaName.DescendantTokens())
                        yield return token;
                }

                if (ServerName != null || DatabaseName != null || SchemaName != null)
                    yield return _schemaToObjectDot;

                foreach (Token token in ObjectName.DescendantTokens())
                    yield return token;
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
                return visitor.VisitWildcardExpr(this);
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

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitQualifiedWildcardExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in YieldQualifiedPrefix(DatabaseName, _databaseToSchemaDot, SchemaName, _schemaToObjectDot, ObjectName, _objectToStarDot))
                    yield return token;
                yield return WildcardToken;
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

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitColumnIdentifierExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (ObjectName != null)
                {
                    foreach (Token token in YieldQualifiedPrefix(DatabaseName, _databaseToSchemaDot, SchemaName, _schemaToObjectDot, ObjectName, _objectToColumnDot))
                        yield return token;
                }
                foreach (Token token in ColumnName.DescendantTokens())
                    yield return token;
            }
        }

        public class WithinGroupClause : SyntaxElement
        {
            public SyntaxElementList<OrderByItem> OrderBy { get; }

            internal Token _withinKeyword;
            internal Token _groupKeyword;
            internal Token _leftParen;
            internal Token _orderKeyword;
            internal Token _byKeyword;
            internal Token _rightParen;

            public WithinGroupClause(SyntaxElementList<OrderByItem> orderBy)
            {
                OrderBy = orderBy;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _withinKeyword;
                yield return _groupKeyword;
                yield return _leftParen;
                yield return _orderKeyword;
                yield return _byKeyword;
                foreach (Token token in OrderBy.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        public class FunctionCall : Expr
        {
            private ObjectIdentifier _callee;
            public ObjectIdentifier Callee
            {
                get => _callee;
                set => SetWithTrivia(ref _callee, value);
            }
            public SyntaxElementList<Expr> Arguments { get; set; }
            public WithinGroupClause WithinGroup { get; set; }

            internal Token _leftParen;
            internal Token _rightParen;

            public FunctionCall(ObjectIdentifier callee, SyntaxElementList<Expr> args)
            {
                _callee = callee;
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

                if (WithinGroup != null)
                {
                    foreach (Token token in WithinGroup.DescendantTokens())
                        yield return token;
                }
            }
        }

        public class Binary : Expr
        {
            private Expr _left;
            public Expr Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            public Token Operator { get; set; }
            private Expr _right;
            public Expr Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            public override T Accept<T>(Visitor<T> visitor)
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

            public Variable(string name)
            {
                _token = new ConcreteToken(TokenType.VARIABLE, name, null);
                _token.AddLeadingTrivia(new Whitespace(" "));
            }

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
            internal TokenType TokenType { get => _token.Type; }

            private readonly Token _token;
            public Literal(object value)
            {
                if (value == null)
                {
                    _token = new ConcreteToken(TokenType.NULL, "NULL", null);
                }
                else if (value is string s)
                {
                    _token = new ConcreteToken(TokenType.STRING, "'" + s.Replace("'", "''") + "'", value);
                }
                else if (value is int i)
                {
                    _token = new ConcreteToken(TokenType.WHOLE_NUMBER, i.ToString(), value);
                }
                else if (value is double || value is decimal || value is float)
                {
                    _token = new ConcreteToken(TokenType.DECIMAL, value.ToString(), value);
                }
                else
                {
                    throw new ArgumentException($"Expected literal to be null, string, int, double, float or decimal but got: {value.GetType().FullName}", nameof(value));
                }
                _token.AddLeadingTrivia(new Whitespace(" "));
            }
            internal Literal(Token token)
            {
                _token = token;
            }

            public override T Accept<T>(Visitor<T> visitor)
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

            public Token Operator { get; set; }
            private Expr _right;
            public Expr Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            public Unary(Token @operator, Expr right)
            {
                Operator = @operator;
                _right = right;
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

        public class Collate : Expr
        {
            private Expr _expression;
            public Expr Expression
            {
                get => _expression;
                set => SetWithTrivia(ref _expression, value);
            }

            public string CollationName { get => _collationName.Lexeme; }

            internal Token _collateKeyword;
            internal Token _collationName;

            public Collate(Expr expression)
            {
                _expression = expression;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitCollateExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Expression.DescendantTokens())
                    yield return token;
                yield return _collateKeyword;
                yield return _collationName;
            }
        }

        public class Iif : Expr
        {
            private AST.Predicate _condition;
            public AST.Predicate Condition
            {
                get => _condition;
                set => SetWithTrivia(ref _condition, value);
            }
            private Expr _trueValue;
            public Expr TrueValue
            {
                get => _trueValue;
                set => SetWithTrivia(ref _trueValue, value);
            }
            private Expr _falseValue;
            public Expr FalseValue
            {
                get => _falseValue;
                set => SetWithTrivia(ref _falseValue, value);
            }

            internal Token _iifKeyword;
            internal Token _leftParen;
            internal Token _firstComma;
            internal Token _secondComma;
            internal Token _rightParen;

            public Iif(AST.Predicate condition, Expr trueValue, Expr falseValue)
            {
                _condition = condition;
                _trueValue = trueValue;
                _falseValue = falseValue;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitIifExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _iifKeyword;
                yield return _leftParen;
                foreach (Token token in Condition.DescendantTokens())
                    yield return token;
                yield return _firstComma;
                foreach (Token token in TrueValue.DescendantTokens())
                    yield return token;
                yield return _secondComma;
                foreach (Token token in FalseValue.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        public class AtTimeZone : Expr
        {
            private Expr _expression;
            public Expr Expression
            {
                get => _expression;
                set => SetWithTrivia(ref _expression, value);
            }
            private Expr _timeZone;
            public Expr TimeZone
            {
                get => _timeZone;
                set => SetWithTrivia(ref _timeZone, value);
            }

            internal Token _atKeyword;
            internal Token _timeKeyword;
            internal Token _zoneKeyword;

            public AtTimeZone(Expr expression, Expr timeZone)
            {
                _expression = expression;
                _timeZone = timeZone;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitAtTimeZoneExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Expression.DescendantTokens())
                    yield return token;
                yield return _atKeyword;
                yield return _timeKeyword;
                yield return _zoneKeyword;
                foreach (Token token in TimeZone.DescendantTokens())
                    yield return token;
            }
        }

        #region OPENXML

        public class OpenXmlColumnDef : SyntaxElement
        {
            public string Name { get => _name.Lexeme; }
            public DataType DataType { get; }
            public string ColPattern { get => _colPatternToken?.Lexeme; }

            internal Token _name;
            internal Token _colPatternToken;

            public OpenXmlColumnDef(DataType dataType)
            {
                DataType = dataType;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _name;
                foreach (Token token in DataType.DescendantTokens())
                    yield return token;
                if (_colPatternToken != null)
                    yield return _colPatternToken;
            }
        }

        public abstract class OpenXmlWithClause : SyntaxElement
        {
            internal Token _withKeyword;
            internal Token _leftParen;
            internal Token _rightParen;
        }

        public class OpenXmlSchemaDeclaration : OpenXmlWithClause
        {
            public SyntaxElementList<OpenXmlColumnDef> Columns { get; }

            public OpenXmlSchemaDeclaration(SyntaxElementList<OpenXmlColumnDef> columns)
            {
                Columns = columns;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _withKeyword;
                yield return _leftParen;
                foreach (Token token in Columns.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        public class OpenXmlTableName : OpenXmlWithClause
        {
            public ObjectIdentifier TableName { get; }

            public OpenXmlTableName(ObjectIdentifier tableName)
            {
                TableName = tableName;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _withKeyword;
                yield return _leftParen;
                foreach (Token token in TableName.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        public class OpenXmlExpression : FunctionCall
        {
            public OpenXmlWithClause WithClause { get; set; }

            internal OpenXmlExpression(FunctionCall source)
                : base(source.Callee, source.Arguments)
            {
                _leftParen = source._leftParen;
                _rightParen = source._rightParen;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitOpenXmlExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in base.DescendantTokens())
                    yield return token;
                if (WithClause != null)
                {
                    foreach (Token token in WithClause.DescendantTokens())
                        yield return token;
                }
            }
        }

        #endregion

        public class Grouping : Expr
        {
            private Expr _expression;
            public Expr Expression
            {
                get => _expression;
                set => SetWithTrivia(ref _expression, value);
            }

            internal Token _leftParen;
            internal Token _rightParen;

            public Grouping(Expr expression)
            {
                _expression = expression;
            }

            internal Grouping(Expr expression, Token leftParen, Token rightParen)
            {
                _expression = expression;
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
            private QueryExpression _query;
            public QueryExpression Query
            {
                get => _query;
                set => SetWithTrivia(ref _query, value);
            }
            internal Token _leftParen;
            internal Token _rightParen;

            public Subquery(QueryExpression query, Token leftParen, Token rightParen)
            {
                _query = query;
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

                foreach (Token token in Query.DescendantTokens())
                {
                    yield return token;
                }

                yield return _rightParen;
            }
        }

        /// <summary>
        /// Represents a window function: function_call OVER (...)
        /// </summary>
        public class WindowFunction : Expr
        {
            private FunctionCall _function;
            public FunctionCall Function
            {
                get => _function;
                set => SetWithTrivia(ref _function, value);
            }
            private OverClause _over;
            public OverClause Over
            {
                get => _over;
                set => SetWithTrivia(ref _over, value);
            }

            public WindowFunction(FunctionCall function, OverClause over)
            {
                _function = function;
                _over = over;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitWindowFunctionExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Function.DescendantTokens())
                {
                    yield return token;
                }

                foreach (Token token in Over.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        /// <summary>
        /// CASE operand WHEN value THEN result [...] [ELSE default] END
        /// </summary>
        public class SimpleCase : Expr
        {
            private Expr _operand;
            public Expr Operand
            {
                get => _operand;
                set => SetWithTrivia(ref _operand, value);
            }
            public List<SimpleCaseWhen> WhenClauses { get; set; }
            private Expr _elseResult;
            public Expr ElseResult
            {
                get => _elseResult;
                set => SetWithTrivia(ref _elseResult, value);
            }

            internal Token _caseToken;
            internal Token _elseToken;
            internal Token _endToken;

            public SimpleCase(Expr operand, List<SimpleCaseWhen> whenClauses, Expr elseResult)
            {
                _operand = operand;
                WhenClauses = whenClauses;
                _elseResult = elseResult;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitSimpleCaseExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _caseToken;
                foreach (Token token in Operand.DescendantTokens())
                    yield return token;
                foreach (SimpleCaseWhen when in WhenClauses)
                    foreach (Token token in when.DescendantTokens())
                        yield return token;
                if (ElseResult != null)
                {
                    yield return _elseToken;
                    foreach (Token token in ElseResult.DescendantTokens())
                        yield return token;
                }
                yield return _endToken;
            }
        }

        public class SimpleCaseWhen : SyntaxElement
        {
            private Expr _value;
            public Expr Value
            {
                get => _value;
                set => SetWithTrivia(ref _value, value);
            }
            private Expr _result;
            public Expr Result
            {
                get => _result;
                set => SetWithTrivia(ref _result, value);
            }

            internal Token _whenToken;
            internal Token _thenToken;

            public SimpleCaseWhen(Expr value, Expr result)
            {
                _value = value;
                _result = result;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _whenToken;
                foreach (Token token in Value.DescendantTokens())
                    yield return token;
                yield return _thenToken;
                foreach (Token token in Result.DescendantTokens())
                    yield return token;
            }
        }

        /// <summary>
        /// CASE WHEN condition THEN result [...] [ELSE default] END
        /// </summary>
        public class SearchedCase : Expr
        {
            public List<SearchedCaseWhen> WhenClauses { get; set; }
            private Expr _elseResult;
            public Expr ElseResult
            {
                get => _elseResult;
                set => SetWithTrivia(ref _elseResult, value);
            }

            internal Token _caseToken;
            internal Token _elseToken;
            internal Token _endToken;

            public SearchedCase(List<SearchedCaseWhen> whenClauses, Expr elseResult)
            {
                WhenClauses = whenClauses;
                _elseResult = elseResult;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitSearchedCaseExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _caseToken;
                foreach (SearchedCaseWhen when in WhenClauses)
                    foreach (Token token in when.DescendantTokens())
                        yield return token;
                if (ElseResult != null)
                {
                    yield return _elseToken;
                    foreach (Token token in ElseResult.DescendantTokens())
                        yield return token;
                }
                yield return _endToken;
            }
        }

        public class SearchedCaseWhen : SyntaxElement
        {
            private AST.Predicate _condition;
            public AST.Predicate Condition
            {
                get => _condition;
                set => SetWithTrivia(ref _condition, value);
            }
            private Expr _result;
            public Expr Result
            {
                get => _result;
                set => SetWithTrivia(ref _result, value);
            }

            internal Token _whenToken;
            internal Token _thenToken;

            public SearchedCaseWhen(AST.Predicate condition, Expr result)
            {
                _condition = condition;
                _result = result;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _whenToken;
                foreach (Token token in Condition.DescendantTokens())
                    yield return token;
                yield return _thenToken;
                foreach (Token token in Result.DescendantTokens())
                    yield return token;
            }
        }

        /// <summary>
        /// CAST(expr AS type) or TRY_CAST(expr AS type)
        /// </summary>
        public class CastExpression : Expr
        {
            private Expr _expression;
            public Expr Expression
            {
                get => _expression;
                set => SetWithTrivia(ref _expression, value);
            }
            public DataType DataType { get; set; }

            internal Token _castKeyword;
            internal Token _leftParen;
            internal Token _asToken;
            internal Token _rightParen;

            public CastExpression(Expr expression, DataType dataType)
            {
                _expression = expression;
                DataType = dataType;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitCastExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _castKeyword;
                yield return _leftParen;
                foreach (Token token in Expression.DescendantTokens())
                    yield return token;
                yield return _asToken;
                foreach (Token token in DataType.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        /// <summary>
        /// CONVERT(type, expr [, style]) or TRY_CONVERT(type, expr [, style])
        /// </summary>
        public class ConvertExpression : Expr
        {
            public DataType DataType { get; set; }
            private Expr _expression;
            public Expr Expression
            {
                get => _expression;
                set => SetWithTrivia(ref _expression, value);
            }
            private Expr _style;
            public Expr Style
            {
                get => _style;
                set => SetWithTrivia(ref _style, value);
            }

            internal Token _convertKeyword;
            internal Token _leftParen;
            internal Token _commaAfterType;
            internal Token _commaAfterExpr;
            internal Token _rightParen;

            public ConvertExpression(DataType dataType, Expr expression, Expr style)
            {
                DataType = dataType;
                _expression = expression;
                _style = style;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitConvertExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _convertKeyword;
                yield return _leftParen;
                foreach (Token token in DataType.DescendantTokens())
                    yield return token;
                yield return _commaAfterType;
                foreach (Token token in Expression.DescendantTokens())
                    yield return token;
                if (Style != null)
                {
                    yield return _commaAfterExpr;
                    foreach (Token token in Style.DescendantTokens())
                        yield return token;
                }
                yield return _rightParen;
            }
        }
    }

    #region Window Function Support

    /// <summary>
    /// Frame type for window functions: ROWS or RANGE
    /// </summary>
    public enum WindowFrameType
    {
        Rows,
        Range
    }

    /// <summary>
    /// Bound type for window frame specifications
    /// </summary>
    public enum WindowFrameBoundType
    {
        UnboundedPreceding,
        UnboundedFollowing,
        CurrentRow,
        Preceding,   // N PRECEDING
        Following    // N FOLLOWING
    }

    /// <summary>
    /// Represents a single frame boundary (e.g., UNBOUNDED PRECEDING, CURRENT ROW, 3 PRECEDING)
    /// </summary>
    public class WindowFrameBound : SyntaxElement
    {
        public WindowFrameBoundType BoundType { get; }
        public Expr Offset { get; }  // For N PRECEDING/FOLLOWING (null otherwise)

        internal Token _unboundedToken;   // UNBOUNDED (if applicable)
        internal Token _currentToken;     // CURRENT (if applicable)
        internal Token _rowToken;         // ROW in CURRENT ROW (if applicable)
        internal Token _precedingToken;   // PRECEDING (if applicable)
        internal Token _followingToken;   // FOLLOWING (if applicable)

        public WindowFrameBound(WindowFrameBoundType boundType, Expr offset = null)
        {
            BoundType = boundType;
            Offset = offset;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            switch (BoundType)
            {
                case WindowFrameBoundType.UnboundedPreceding:
                    yield return _unboundedToken;
                    yield return _precedingToken;
                    break;
                case WindowFrameBoundType.UnboundedFollowing:
                    yield return _unboundedToken;
                    yield return _followingToken;
                    break;
                case WindowFrameBoundType.CurrentRow:
                    yield return _currentToken;
                    yield return _rowToken;
                    break;
                case WindowFrameBoundType.Preceding:
                    foreach (Token token in Offset.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _precedingToken;
                    break;
                case WindowFrameBoundType.Following:
                    foreach (Token token in Offset.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _followingToken;
                    break;
            }
        }
    }

    /// <summary>
    /// Represents a window frame clause: ROWS/RANGE [BETWEEN bound AND bound | bound]
    /// </summary>
    public class WindowFrame : SyntaxElement
    {
        public WindowFrameType FrameType { get; }
        public WindowFrameBound Start { get; }
        public WindowFrameBound End { get; }  // null if short syntax (not BETWEEN)

        internal Token _rowsOrRangeToken;
        internal Token _betweenToken;  // null if short syntax
        internal Token _andToken;      // null if short syntax

        public WindowFrame(WindowFrameType frameType, WindowFrameBound start, WindowFrameBound end = null)
        {
            FrameType = frameType;
            Start = start;
            End = end;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _rowsOrRangeToken;

            if (_betweenToken != null)
            {
                yield return _betweenToken;
                foreach (Token token in Start.DescendantTokens())
                {
                    yield return token;
                }
                yield return _andToken;
                foreach (Token token in End.DescendantTokens())
                {
                    yield return token;
                }
            }
            else
            {
                foreach (Token token in Start.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }

    /// <summary>
    /// Represents the OVER clause: OVER (PARTITION BY ... ORDER BY ... ROWS/RANGE ...)
    /// </summary>
    public class OverClause : SyntaxElement
    {
        public SyntaxElementList<Expr> PartitionBy { get; set; }
        public SyntaxElementList<OrderByItem> OrderBy { get; set; }
        public WindowFrame Frame { get; set; }

        internal Token _overKeyword;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _partitionKeyword;
        internal Token _partitionByKeyword;
        internal Token _orderKeyword;
        internal Token _orderByKeyword;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _overKeyword;
            yield return _leftParen;

            if (PartitionBy != null && PartitionBy.Count > 0)
            {
                yield return _partitionKeyword;
                yield return _partitionByKeyword;
                foreach (Token token in PartitionBy.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (OrderBy != null && OrderBy.Count > 0)
            {
                yield return _orderKeyword;
                yield return _orderByKeyword;
                foreach (Token token in OrderBy.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (Frame != null)
            {
                foreach (Token token in Frame.DescendantTokens())
                {
                    yield return token;
                }
            }

            yield return _rightParen;
        }
    }

    #endregion

    /// <summary>
    /// Represents a SQL data type: INT, VARCHAR(50), DECIMAL(10, 2), etc.
    /// </summary>
    public class DataType : SyntaxElement
    {
        public Token TypeName { get; }
        public SyntaxElementList<Expr> Parameters { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public DataType(Token typeName, SyntaxElementList<Expr> parameters)
        {
            TypeName = typeName;
            Parameters = parameters;
        }

        public DataType(Token typeName)
        {
            TypeName = typeName;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return TypeName;
            if (Parameters != null)
            {
                yield return _leftParen;
                foreach (Token token in Parameters.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }
    }

    public enum SetOperationType { Union, UnionAll, Intersect, Except }

    public class OrderByClause : SyntaxElement
    {
        public SyntaxElementList<OrderByItem> Items { get; set; } = new SyntaxElementList<OrderByItem>();
        public Expr OffsetCount { get; set; }
        public Expr FetchCount { get; set; }

        internal Token _orderKeyword;
        internal Token _orderByKeyword;
        internal Token _offsetKeyword;
        internal Token _offsetRowOrRows;
        internal Token _fetchKeyword;
        internal Token _firstOrNext;
        internal Token _fetchRowOrRows;
        internal Token _onlyKeyword;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _orderKeyword;
            yield return _orderByKeyword;
            foreach (Token token in Items.DescendantTokens())
                yield return token;

            if (OffsetCount != null)
            {
                yield return _offsetKeyword;
                foreach (Token token in OffsetCount.DescendantTokens())
                    yield return token;
                yield return _offsetRowOrRows;

                if (FetchCount != null)
                {
                    yield return _fetchKeyword;
                    yield return _firstOrNext;
                    foreach (Token token in FetchCount.DescendantTokens())
                        yield return token;
                    yield return _fetchRowOrRows;
                    yield return _onlyKeyword;
                }
            }
        }
    }

    public abstract class QueryExpression : SyntaxElement
    {
        public OrderByClause OrderBy { get; set; }
    }

    public class SelectExpression : QueryExpression
    {
        public bool Distinct { get; set; }
        public TopClause Top { get; set; }
        public SyntaxElementList<SelectItem> Columns { get; set; } = new SyntaxElementList<SelectItem>();
        public FromClause From { get; set; }
        private AST.Predicate _where;
        public AST.Predicate Where
        {
            get => _where;
            set => SetWithTrivia(ref _where, value);
        }
        public GroupByClause GroupBy { get; set; }
        private AST.Predicate _having;
        public AST.Predicate Having
        {
            get => _having;
            set => SetWithTrivia(ref _having, value);
        }

        // Original tokens
        internal Token _selectKeyword;
        internal Token _distinctKeyword;
        internal Token _whereKeyword;
        internal Token _havingKeyword;

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

            if (Where != null)
            {
                yield return _whereKeyword;
                foreach (Token token in Where.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (GroupBy != null)
            {
                foreach (Token token in GroupBy.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (Having != null)
            {
                yield return _havingKeyword;
                foreach (Token token in Having.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (OrderBy != null)
            {
                foreach (Token token in OrderBy.DescendantTokens())
                    yield return token;
            }
        }
    }

    public class SetOperation : QueryExpression
    {
        private QueryExpression _left;
        public QueryExpression Left
        {
            get => _left;
            set => SetWithTrivia(ref _left, value);
        }
        private QueryExpression _right;
        public QueryExpression Right
        {
            get => _right;
            set => SetWithTrivia(ref _right, value);
        }
        public SetOperationType OperationType { get; }

        internal Token _operatorToken;
        internal Token _allToken;

        public SetOperation(QueryExpression left, QueryExpression right, SetOperationType operationType)
        {
            _left = left;
            _right = right;
            OperationType = operationType;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Left.DescendantTokens())
                yield return token;
            yield return _operatorToken;
            if (_allToken != null)
                yield return _allToken;
            foreach (Token token in Right.DescendantTokens())
                yield return token;

            if (OrderBy != null)
            {
                foreach (Token token in OrderBy.DescendantTokens())
                    yield return token;
            }
        }
    }

    public abstract class SqlName : SyntaxElement
    {
        public string Name { get; }
        private readonly Token _token;

        protected SqlName(string name) { Name = name; }

        internal SqlName(Token token)
        {
            Name = token.Lexeme;
            _token = token;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }
    }

    public class ServerName : SqlName
    {
        public ServerName(string name) : base(name) { }
        internal ServerName(Token token) : base(token) { }
    }

    public class DatabaseName : SqlName
    {
        public DatabaseName(string name) : base(name) { }
        internal DatabaseName(Token token) : base(token) { }
    }

    public class SchemaName : SqlName
    {
        public SchemaName(string name) : base(name) { }
        internal SchemaName(Token token) : base(token) { }
    }

    public class ObjectName : SqlName
    {
        public ObjectName(string name) : base(name) { }
        internal ObjectName(Token token) : base(token) { }
    }

    public class ColumnName : SqlName
    {
        public ColumnName(string name) : base(name) { }
        internal ColumnName(Token token) : base(token) { }
    }
}
