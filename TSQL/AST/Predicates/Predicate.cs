using System.Collections.Generic;

namespace TSQL.AST
{
    public abstract class Predicate : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor);

        public interface Visitor<T>
        {
            T VisitComparisonPredicate(Comparison predicate);
            T VisitLikePredicate(Like predicate);
            T VisitBetweenPredicate(Between predicate);
            T VisitNullPredicate(Null predicate);
            T VisitContainsPredicate(Contains predicate);
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
            public Expr Left { get; set; }
            public Token Operator { get; set; }
            public Expr Right { get; set; }

            public Comparison(Expr left, Token @operator, Expr right)
            {
                Left = left;
                Operator = @operator;
                Right = right;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitComparisonPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                yield return Operator;
                foreach (Token token in Right.DescendantTokens())
                    yield return token;
            }
        }

        #endregion

        #region LIKE Predicate

        public class Like : Predicate
        {
            public Expr Left { get; set; }
            public Expr Pattern { get; set; }
            public Expr EscapeExpr { get; set; }
            public bool Negated { get; set; }

            internal Token _notToken;
            internal Token _likeToken;
            internal Token _escapeToken;

            public Like(Expr left, Expr pattern, Expr escapeExpr, bool negated)
            {
                Left = left;
                Pattern = pattern;
                EscapeExpr = escapeExpr;
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
            public Expr Expr { get; set; }
            public Expr LowRangeExpr { get; set; }
            public Expr HighRangeExpr { get; set; }
            public bool Negated { get; set; }

            internal Token _notToken;
            internal Token _betweenToken;
            internal Token _andToken;

            public Between(Expr expr, Expr lowRangeExpr, Expr highRangeExpr, bool negated)
            {
                Expr = expr;
                LowRangeExpr = lowRangeExpr;
                HighRangeExpr = highRangeExpr;
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
            public Expr Expr { get; set; }
            public bool Negated { get; set; }

            internal Token _isToken;
            internal Token _notToken;
            internal Token _nullToken;

            public Null(Expr expr, bool negated)
            {
                Expr = expr;
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

        #region CONTAINS Predicate

        public class Contains : Predicate
        {
            public Expr Column { get; set; }
            public Expr SearchCondition { get; set; }

            internal Token _containsToken;
            internal Token _leftParen;
            internal Token _comma;
            internal Token _rightParen;

            public Contains(Expr column, Expr searchCondition)
            {
                Column = column;
                SearchCondition = searchCondition;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitContainsPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _containsToken;
                yield return _leftParen;
                foreach (Token token in Column.DescendantTokens())
                    yield return token;
                yield return _comma;
                foreach (Token token in SearchCondition.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        #endregion

        #region IN Predicate

        public class In : Predicate
        {
            public Expr Expr { get; set; }
            public bool Negated { get; set; }
            public SyntaxElementList<Expr> ValueList { get; set; }
            public Expr.Subquery Subquery { get; set; }

            internal Token _notToken;
            internal Token _inToken;
            internal Token _leftParen;
            internal Token _rightParen;

            public In(Expr expr, bool negated, SyntaxElementList<Expr> valueList)
            {
                Expr = expr;
                Negated = negated;
                ValueList = valueList;
            }

            public In(Expr expr, bool negated, Expr.Subquery subquery)
            {
                Expr = expr;
                Negated = negated;
                Subquery = subquery;
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
                    foreach (Token token in Subquery.SelectExpression.DescendantTokens())
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
            public Expr Left { get; set; }
            public Token Operator { get; set; }
            public Token QuantifierKeyword { get; set; }
            public Expr.Subquery Subquery { get; set; }

            internal Token _leftParen;
            internal Token _rightParen;

            public Quantifier(Expr left, Token @operator, Token quantifierKeyword, Expr.Subquery subquery)
            {
                Left = left;
                Operator = @operator;
                QuantifierKeyword = quantifierKeyword;
                Subquery = subquery;
            }

            public override T Accept<T>(Visitor<T> visitor) => visitor.VisitQuantifierPredicate(this);

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in Left.DescendantTokens())
                    yield return token;
                yield return Operator;
                yield return QuantifierKeyword;
                yield return _leftParen;
                foreach (Token token in Subquery.SelectExpression.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        #endregion

        #region EXISTS Predicate

        public class Exists : Predicate
        {
            public Expr.Subquery Subquery { get; set; }

            internal Token _existsToken;

            public Exists(Expr.Subquery subquery)
            {
                Subquery = subquery;
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
            public Predicate Predicate { get; set; }

            internal Token _leftParen;
            internal Token _rightParen;

            public Grouping(Predicate predicate)
            {
                Predicate = predicate;
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
            public Predicate Left { get; set; }
            public Predicate Right { get; set; }

            internal Token _andToken;

            public And(Predicate left, Predicate right)
            {
                Left = left;
                Right = right;
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
            public Predicate Left { get; set; }
            public Predicate Right { get; set; }

            internal Token _orToken;

            public Or(Predicate left, Predicate right)
            {
                Left = left;
                Right = right;
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
            public Predicate Predicate { get; set; }

            internal Token _notToken;

            public Not(Predicate predicate)
            {
                Predicate = predicate;
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
