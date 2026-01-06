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
            public IReadOnlyList<Trivia> LeadingTrivia => GetTriviaList(_leadingTriviaSingle, _leadingTriviaList);
            public IReadOnlyList<Trivia> TrailingTrivia => GetTriviaList(_trailingTriviaSingle, _trailingTriviaList);

            // Single-element optimization: store one trivia directly, only allocate list for 2+
            private Trivia _leadingTriviaSingle;
            private List<Trivia> _leadingTriviaList;
            private Trivia _trailingTriviaSingle;
            private List<Trivia> _trailingTriviaList;

            protected Token(TokenType type, object literal)
            {
                Type = type;
                Literal = literal;
            }

            private static IReadOnlyList<Trivia> GetTriviaList(Trivia single, List<Trivia> list)
            {
                if (list != null) return list;
                if (single != null) return new SingleItemReadOnlyList<Trivia>(single);
                return EmptyTrivia;
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
                foreach (var t in trivia)
                    AddLeadingTrivia(t);
            }

            internal void AddLeadingTrivia(IEnumerable<Trivia> trivia)
            {
                foreach (var t in trivia)
                    AddLeadingTrivia(t);
            }

            internal void AddLeadingTrivia(Trivia trivia)
            {
                if (trivia == null) return;

                if (_leadingTriviaList != null)
                {
                    // Already have a list, just add to it
                    _leadingTriviaList.Add(trivia);
                }
                else if (_leadingTriviaSingle == null)
                {
                    // First item - store directly without list allocation
                    _leadingTriviaSingle = trivia;
                }
                else
                {
                    // Second item - need to promote to list
                    _leadingTriviaList = new List<Trivia>(4) { _leadingTriviaSingle, trivia };
                    _leadingTriviaSingle = null;
                }
            }

            internal void AddTrailingTrivia(params Trivia[] trivia)
            {
                foreach (var t in trivia)
                    AddTrailingTrivia(t);
            }

            internal void AddTrailingTrivia(IEnumerable<Trivia> trivia)
            {
                foreach (var t in trivia)
                    AddTrailingTrivia(t);
            }

            internal void AddTrailingTrivia(Trivia trivia)
            {
                if (trivia == null) return;

                if (_trailingTriviaList != null)
                {
                    // Already have a list, just add to it
                    _trailingTriviaList.Add(trivia);
                }
                else if (_trailingTriviaSingle == null)
                {
                    // First item - store directly without list allocation
                    _trailingTriviaSingle = trivia;
                }
                else
                {
                    // Second item - need to promote to list
                    _trailingTriviaList = new List<Trivia>(4) { _trailingTriviaSingle, trivia };
                    _trailingTriviaSingle = null;
                }
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

        /// <summary>
        /// A lightweight read-only list wrapper for a single item, avoiding list allocation.
        /// </summary>
        internal readonly struct SingleItemReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly T _item;

            public SingleItemReadOnlyList(T item) => _item = item;

            public T this[int index] => index == 0 ? _item : throw new IndexOutOfRangeException();
            public int Count => 1;

            public IEnumerator<T> GetEnumerator()
            {
                yield return _item;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
