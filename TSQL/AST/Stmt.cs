using System.Collections.Generic;
using static TSQL.Expr;

namespace TSQL
{
    #region Statement Base and Select
    public abstract class Stmt : SyntaxElement
    {
        /// <summary>Parses a SQL statement from the given string.</summary>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Stmt Parse(string sql)
        {
            return Parser.CreateParser(sql).Parse();
        }

        /// <summary>Parses a SQL SELECT statement from the given string.</summary>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Select ParseSelect(string sql)
        {
            return Parser.CreateParser(sql).ParseSelect();
        }

        /// <summary>Parses a SQL INSERT statement from the given string.</summary>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Insert ParseInsert(string sql)
        {
            return Parser.CreateParser(sql).ParseInsert();
        }

        public abstract T Accept<T>(Visitor<T> visitor);
        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
            T VisitInsertStmt(Stmt.Insert stmt);
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

        public class Insert : Stmt
        {
            public Cte CteStmt { get; set; }
            public Expr Target { get; }
            public InsertColumnList ColumnList { get; set; }
            public InsertSource Source { get; }

            internal Token _insertToken;
            internal Token _intoToken;

            public Insert(Expr target, InsertSource source)
            {
                Target = target;
                Source = source;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitInsertStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (CteStmt != null)
                {
                    foreach (Token token in CteStmt.DescendantTokens())
                        yield return token;
                }

                yield return _insertToken;

                if (_intoToken != null)
                {
                    yield return _intoToken;
                }

                foreach (Token token in Target.DescendantTokens())
                {
                    yield return token;
                }

                if (ColumnList != null)
                {
                    foreach (Token token in ColumnList.DescendantTokens())
                        yield return token;
                }

                foreach (Token token in Source.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }
    #endregion

    #region INSERT Source Types

    public abstract class InsertSource : SyntaxElement { }

    public class SelectSource : InsertSource
    {
        public QueryExpression Query { get; }
        public OptionClause Option { get; set; }

        public SelectSource(QueryExpression query)
        {
            Query = query;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Query.DescendantTokens())
                yield return token;

            if (Option != null)
            {
                foreach (Token token in Option.DescendantTokens())
                    yield return token;
            }
        }
    }

    public class ValuesSource : InsertSource
    {
        public SyntaxElementList<ValuesRow> Rows { get; }

        internal Token _valuesToken;

        public ValuesSource(SyntaxElementList<ValuesRow> rows)
        {
            Rows = rows;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _valuesToken;
            foreach (Token token in Rows.DescendantTokens())
                yield return token;
        }
    }

    public class DefaultValuesSource : InsertSource
    {
        internal Token _defaultToken;
        internal Token _valuesToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _defaultToken;
            yield return _valuesToken;
        }
    }

    public class ExecSource : InsertSource
    {
        public Expr.ObjectIdentifier ProcedureName { get; }
        public SyntaxElementList<Expr> Arguments { get; }

        internal Token _execToken;

        public ExecSource(Expr.ObjectIdentifier procedureName, SyntaxElementList<Expr> arguments)
        {
            ProcedureName = procedureName;
            Arguments = arguments;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _execToken;
            foreach (Token token in ProcedureName.DescendantTokens())
                yield return token;

            if (Arguments.Count > 0)
            {
                foreach (Token token in Arguments.DescendantTokens())
                    yield return token;
            }
        }
    }

    public class InsertColumnList : SyntaxElement
    {
        public SyntaxElementList<ColumnName> Columns { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public InsertColumnList(SyntaxElementList<ColumnName> columns)
        {
            Columns = columns;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;

            foreach (Token token in Columns.DescendantTokens())
            {
                yield return token;
            }

            yield return _rightParen;
        }
    }

    #endregion

    #region Script

    public class Script : SyntaxElement
    {
        public IReadOnlyList<Stmt> Statements { get; }
        internal readonly List<Token> _semicolons;

        /// <summary>Parses a SQL script containing one or more statements.</summary>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Script Parse(string sql)
        {
            return Parser.CreateParser(sql).ParseScript();
        }

        internal Script(IReadOnlyList<Stmt> statements, List<Token> semicolons)
        {
            Statements = statements;
            _semicolons = semicolons;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            for (int i = 0; i < Statements.Count; i++)
            {
                foreach (Token token in Statements[i].DescendantTokens())
                {
                    yield return token;
                }

                if (_semicolons[i] != null)
                {
                    yield return _semicolons[i];
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
            _nameToken = ConcreteToken.WithLeadingSpace(TokenType.IDENTIFIER, name);
            if (useAs)
            {
                _asKeyword = ConcreteToken.WithLeadingSpace(TokenType.AS, "AS");
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
            _equalsToken = ConcreteToken.WithLeadingSpace(TokenType.EQUAL, "=");
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

    [System.Flags]
    public enum TopModifier { None = 0, Percent = 1, WithTies = 2 }

    public class TopClause : SyntaxElement
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }
        public TopModifier Modifiers { get; set; }

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
