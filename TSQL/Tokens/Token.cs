using System;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{


    public class ConcreteToken : Token
    {
        public static ConcreteToken Empty = new ConcreteToken(TokenType.NONE, null, null);
        public ConcreteToken(TokenType type, string lexeme, object literal) : base(type, lexeme, literal)
        {

        }
    }

    public class SourceToken : Token
    {
        public int Line { get => _line; private set => _line = value; }
        private int _line;
        public SourceToken(TokenType type, string lexeme, object literal, int line) : base(type, lexeme, literal)
        {
            _line = line;
        }
    }

    public abstract class Token : IEquatable<Token>
    {
        private static readonly IReadOnlyList<Trivia> EmptyTrivia = Array.Empty<Trivia>();

        public TokenType Type { get => _type; private set => _type = value; }
        public string Lexeme { get => _lexeme; private set => _lexeme = value; }
        public object Literal { get => _literal; private set => _literal = value; }
        public IReadOnlyList<Trivia> LeadingTrivia { get => _leadingTrivia ?? EmptyTrivia; }
        public IReadOnlyList<Trivia> TrailingTrivia { get => _trailingTrivia ?? EmptyTrivia; }

        private TokenType _type;
        private string _lexeme;
        private object _literal;

        private List<Trivia> _leadingTrivia;
        private List<Trivia> _trailingTrivia;

        public Token(TokenType type, string lexeme, object literal)
        {
            _type = type;
            _lexeme = lexeme;
            _literal = literal;
        }

        public override string ToString()
        {
            return $"{_type} {_lexeme} {_literal}";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Token);
        }

        public bool Equals(Token other)
        {
            return !(other is null) &&
                   Type == other.Type &&
                   Lexeme == other.Lexeme &&
                   EqualityComparer<object>.Default.Equals(Literal, other.Literal);
        }

        public override int GetHashCode()
        {
            return (Type, Lexeme, Literal).GetHashCode();
        }

        public static bool operator ==(Token left, Token right)
        {
            return EqualityComparer<Token>.Default.Equals(left, right);
        }

        public static bool operator !=(Token left, Token right)
        {
            return !(left == right);
        }

        internal void AddLeadingTrivia(params Trivia[] trivia)
        {
            if (_leadingTrivia == null) _leadingTrivia = new List<Trivia>(trivia.Length);
            _leadingTrivia.AddRange(trivia);
        }
        internal void AddLeadingTrivia(IEnumerable<Trivia> trivia)
        {
            if (_leadingTrivia == null) _leadingTrivia = new List<Trivia>();
            _leadingTrivia.AddRange(trivia);
        }
        internal void AddLeadingTrivia(Trivia trivia)
        {
            if (_leadingTrivia == null) _leadingTrivia = new List<Trivia>(2);
            _leadingTrivia.Add(trivia);
        }

        internal void AddTrailingTrivia(params Trivia[] trivia)
        {
            if (_trailingTrivia == null) _trailingTrivia = new List<Trivia>(trivia.Length);
            _trailingTrivia.AddRange(trivia);
        }
        internal void AddTrailingTrivia(IEnumerable<Trivia> trivia)
        {
            if (_trailingTrivia == null) _trailingTrivia = new List<Trivia>();
            _trailingTrivia.AddRange(trivia);
        }
        internal void AddTrailingTrivia(Trivia trivia)
        {
            if (_trailingTrivia == null) _trailingTrivia = new List<Trivia>(2);
            _trailingTrivia.Add(trivia);
        }

        public string ToSource()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Trivia trivia in LeadingTrivia)
            {
                sb.Append(trivia.Content);
            }

            sb.Append(Lexeme);

            return sb.ToString();
        }
    }
}
