using System.Collections.Generic;
using static TSQL.Expr;

namespace TSQL
{
    public abstract class Stmt : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor);
        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
        }

        public class Select : Stmt
        {
            public Cte CteStmt;
            public SelectExpression SelectExpression;

            public Select(SelectExpression selectExpression)
            {
                SelectExpression = selectExpression;
            }

            public override T Accept<T>(Stmt.Visitor<T> visitor)
            {
                return visitor.VisitSelectStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                foreach (Token token in SelectExpression.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }

    public class Cte : SyntaxElement
    {
        public SyntaxElementList<CteDefinition> Ctes { get; set; } = new SyntaxElementList<CteDefinition>();

        internal Token _withToken;
        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _withToken;

            foreach (Token token in Ctes.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class CteDefinition : SyntaxElement
    {
        public Token Name { get; set; }
        public CteColumnNames ColumnNames { get; set; }
        public Expr.Subquery Query { get; set; }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Name;

            foreach (Token token in ColumnNames.DescendantTokens())
            {
                yield return token;
            }

            foreach (Token token in Query.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class CteColumnNames : SyntaxElement
    {
        public SyntaxElementList<ColumnName> ColumnNames { get; set; }
        internal Token _leftParen;
        internal Token _rightParen;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;

            foreach (Token token in ColumnNames.DescendantTokens())
            {
                yield return token;
            }

            yield return _rightParen;
        }
    }

    public class SelectColumn : SyntaxElement
    {
        public Expr Expression { get; }
        public Alias Alias { get; }

        public SelectColumn(Expr expression, Alias alias)
        {
            Expression = expression;
            Alias = alias;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            if (Alias is PrefixAlias prefixAlias)
            {
                foreach (Token token in prefixAlias.DescendantTokens())
                {
                    yield return token;
                }

                foreach (Token token in Expression.DescendantTokens())
                {
                    yield return token;
                }
            }
            else
            {
                foreach (Token token in Expression.DescendantTokens())
                {
                    yield return token;
                }

                if (Alias is SuffixAlias suffixAlias)
                {
                    foreach (Token token in suffixAlias.DescendantTokens())
                    {
                        yield return token;
                    }
                }
            }
        }
    }

    public interface Alias
    {
        Token Name { get; }
        IEnumerable<Token> DescendantTokens();
    }

    internal class SuffixAlias : SyntaxElement, Alias
    {
        public Token Name { get; }
        internal Token _asKeyword;
        public SuffixAlias(Token name)
        {
            Name = name;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            if (_asKeyword != null)
            {
                yield return _asKeyword;
            }

            yield return Name;
        }
    }

    internal class PrefixAlias : SyntaxElement, Alias
    {
        public Token Name { get; }
        internal Token _equalsToken;
        public PrefixAlias(Token name)
        {
            Name = name;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Name;
            yield return _equalsToken;
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
        internal Token _leftParen;
        internal Token _rightParen;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _topKeyword;
            if (_leftParen != null)
            {
                yield return _leftParen;
            }

            foreach (Token token in Expression.DescendantTokens())
            {
                yield return token;
            }

            if (_rightParen != null)
            {
                yield return _rightParen;
            }
        }
    }

    public class FromClause : SyntaxElement
    {
        public TableSource TableSource { get; set; }
        public Alias Alias { get; set; }

        internal Token _fromToken;
        internal FromClause(Token fromToken)
        {
            _fromToken = fromToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _fromToken;

            foreach (Token token in TableSource.DescendantTokens())
            {
                yield return token;
            }

            if (Alias != null)
            {
                foreach (Token token in Alias.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }

    public class JoinClause : SyntaxElement
    {
        public string JoinType { get; set; } // INNER, LEFT, RIGHT, FULL, CROSS
        public TableSource TableSource { get; set; }
        public Alias Alias { get; set; }
        public Expr OnCondition { get; set; }

        public override IEnumerable<Token> DescendantTokens()
        {
            throw new System.NotImplementedException();
        }
    }


    public class OrderByItem : SyntaxElement
    {
        public Expr Expression { get; set; }
        public bool Descending { get; set; }


        // ASC, DESC
        public Token _orderToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Expression.DescendantTokens())
            {
                yield return token;
            }

            if (_orderToken != null)
            {
                yield return _orderToken;
            }
        }
    }

    public abstract class TableSource : SyntaxElement
    {
        public Alias Alias { get; set; }
    }

    public class TableReference : TableSource
    {
        public Token TableName { get; set; }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return TableName;
        }
    }

    public class SubqueryReference : TableSource
    {
        public Expr.Subquery Subquery { get; set; }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Subquery.DescendantTokens())
            {
                yield return token;
            }
        }
    }
}
