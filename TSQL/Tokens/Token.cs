using System;
using System.Collections.Generic;

namespace TSQL
{
    public class Token : IEquatable<Token>
    {
        public TokenType Type { get => _type; private set => _type = value; }
        public string Lexeme { get => _lexeme; private set => _lexeme = value; }
        public object Literal { get => _literal; private set => _literal = value; }
        public int Line { get => _line; private set => _line = value; }
        public IReadOnlyList<Trivia> LeadingTrivia { get => _leadingTrivia; }
        public IReadOnlyList<Trivia> TrailingTrivia { get => _trailingTrivia; }

        internal List<Trivia> _leadingTrivia = new List<Trivia>();
        internal List<Trivia> _trailingTrivia = new List<Trivia>();

        private TokenType _type;
        private string _lexeme;
        private object _literal;
        private int _line;

        public Token(TokenType type, string lexeme, object literal, int line)
        {
            _type = type;
            _lexeme = lexeme;
            _literal = literal;
            _line = line;
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
    }
}
