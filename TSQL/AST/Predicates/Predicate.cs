using System;

namespace TSQL.AST
{
    public abstract class Predicate : SyntaxElement
    {
        public class Comparison : Predicate
        {
            public Expr Left { get; private set; }
            public Token Operator { get; private set; }
            public Expr Right { get; private set; }
            public Comparison(Expr left, Token @operator, Expr right)
            {
                Left = left;
                Operator = @operator;
                Right = right;
            }

            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }
        public class Like : Predicate
        {
            public Expr Left { get; private set; }
            public Expr Right { get; private set; }

            public Token EscapeCharacter { get; private set; }
            public bool Negated { get; private set; } = false;

            public Like(Expr left, Expr right, Token escapeCharacter, bool negated)
            {
                Left = left;
                Right = right;
                EscapeCharacter = escapeCharacter;
                Negated = negated;
            }

            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Between : Predicate
        {
            public Expr Expr { get; private set; }
            public Expr LowRangeExpr { get; private set; }
            public Expr HighRangeExpr { get; private set; }
            public bool Negated { get; private set; } = false;

            public Between(Expr expr, Expr lowRangeExpr, Expr highRangeExpr, bool negated)
            {
                Expr = expr;
                LowRangeExpr = lowRangeExpr;
                HighRangeExpr = highRangeExpr;
                Negated = negated;
            }
            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Null : Predicate
        {
            public Expr Expr { get; private set; }
            public bool Negated { get; private set; }

            public Null(Expr expr, bool negated)
            {
                Expr = expr;
                Negated = negated;
            }

            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Contains : Predicate
        {
            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class In : Predicate
        {
            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Quantifier : Predicate
        {
            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Exists : Predicate
        {
            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Grouping : Predicate
        {
            public Predicate Predicate { get; private set; }
            public Grouping(Predicate predicate)
            {
                Predicate = predicate;
            }
            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class And : Predicate
        {
            public Predicate Left { get; private set; }
            public Predicate Right { get; private set; }

            public And(Predicate left, Predicate right)
            {
                Left = left;
                Right = right;
            }

            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Or : Predicate
        {
            public Predicate Left { get; private set; }
            public Predicate Right { get; private set; }

            public Or(Predicate left, Predicate right)
            {
                Left = left;
                Right = right;
            }

            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }

        public class Not : Predicate
        {
            public Predicate Predicate { get; private set; }
            public Not(Predicate predicate)
            {
                Predicate = predicate;
            }

            public override string ToSource()
            {
                throw new NotImplementedException();
            }
        }
    }
}
