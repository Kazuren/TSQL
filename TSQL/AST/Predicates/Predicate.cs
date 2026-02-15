using System.Collections.Generic;

namespace TSQL.AST
{
    public enum ComparisonOperator { Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, NotLessThan, NotGreaterThan }
    public enum QuantifierType { All, Any, Some }

    public abstract class Predicate : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor);

        internal static ComparisonOperator TokenTypeToComparisonOperator(TokenType type)
        {
            switch (type)
            {
                case TokenType.EQUAL: return ComparisonOperator.Equal;
                case TokenType.NOT_EQUAL: return ComparisonOperator.NotEqual;
                case TokenType.LESS: return ComparisonOperator.LessThan;
                case TokenType.LESS_EQUAL: return ComparisonOperator.LessThanOrEqual;
                case TokenType.GREATER: return ComparisonOperator.GreaterThan;
                case TokenType.GREATER_EQUAL: return ComparisonOperator.GreaterThanOrEqual;
                case TokenType.NOT_LESS: return ComparisonOperator.NotLessThan;
                case TokenType.NOT_GREATER: return ComparisonOperator.NotGreaterThan;
                default: throw new System.ArgumentException($"Unknown comparison operator token type: {type}");
            }
        }

        internal static string ComparisonOperatorToLexeme(ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal: return "=";
                case ComparisonOperator.NotEqual: return "<>";
                case ComparisonOperator.LessThan: return "<";
                case ComparisonOperator.LessThanOrEqual: return "<=";
                case ComparisonOperator.GreaterThan: return ">";
                case ComparisonOperator.GreaterThanOrEqual: return ">=";
                case ComparisonOperator.NotLessThan: return "!<";
                case ComparisonOperator.NotGreaterThan: return "!>";
                default: throw new System.ArgumentException($"Unknown comparison operator: {op}");
            }
        }

        internal static TokenType ComparisonOperatorToTokenType(ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Equal: return TokenType.EQUAL;
                case ComparisonOperator.NotEqual: return TokenType.NOT_EQUAL;
                case ComparisonOperator.LessThan: return TokenType.LESS;
                case ComparisonOperator.LessThanOrEqual: return TokenType.LESS_EQUAL;
                case ComparisonOperator.GreaterThan: return TokenType.GREATER;
                case ComparisonOperator.GreaterThanOrEqual: return TokenType.GREATER_EQUAL;
                case ComparisonOperator.NotLessThan: return TokenType.NOT_LESS;
                case ComparisonOperator.NotGreaterThan: return TokenType.NOT_GREATER;
                default: throw new System.ArgumentException($"Unknown comparison operator: {op}");
            }
        }

        internal static QuantifierType TokenTypeToQuantifierType(TokenType type)
        {
            switch (type)
            {
                case TokenType.ALL: return QuantifierType.All;
                case TokenType.ANY: return QuantifierType.Any;
                case TokenType.SOME: return QuantifierType.Some;
                default: throw new System.ArgumentException($"Unknown quantifier token type: {type}");
            }
        }

        public interface Visitor<T>
        {
            T VisitComparisonPredicate(Comparison predicate);
            T VisitLikePredicate(Like predicate);
            T VisitBetweenPredicate(Between predicate);
            T VisitNullPredicate(Null predicate);
            T VisitContainsPredicate(Contains predicate);
            T VisitFreetextPredicate(Freetext predicate);
            T VisitInPredicate(In predicate);
            T VisitQuantifierPredicate(Quantifier predicate);
            T VisitExistsPredicate(Exists predicate);
            T VisitGroupingPredicate(Grouping predicate);
            T VisitAndPredicate(And predicate);
            T VisitOrPredicate(Or predicate);
            T VisitNotPredicate(Not predicate);
        }

        #region Comparison Predicate

        public class Comparison : Predicate
        {
            private Expr _left;
            public Expr Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            private ComparisonOperator _operator;
            public ComparisonOperator Operator
            {
                get => _operator;
                set
                {
                    _operator = value;
                    _operatorToken = new ConcreteToken(
                        ComparisonOperatorToTokenType(value),
                        ComparisonOperatorToLexeme(value),
                        null);
                    _operatorToken.AddLeadingTrivia(new Whitespace(" "));
                }
            }
            private Expr _right;
            public Expr Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            internal Token _operatorToken;

            internal Comparison(Expr left, Token operatorToken, Expr right)
            {
                _left = left;
                _operatorToken = operatorToken;
                _operator = TokenTypeToComparisonOperator(operatorToken.Type);
                _right = right;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitComparisonPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                yield return _operatorToken;
                foreach (Token token in Right.DescendantTokens())
                    yield return token;
            }
        }

        #endregion

        #region LIKE Predicate

        public class Like : Predicate
        {
            private Expr _left;
            public Expr Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            private Expr _pattern;
            public Expr Pattern
            {
                get => _pattern;
                set => SetWithTrivia(ref _pattern, value);
            }
            private Expr _escapeExpr;
            public Expr EscapeExpr
            {
                get => _escapeExpr;
                set => SetWithTrivia(ref _escapeExpr, value);
            }
            public bool Negated { get; set; }

            internal Token _notToken;
            internal Token _likeToken;
            internal Token _escapeToken;

            public Like(Expr left, Expr pattern, Expr escapeExpr, bool negated)
            {
                _left = left;
                _pattern = pattern;
                _escapeExpr = escapeExpr;
                Negated = negated;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitLikePredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                if (_notToken != null)
                    yield return _notToken;
                yield return _likeToken;
                foreach (Token token in Pattern.DescendantTokens())
                    yield return token;
                if (_escapeToken != null)
                {
                    yield return _escapeToken;
                    foreach (Token token in EscapeExpr.DescendantTokens())
                        yield return token;
                }
            }
        }

        #endregion

        #region BETWEEN Predicate

        public class Between : Predicate
        {
            private Expr _expr;
            public Expr Expr
            {
                get => _expr;
                set => SetWithTrivia(ref _expr, value);
            }
            private Expr _lowRangeExpr;
            public Expr LowRangeExpr
            {
                get => _lowRangeExpr;
                set => SetWithTrivia(ref _lowRangeExpr, value);
            }
            private Expr _highRangeExpr;
            public Expr HighRangeExpr
            {
                get => _highRangeExpr;
                set => SetWithTrivia(ref _highRangeExpr, value);
            }
            public bool Negated { get; set; }

            internal Token _notToken;
            internal Token _betweenToken;
            internal Token _andToken;

            public Between(Expr expr, Expr lowRangeExpr, Expr highRangeExpr, bool negated)
            {
                _expr = expr;
                _lowRangeExpr = lowRangeExpr;
                _highRangeExpr = highRangeExpr;
                Negated = negated;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitBetweenPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Expr.DescendantTokens())
                    yield return token;
                if (_notToken != null)
                    yield return _notToken;
                yield return _betweenToken;
                foreach (Token token in LowRangeExpr.DescendantTokens())
                    yield return token;
                yield return _andToken;
                foreach (Token token in HighRangeExpr.DescendantTokens())
                    yield return token;
            }
        }

        #endregion

        #region IS NULL Predicate

        public class Null : Predicate
        {
            private Expr _expr;
            public Expr Expr
            {
                get => _expr;
                set => SetWithTrivia(ref _expr, value);
            }
            public bool Negated { get; set; }

            internal Token _isToken;
            internal Token _notToken;
            internal Token _nullToken;

            public Null(Expr expr, bool negated)
            {
                _expr = expr;
                Negated = negated;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitNullPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Expr.DescendantTokens())
                    yield return token;
                yield return _isToken;
                if (_notToken != null)
                    yield return _notToken;
                yield return _nullToken;
            }
        }

        #endregion

        #region Full-Text Column Argument

        /// <summary>
        /// Column argument for CONTAINS/FREETEXT: either * or a list of column identifiers.
        /// </summary>
        public abstract class FullTextColumns : SyntaxElement { }

        /// <summary>All full-text indexed columns: *</summary>
        public class FullTextAllColumns : FullTextColumns
        {
            internal Token _wildcardToken;

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _wildcardToken;
            }
        }

        /// <summary>One or more column identifiers, optionally parenthesized.</summary>
        public class FullTextColumnNames : FullTextColumns
        {
            public SyntaxElementList<Expr.ColumnIdentifier> Columns { get; }

            internal Token _leftParen;
            internal Token _rightParen;

            public FullTextColumnNames(SyntaxElementList<Expr.ColumnIdentifier> columns)
            {
                Columns = columns;
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (_leftParen != null)
                {
                    yield return _leftParen;
                }
                foreach (Token token in Columns.DescendantTokens())
                    yield return token;
                if (_rightParen != null)
                {
                    yield return _rightParen;
                }
            }
        }

        #endregion

        #region CONTAINS Predicate

        public class Contains : Predicate
        {
            public FullTextColumns Columns { get; }
            private Expr _searchCondition;
            public Expr SearchCondition
            {
                get => _searchCondition;
                set => SetWithTrivia(ref _searchCondition, value);
            }
            public Expr Language { get; set; }

            internal Token _containsToken;
            internal Token _leftParen;
            internal Token _comma;
            internal Token _languageComma;
            internal Token _languageKeyword;
            internal Token _rightParen;

            public Contains(FullTextColumns columns, Expr searchCondition)
            {
                Columns = columns;
                _searchCondition = searchCondition;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitContainsPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _containsToken;
                yield return _leftParen;
                foreach (Token token in Columns.DescendantTokens())
                    yield return token;
                yield return _comma;
                foreach (Token token in SearchCondition.DescendantTokens())
                    yield return token;
                if (Language != null)
                {
                    yield return _languageComma;
                    yield return _languageKeyword;
                    foreach (Token token in Language.DescendantTokens())
                        yield return token;
                }
                yield return _rightParen;
            }
        }

        #endregion

        #region FREETEXT Predicate

        public class Freetext : Predicate
        {
            public FullTextColumns Columns { get; }
            private Expr _searchCondition;
            public Expr SearchCondition
            {
                get => _searchCondition;
                set => SetWithTrivia(ref _searchCondition, value);
            }
            public Expr Language { get; set; }

            internal Token _freetextToken;
            internal Token _leftParen;
            internal Token _comma;
            internal Token _languageComma;
            internal Token _languageKeyword;
            internal Token _rightParen;

            public Freetext(FullTextColumns columns, Expr searchCondition)
            {
                Columns = columns;
                _searchCondition = searchCondition;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitFreetextPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _freetextToken;
                yield return _leftParen;
                foreach (Token token in Columns.DescendantTokens())
                    yield return token;
                yield return _comma;
                foreach (Token token in SearchCondition.DescendantTokens())
                    yield return token;
                if (Language != null)
                {
                    yield return _languageComma;
                    yield return _languageKeyword;
                    foreach (Token token in Language.DescendantTokens())
                        yield return token;
                }
                yield return _rightParen;
            }
        }

        #endregion

        #region IN Predicate

        public class In : Predicate
        {
            private Expr _expr;
            public Expr Expr
            {
                get => _expr;
                set => SetWithTrivia(ref _expr, value);
            }
            public bool Negated { get; set; }
            public SyntaxElementList<Expr> ValueList { get; set; }
            private Expr.Subquery _subquery;
            public Expr.Subquery Subquery
            {
                get => _subquery;
                set => SetWithTrivia(ref _subquery, value);
            }

            internal Token _notToken;
            internal Token _inToken;
            internal Token _leftParen;
            internal Token _rightParen;

            public In(Expr expr, bool negated, SyntaxElementList<Expr> valueList)
            {
                _expr = expr;
                Negated = negated;
                ValueList = valueList;
            }

            public In(Expr expr, bool negated, Expr.Subquery subquery)
            {
                _expr = expr;
                Negated = negated;
                _subquery = subquery;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitInPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Expr.DescendantTokens())
                    yield return token;
                if (_notToken != null)
                    yield return _notToken;
                yield return _inToken;
                yield return _leftParen;
                if (Subquery != null)
                {
                    foreach (Token token in Subquery.Query.DescendantTokens())
                        yield return token;
                }
                else
                {
                    foreach (Token token in ValueList.DescendantTokens())
                        yield return token;
                }
                yield return _rightParen;
            }
        }

        #endregion

        #region Quantifier Predicate

        public class Quantifier : Predicate
        {
            private Expr _left;
            public Expr Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            public ComparisonOperator Operator { get; set; }
            public QuantifierType QuantifierKind { get; set; }
            private Expr.Subquery _subquery;
            public Expr.Subquery Subquery
            {
                get => _subquery;
                set => SetWithTrivia(ref _subquery, value);
            }

            internal Token _operatorToken;
            internal Token _quantifierToken;
            internal Token _leftParen;
            internal Token _rightParen;

            internal Quantifier(Expr left, Token operatorToken, Token quantifierToken, Expr.Subquery subquery)
            {
                _left = left;
                _operatorToken = operatorToken;
                Operator = TokenTypeToComparisonOperator(operatorToken.Type);
                _quantifierToken = quantifierToken;
                QuantifierKind = TokenTypeToQuantifierType(quantifierToken.Type);
                _subquery = subquery;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitQuantifierPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                yield return _operatorToken;
                yield return _quantifierToken;
                yield return _leftParen;
                foreach (Token token in Subquery.Query.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        #endregion

        #region EXISTS Predicate

        public class Exists : Predicate
        {
            private Expr.Subquery _subquery;
            public Expr.Subquery Subquery
            {
                get => _subquery;
                set => SetWithTrivia(ref _subquery, value);
            }

            internal Token _existsToken;

            public Exists(Expr.Subquery subquery)
            {
                _subquery = subquery;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitExistsPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _existsToken;
                foreach (Token token in Subquery.DescendantTokens())
                    yield return token;
            }
        }

        #endregion

        #region Grouping Predicate

        public class Grouping : Predicate
        {
            private Predicate _predicate;
            public Predicate Predicate
            {
                get => _predicate;
                set => SetWithTrivia(ref _predicate, value);
            }

            internal Token _leftParen;
            internal Token _rightParen;

            public Grouping(Predicate predicate)
            {
                _predicate = predicate;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitGroupingPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _leftParen;
                foreach (Token token in Predicate.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        #endregion

        #region Logical Predicates

        public class And : Predicate
        {
            private Predicate _left;
            public Predicate Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            private Predicate _right;
            public Predicate Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            internal Token _andToken;

            public And(Predicate left, Predicate right)
            {
                _left = left;
                _right = right;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitAndPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                yield return _andToken;
                foreach (Token token in Right.DescendantTokens())
                    yield return token;
            }
        }

        public class Or : Predicate
        {
            private Predicate _left;
            public Predicate Left
            {
                get => _left;
                set => SetWithTrivia(ref _left, value);
            }
            private Predicate _right;
            public Predicate Right
            {
                get => _right;
                set => SetWithTrivia(ref _right, value);
            }

            internal Token _orToken;

            public Or(Predicate left, Predicate right)
            {
                _left = left;
                _right = right;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitOrPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                yield return _orToken;
                foreach (Token token in Right.DescendantTokens())
                    yield return token;
            }
        }

        public class Not : Predicate
        {
            private Predicate _predicate;
            public Predicate Predicate
            {
                get => _predicate;
                set => SetWithTrivia(ref _predicate, value);
            }

            internal Token _notToken;

            public Not(Predicate predicate)
            {
                _predicate = predicate;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitNotPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _notToken;
                foreach (Token token in Predicate.DescendantTokens())
                    yield return token;
            }
        }

        #endregion
    }
}
