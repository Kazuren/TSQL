using System;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    /// <summary>
    /// Public interface for tokens, exposing Lexeme as string for external consumption.
    /// </summary>
    public interface IToken
    {
        TokenType Type { get; }
        string Lexeme { get; }
        object Literal { get; }
        IReadOnlyList<Trivia> LeadingTrivia { get; }
        IReadOnlyList<Trivia> TrailingTrivia { get; }
    }

    /// <summary>
    /// A token created programmatically (not from source parsing). Stores lexeme as string.
    /// </summary>
    public class ConcreteToken : Token
    {
        public static ConcreteToken Empty = new ConcreteToken(TokenType.NONE, null, null);

        private readonly string _lexeme;

        public ConcreteToken(TokenType type, string lexeme, object literal) : base(type, literal)
        {
            _lexeme = lexeme;
        }

        public override string Lexeme => _lexeme;
    }

    /// <summary>
    /// A token parsed from source code. Stores lexeme as StringSlice for deferred allocation.
    /// </summary>
    public class SourceToken : Token
    {
        private readonly StringSlice _lexemeSlice;
        private string _lexemeCache;

        public int Line { get; }

        /// <summary>
        /// Creates a SourceToken with a pre-allocated lexeme string.
        /// </summary>
        public SourceToken(TokenType type, string lexeme, object literal, int line) : base(type, literal)
        {
            _lexemeSlice = StringSlice.FromString(lexeme);
            _lexemeCache = lexeme; // Already allocated, cache it
            Line = line;
        }

        /// <summary>
        /// Internal constructor for creating tokens with deferred string allocation.
        /// </summary>
        internal SourceToken(TokenType type, StringSlice lexemeSlice, object literal, int line) : base(type, literal)
        {
            _lexemeSlice = lexemeSlice;
            Line = line;
        }

        public override string Lexeme
        {
            get
            {
                // Lazy allocation: only create string when actually accessed
                if (_lexemeCache == null)
                {
                    _lexemeCache = _lexemeSlice.ToString();
                }
                return _lexemeCache;
            }
        }
    }

    /// <summary>
    /// Abstract base class for tokens with shared functionality.
    /// </summary>
    public abstract class Token : IToken, IEquatable<Token>
    {
        private static readonly IReadOnlyList<Trivia> EmptyTrivia = Array.Empty<Trivia>();

        public TokenType Type { get; }
        public abstract string Lexeme { get; }
        public object Literal { get; }
        public IReadOnlyList<Trivia> LeadingTrivia => _leadingTrivia ?? EmptyTrivia;
        public IReadOnlyList<Trivia> TrailingTrivia => _trailingTrivia ?? EmptyTrivia;

        private List<Trivia> _leadingTrivia;
        private List<Trivia> _trailingTrivia;

        protected Token(TokenType type, object literal)
        {
            Type = type;
            Literal = literal;
        }

        public override string ToString()
        {
            return $"{Type} {Lexeme} {Literal}";
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
