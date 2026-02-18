using System;
using System.Collections.Generic;

namespace TSQL
{
    public abstract partial class Expr : SyntaxElement
    {
        #region Base, Visitor, and Enums

        public abstract T Accept<T>(Visitor<T> visitor);

        public interface Visitor<T>
        {
            T VisitBinaryExpr(Binary expr);
            T VisitStringLiteralExpr(StringLiteral expr);
            T VisitIntLiteralExpr(IntLiteral expr);
            T VisitDecimalLiteralExpr(DecimalLiteral expr);
            T VisitNullLiteralExpr(NullLiteral expr);
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

        /// <summary>
        /// Factory method that creates the appropriate literal subtype based on the value's runtime type.
        /// </summary>
        public static Expr Literal(object value)
        {
            if (value == null)
            {
                return new NullLiteral();
            }
            else if (value is string s)
            {
                return new StringLiteral(s);
            }
            else if (value is int i)
            {
                return new IntLiteral(i);
            }
            else if (value is double || value is decimal || value is float)
            {
                return new DecimalLiteral(System.Convert.ToDecimal(value));
            }
            else
            {
                throw new System.ArgumentException($"Expected literal to be null, string, int, double, float or decimal but got: {value.GetType().FullName}", nameof(value));
            }
        }

        public enum ArithmeticOperator { Add, Subtract, Multiply, Divide, Modulo, BitwiseAnd, BitwiseOr, BitwiseXor }
        public enum UnaryOperator { Negate, BitwiseNot }

        private static readonly Dictionary<ArithmeticOperator, (TokenType Type, string Lexeme)> ArithmeticOpToToken =
            new Dictionary<ArithmeticOperator, (TokenType, string)>
            {
                { ArithmeticOperator.Add, (TokenType.PLUS, "+") },
                { ArithmeticOperator.Subtract, (TokenType.MINUS, "-") },
                { ArithmeticOperator.Multiply, (TokenType.STAR, "*") },
                { ArithmeticOperator.Divide, (TokenType.SLASH, "/") },
                { ArithmeticOperator.Modulo, (TokenType.MODULO, "%") },
                { ArithmeticOperator.BitwiseAnd, (TokenType.BITWISE_AND, "&") },
                { ArithmeticOperator.BitwiseOr, (TokenType.BITWISE_OR, "|") },
                { ArithmeticOperator.BitwiseXor, (TokenType.BITWISE_XOR, "^") },
            };

        private static readonly Dictionary<TokenType, ArithmeticOperator> TokenToArithmeticOp = BuildReverse(ArithmeticOpToToken);

        private static readonly Dictionary<UnaryOperator, (TokenType Type, string Lexeme)> UnaryOpToToken =
            new Dictionary<UnaryOperator, (TokenType, string)>
            {
                { UnaryOperator.Negate, (TokenType.MINUS, "-") },
                { UnaryOperator.BitwiseNot, (TokenType.BITWISE_NOT, "~") },
            };

        private static readonly Dictionary<TokenType, UnaryOperator> TokenToUnaryOp = BuildReverse(UnaryOpToToken);

        private static Dictionary<TokenType, TEnum> BuildReverse<TEnum>(Dictionary<TEnum, (TokenType Type, string Lexeme)> forward)
            where TEnum : struct
        {
            var reverse = new Dictionary<TokenType, TEnum>();
            foreach (var kvp in forward)
            {
                reverse[kvp.Value.Type] = kvp.Key;
            }
            return reverse;
        }

        internal static ArithmeticOperator TokenTypeToArithmeticOperator(TokenType type) => TokenToArithmeticOp[type];
        internal static string ArithmeticOperatorToLexeme(ArithmeticOperator op) => ArithmeticOpToToken[op].Lexeme;
        internal static TokenType ArithmeticOperatorToTokenType(ArithmeticOperator op) => ArithmeticOpToToken[op].Type;

        internal static UnaryOperator TokenTypeToUnaryOperator(TokenType type) => TokenToUnaryOp[type];
        internal static string UnaryOperatorToLexeme(UnaryOperator op) => UnaryOpToToken[op].Lexeme;
        internal static TokenType UnaryOperatorToTokenType(UnaryOperator op) => UnaryOpToToken[op].Type;

        #endregion

        #region SQL Identifiers

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
                _serverToDatabaseDot = new ConcreteToken(TokenType.DOT, ".", null);
                _databaseToSchemaDot = new ConcreteToken(TokenType.DOT, ".", null);
                _schemaToObjectDot = new ConcreteToken(TokenType.DOT, ".", null);
                databaseName.FirstToken()?.ClearLeadingTrivia();
                schemaName.FirstToken()?.ClearLeadingTrivia();
                objectName.FirstToken()?.ClearLeadingTrivia();
            }

            public ObjectIdentifier(DatabaseName databaseName, SchemaName schemaName, ObjectName objectName)
            {
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
                _databaseToSchemaDot = new ConcreteToken(TokenType.DOT, ".", null);
                _schemaToObjectDot = new ConcreteToken(TokenType.DOT, ".", null);
                schemaName.FirstToken()?.ClearLeadingTrivia();
                objectName.FirstToken()?.ClearLeadingTrivia();
            }

            public ObjectIdentifier(SchemaName schemaName, ObjectName objectName)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
                _schemaToObjectDot = new ConcreteToken(TokenType.DOT, ".", null);
                objectName.FirstToken()?.ClearLeadingTrivia();
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
            internal Token _wildcardToken;

            public Wildcard()
            {
                _wildcardToken = ConcreteToken.WithLeadingSpace(TokenType.STAR, "*");
            }

            internal Wildcard(Token wildcardToken)
            {
                _wildcardToken = wildcardToken;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitWildcardExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _wildcardToken;
            }
        }

        public class QualifiedWildcard : SqlIdentifier, SelectItem
        {
            public DatabaseName DatabaseName { get; }
            public SchemaName SchemaName { get; }
            public ObjectName ObjectName { get; }
            internal Token _wildcardToken;

            internal Token _databaseToSchemaDot;
            internal Token _schemaToObjectDot;
            internal Token _objectToStarDot;

            internal QualifiedWildcard(DatabaseName databaseName, SchemaName schemaName, ObjectName objectName, Token wildcardToken)
            {
                DatabaseName = databaseName;
                SchemaName = schemaName;
                ObjectName = objectName;
                _wildcardToken = wildcardToken;
            }

            internal QualifiedWildcard(DatabaseName databaseName, ObjectName objectName, Token wildcardToken)
            {
                DatabaseName = databaseName;
                ObjectName = objectName;
                _wildcardToken = wildcardToken;
            }

            internal QualifiedWildcard(SchemaName schemaName, ObjectName objectName, Token wildcardToken)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
                _wildcardToken = wildcardToken;
            }

            internal QualifiedWildcard(ObjectName objectName, Token wildcardToken)
            {
                ObjectName = objectName;
                _wildcardToken = wildcardToken;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitQualifiedWildcardExpr(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in YieldQualifiedPrefix(DatabaseName, _databaseToSchemaDot, SchemaName, _schemaToObjectDot, ObjectName, _objectToStarDot))
                    yield return token;
                yield return _wildcardToken;
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
                _databaseToSchemaDot = new ConcreteToken(TokenType.DOT, ".", null);
                _schemaToObjectDot = new ConcreteToken(TokenType.DOT, ".", null);
                _objectToColumnDot = new ConcreteToken(TokenType.DOT, ".", null);
                schemaName.FirstToken()?.ClearLeadingTrivia();
                objectName.FirstToken()?.ClearLeadingTrivia();
                columnName.FirstToken()?.ClearLeadingTrivia();
            }

            public ColumnIdentifier(DatabaseName databaseName, ObjectName objectName, ColumnName columnName)
            {
                DatabaseName = databaseName;
                ObjectName = objectName;
                ColumnName = columnName;
                _databaseToSchemaDot = new ConcreteToken(TokenType.DOT, ".", null);
                _schemaToObjectDot = new ConcreteToken(TokenType.DOT, ".", null);
                _objectToColumnDot = new ConcreteToken(TokenType.DOT, ".", null);
                objectName.FirstToken()?.ClearLeadingTrivia();
                columnName.FirstToken()?.ClearLeadingTrivia();
            }

            public ColumnIdentifier(SchemaName schemaName, ObjectName objectName, ColumnName columnName)
            {
                SchemaName = schemaName;
                ObjectName = objectName;
                ColumnName = columnName;
                _schemaToObjectDot = new ConcreteToken(TokenType.DOT, ".", null);
                _objectToColumnDot = new ConcreteToken(TokenType.DOT, ".", null);
                objectName.FirstToken()?.ClearLeadingTrivia();
                columnName.FirstToken()?.ClearLeadingTrivia();
            }

            public ColumnIdentifier(ObjectName objectName, ColumnName columnName)
            {
                ObjectName = objectName;
                ColumnName = columnName;
                _objectToColumnDot = new ConcreteToken(TokenType.DOT, ".", null);
                columnName.FirstToken()?.ClearLeadingTrivia();
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

        #endregion

        #region Function Call

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

        #endregion

        #region Operators

        public class Binary : Expr
        {
            private Expr _left;
            public Expr Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            private ArithmeticOperator _operator;
            public ArithmeticOperator Operator
            {
                get => _operator;
                set
                {
                    _operator = value;
                    _operatorToken = ConcreteToken.WithLeadingSpace(
                        ArithmeticOperatorToTokenType(value),
                        ArithmeticOperatorToLexeme(value));
                }
            }
            private Expr _right;
            public Expr Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            internal Token _operatorToken;

            public Binary(Expr left, ArithmeticOperator op, Expr right)
            {
                _left = left;
                _operator = op;
                _operatorToken = ConcreteToken.WithLeadingSpace(
                    ArithmeticOperatorToTokenType(op),
                    ArithmeticOperatorToLexeme(op));
                _right = right;
            }

            internal Binary(Token operatorToken)
            {
                _operatorToken = operatorToken;
                _operator = TokenTypeToArithmeticOperator(operatorToken.Type);
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

                yield return _operatorToken;

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
                _token = ConcreteToken.WithLeadingSpace(TokenType.VARIABLE, name);
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

        #endregion

        #region Literals

        public class StringLiteral : Expr
        {
            public string Value { get; }
            internal Token _token;

            public StringLiteral(string value)
            {
                Value = value;
                _token = ConcreteToken.WithLeadingSpace(TokenType.STRING, "'" + value.Replace("'", "''") + "'", value);
            }

            internal StringLiteral(Token token)
            {
                Value = (string)token.Literal;
                _token = token;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitStringLiteralExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _token;
            }
        }

        public class IntLiteral : Expr
        {
            public int Value { get; }
            internal Token _token;

            public IntLiteral(int value)
            {
                Value = value;
                _token = ConcreteToken.WithLeadingSpace(TokenType.WHOLE_NUMBER, value.ToString(), value);
            }

            internal IntLiteral(Token token)
            {
                Value = (int)token.Literal;
                _token = token;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitIntLiteralExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _token;
            }
        }

        public class DecimalLiteral : Expr
        {
            public decimal Value { get; }
            internal Token _token;

            public DecimalLiteral(decimal value)
            {
                Value = value;
                _token = ConcreteToken.WithLeadingSpace(TokenType.DECIMAL, value.ToString(), value);
            }

            internal DecimalLiteral(Token token)
            {
                Value = System.Convert.ToDecimal(token.Literal);
                _token = token;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitDecimalLiteralExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _token;
            }
        }

        public class NullLiteral : Expr
        {
            internal Token _token;

            public NullLiteral()
            {
                _token = ConcreteToken.WithLeadingSpace(TokenType.NULL, "NULL");
            }

            internal NullLiteral(Token token)
            {
                _token = token;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitNullLiteralExpr(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _token;
            }
        }

        #endregion

        #region Unary and Expression Modifiers

        public class Unary : Expr
        {
            private UnaryOperator _operator;
            public UnaryOperator Operator
            {
                get => _operator;
                set
                {
                    _operator = value;
                    _operatorToken = new ConcreteToken(
                        UnaryOperatorToTokenType(value),
                        UnaryOperatorToLexeme(value),
                        null);
                }
            }
            private Expr _right;
            public Expr Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            internal Token _operatorToken;

            public Unary(UnaryOperator op, Expr right)
            {
                _operator = op;
                _operatorToken = new ConcreteToken(
                    UnaryOperatorToTokenType(op),
                    UnaryOperatorToLexeme(op),
                    null);
                _right = right;
            }

            internal Unary(Token operatorToken, Expr right)
            {
                _operatorToken = operatorToken;
                _operator = TokenTypeToUnaryOperator(operatorToken.Type);
                _right = right;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitUnaryExpr(this);
            }
            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _operatorToken;
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

        #endregion

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

        #region Grouping and Subquery

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

        #endregion

        #region Window Function

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

        #endregion

        #region CASE Expressions

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

        #endregion

        #region CAST and CONVERT

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

        #endregion
    }
}
