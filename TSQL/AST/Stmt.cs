using System.Collections.Generic;
using static TSQL.Expr;

namespace TSQL
{
    #region Statement Base and Select
    public abstract class Stmt : SyntaxElement
    {
        public static Stmt Parse(string sql)
        {
            Scanner scanner = new Scanner(sql);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        public static Select ParseSelect(string sql)
        {
            Scanner scanner = new Scanner(sql);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.ParseSelect();
        }

        public abstract T Accept<T>(Visitor<T> visitor);
        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
        }

        public class Select : Stmt
        {
            public Cte CteStmt { get; set; }
            private QueryExpression _query;
            public QueryExpression Query
            {
                get => _query;
                set => SetWithTrivia(ref _query, value);
            }
            public OptionClause Option { get; set; }

            public Select(QueryExpression query)
            {
                _query = query;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitSelectStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (CteStmt != null)
                {
                    foreach (Token token in CteStmt.DescendantTokens())
                        yield return token;
                }

                foreach (Token token in Query.DescendantTokens())
                {
                    yield return token;
                }

                if (Option != null)
                {
                    foreach (Token token in Option.DescendantTokens())
                        yield return token;
                }
            }
        }
    }
    #endregion

    #region Common Table Expressions
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
        public string Name
        {
            get => _nameToken.Lexeme;
            set { _nameToken = new ConcreteToken(TokenType.IDENTIFIER, value, null); }
        }
        public CteColumnNames ColumnNames { get; set; }
        public Expr.Subquery Query { get; set; }

        internal Token _nameToken;
        internal Token _asToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _nameToken;

            if (ColumnNames != null)
            {
                foreach (Token token in ColumnNames.DescendantTokens())
                    yield return token;
            }

            yield return _asToken;

            foreach (Token token in Query.DescendantTokens())
                yield return token;
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
    #endregion

    #region SELECT Column Types
    public interface SelectItem : ISyntaxElement
    {
    }

    public class SelectColumn : SyntaxElement, SelectItem
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }

        private Alias _alias;
        public Alias Alias
        {
            get => _alias;
            set
            {
                if (_alias is SyntaxElement oldSe && value is SyntaxElement newSe)
                {
                    TransferLeadingTrivia(oldSe, newSe);
                }
                _alias = value;
            }
        }

        public SelectColumn(Expr expression, Alias alias)
        {
            _expression = expression;
            _alias = alias;
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

    public interface Alias : ISyntaxElement
    {
        string Name { get; }
    }

    public class SuffixAlias : SyntaxElement, Alias
    {
        public string Name { get => _nameToken.Lexeme; }
        internal Token _nameToken;
        internal Token _asKeyword;

        public SuffixAlias(string name, bool useAs = true)
        {
            _nameToken = new ConcreteToken(TokenType.IDENTIFIER, name, null);
            _nameToken.AddLeadingTrivia(new Whitespace(" "));
            if (useAs)
            {
                _asKeyword = new ConcreteToken(TokenType.AS, "AS", null);
                _asKeyword.AddLeadingTrivia(new Whitespace(" "));
            }
        }

        internal SuffixAlias(Token name)
        {
            _nameToken = name;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            if (_asKeyword != null)
            {
                yield return _asKeyword;
            }

            yield return _nameToken;
        }
    }

    public class PrefixAlias : SyntaxElement, Alias
    {
        public string Name { get => _nameToken.Lexeme; }
        internal Token _nameToken;
        internal Token _equalsToken;

        public PrefixAlias(string name)
        {
            _nameToken = new ConcreteToken(TokenType.IDENTIFIER, name, null);
            _equalsToken = new ConcreteToken(TokenType.EQUAL, "=", null);
            _equalsToken.AddLeadingTrivia(new Whitespace(" "));
        }

        internal PrefixAlias(Token name, Token equalsToken)
        {
            _nameToken = name;
            _equalsToken = equalsToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _nameToken;
            yield return _equalsToken;
        }
    }
    #endregion

    #region TOP Clause
    public class TopClause : SyntaxElement
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }
        public bool Percent { get; set; }
        public bool WithTies { get; set; }

        public TopClause(Expr expr)
        {
            _expression = expr;
        }

        internal Token _topKeyword;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _percentKeyword;
        internal Token _withKeyword;
        internal Token _tiesKeyword;

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

            if (_percentKeyword != null)
            {
                yield return _percentKeyword;
            }

            if (_withKeyword != null)
            {
                yield return _withKeyword;
                yield return _tiesKeyword;
            }
        }
    }
    #endregion
}
