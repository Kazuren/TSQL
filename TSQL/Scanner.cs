using System;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    internal partial class Scanner
    {
        private readonly string _source;
        private readonly List<SourceToken> _tokens;
        private readonly List<Trivia> _pendingTrivia;

        private SourceToken _previousToken;

        private int _start = 0;
        private int _current = 0;
        private int _line = 1;

        public Scanner(string source)
        {
            _source = source;
            // Pre-allocate capacity: estimate ~1 token per 4 characters, minimum 16
            int estimatedTokens = Math.Max(16, source.Length / 4);
            _tokens = new List<SourceToken>(estimatedTokens);
            // Trivia list typically holds 1-2 items at a time
            _pendingTrivia = new List<Trivia>(2);
        }

        public List<SourceToken> ScanTokens()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                ScanToken();
            }

            AddTokenWithString(TokenType.EOF, null, "");

            return _tokens;
        }

        private void ScanToken()
        {
            char c = Advance();
            switch (c)
            {
                case '(':
                    AddToken(TokenType.LEFT_PAREN);
                    break;
                case ')':
                    AddToken(TokenType.RIGHT_PAREN);
                    break;
                case ',':
                    AddToken(TokenType.COMMA);
                    break;
                case '.':
                    AddToken(TokenType.DOT);
                    break;
                case '-':
                    if (Match('-'))
                    {
                        // we matched a second '-' so the current line is a comment
                        SingleLineComment();
                    }
                    else
                    {
                        AddToken(TokenType.MINUS);
                    }
                    break;
                case '+':
                    AddToken(TokenType.PLUS);
                    break;
                case ';':
                    AddToken(TokenType.SEMICOLON);
                    break;
                case '*':
                    AddToken(TokenType.STAR);
                    break;
                case '%':
                    AddToken(TokenType.MODULO);
                    break;
                case '=':
                    AddToken(TokenType.EQUAL);
                    break;
                case '!':
                    if (Match('='))
                    {
                        AddToken(TokenType.NOT_EQUAL);
                    }
                    else if (Match('<'))
                    {
                        AddToken(TokenType.NOT_LESS);
                    }
                    else if (Match('>'))
                    {
                        AddToken(TokenType.NOT_GREATER);
                    }
                    break;
                case '<':
                    if (Match('>'))
                    {
                        AddToken(TokenType.NOT_EQUAL);
                    }
                    else if (Match('='))
                    {
                        AddToken(TokenType.LESS_EQUAL);
                    }
                    else
                    {
                        AddToken(TokenType.LESS);
                    }
                    break;
                case '>':
                    AddToken(Match('=') ? TokenType.GREATER_EQUAL : TokenType.GREATER);
                    break;
                case '/':
                    if (Match('*'))
                    {
                        MultiLineComment();
                    }
                    else
                    {
                        AddToken(TokenType.SLASH);
                    }
                    break;
                case '\'':
                    String();
                    break;
                case '[':
                    BracketDelimitedIdentifier();
                    break;
                case '"':
                    QuoteDelimitedIdentifier();
                    break;
                case '&':
                    AddToken(TokenType.BITWISE_AND);
                    break;
                case '|':
                    AddToken(TokenType.BITWISE_OR);
                    break;
                case '^':
                    AddToken(TokenType.BITWISE_XOR);
                    break;
                case '~':
                    AddToken(TokenType.BITWISE_NOT);
                    break;
                case '@':
                    Variable();
                    break;
                case 'N':
                case 'n':
                    if (Match('\''))
                    {
                        String();
                    }
                    else
                    {
                        Identifier();
                    }
                    break;
                default:
                    if (Char.IsWhiteSpace(c))
                    {
                        Whitespace();
                    }
                    else if (IsDigit(c))
                    {
                        Number();
                    }
                    else if (IsAlpha(c))
                    {
                        Identifier();
                    }
                    else
                    {
                        throw new Exception($"Unexpected character. {c}");
                    }
                    break;
            }
        }

        private void Variable()
        {
            while (IsAlphaNumeric(Peek()))
            {
                Advance();
            }

            StringSlice slice = new StringSlice(_source, _start, _current - _start);
            AddToken(TokenType.VARIABLE, null, slice);
        }

        private void MultiLineComment()
        {
            int commentDepth = 1;
            while (!IsAtEnd())
            {
                if (Peek() == '/' && PeekNext() == '*')
                {
                    Consume(2);
                    ++commentDepth;
                }
                else if (Peek() == '*' && PeekNext() == '/')
                {
                    --commentDepth;
                    if (commentDepth == 0)
                    {
                        break;
                    }
                    else
                    {
                        Consume(2);
                    }
                }
                else if (Peek() == '\n')
                {
                    IncrementLineNumber();
                    Advance();
                }
                else
                {
                    Advance();
                }
            }

            if (IsAtEnd())
            {
                throw new Exception("Unterminated multi-line comment.");
            }

            // Consume the closing "*/" of the multi-line comment
            Consume(2);

            AddTrivia(new Comment(_source, _start, _current - _start));
        }

        private void SingleLineComment()
        {
            while (Peek() != '\n' && !IsAtEnd())
            {
                Advance();
            }

            AddTrivia(new Comment(_source, _start, _current - _start));
        }

        private void Whitespace()
        {
            char c = Peek();
            while (char.IsWhiteSpace(c))
            {
                // repeating check to see if current character is a new line to increment line counter
                if (c == '\n')
                {
                    IncrementLineNumber();
                }

                Advance();
                c = Peek();
            }

            AddTrivia(new Whitespace(_source, _start, _current - _start));
        }

        private void Number()
        {
            while (IsDigit(Peek()))
            {
                Advance();
            }

            if (Peek() == '.' && IsDigit(PeekNext()))
            {
                Advance();

                while (IsDigit(Peek()))
                {
                    Advance();
                }
                AddToken(TokenType.DECIMAL, Double.Parse(_source.Substring(_start, _current - _start)));
            }
            else
            {
                AddToken(TokenType.WHOLE_NUMBER, int.Parse(_source.Substring(_start, _current - _start)));
            }
        }

        private void Identifier()
        {
            while (IsAlphaNumeric(Peek()))
            {
                Advance();
            }

            // Use StringSlice for keyword lookup and token creation - no string allocation
            StringSlice slice = new StringSlice(_source, _start, _current - _start);

            TokenType type;
            if (TryGetKeyword(slice, out TokenType value))
            {
                type = value;
            }
            else
            {
                type = TokenType.IDENTIFIER;
            }

            // Only identifiers need the literal string; keywords (including NULL) get null
            object literal;
            if (type == TokenType.IDENTIFIER)
            {
                literal = slice.ToString();
            }
            else
            {
                literal = null;
            }
            // Pass the slice directly - string allocation is deferred until Lexeme is accessed
            AddToken(type, literal, slice);
        }
        private string ConsumeDelimitedContent(char closingDelimiter, string unterminatedError)
        {
            StringBuilder builder = new StringBuilder();

            while (!IsAtEnd())
            {
                if (Peek() == closingDelimiter)
                {
                    if (PeekNext() == closingDelimiter)
                    {
                        // Escaped delimiter (doubled) — consume both, emit one
                        Consume(2);
                        builder.Append(closingDelimiter);
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    char currentChar = Peek();
                    if (currentChar == '\n')
                    {
                        IncrementLineNumber();
                    }
                    builder.Append(currentChar);
                    Advance();
                }
            }

            if (IsAtEnd())
            {
                throw new Exception(unterminatedError);
            }

            // Consume the closing delimiter
            Advance();

            return builder.ToString();
        }

        private void BracketDelimitedIdentifier()
        {
            string content = ConsumeDelimitedContent(']', "Unterminated delimited identifier.");
            AddToken(TokenType.IDENTIFIER, content);
        }

        private void QuoteDelimitedIdentifier()
        {
            string content = ConsumeDelimitedContent('"', "Unterminated delimited identifier.");
            AddToken(TokenType.IDENTIFIER, content);
        }

        private void String()
        {
            string value = ConsumeDelimitedContent('\'', "Unterminated string.");
            AddToken(TokenType.STRING, value);
        }

        private void IncrementLineNumber()
        {
            ++_line;
        }

        // Character lookup
        private char Peek()
        {
            if (IsAtEnd()) return '\0';
            return _source[_current];
        }

        private char PeekNext()
        {
            if (_current + 1 >= _source.Length)
            {
                return '\0';
            }

            return _source[_current + 1];
        }

        private bool Match(char expected)
        {
            if (IsAtEnd()) { return false; }
            if (_source[_current] != expected) { return false; }
            Consume();
            return true;
        }

        private bool IsAtEnd()
        {
            return _current >= _source.Length;
        }

        private char Advance()
        {
            return _source[_current++];
        }

        private void Consume()
        {
            ++_current;
        }

        private void Consume(int amount)
        {
            System.Diagnostics.Debug.Assert(amount > 0, "Amount consumed should be greater than 0!");
            _current += amount;
        }

        private void AddToken(TokenType type)
        {
            AddToken(type, null);
        }

        private void AddTrivia(Trivia trivia)
        {
            _pendingTrivia.Add(trivia);
        }

        private void AddToken(TokenType type, object literal)
        {
            StringSlice slice = new StringSlice(_source, _start, _current - _start);
            AddTokenCore(type, literal, slice);
        }

        private void AddToken(TokenType type, object literal, StringSlice lexemeSlice)
        {
            AddTokenCore(type, literal, lexemeSlice);
        }

        /// <summary>
        /// Overload for tokens where the lexeme is built dynamically (e.g., escaped strings).
        /// Wraps the string in a StringSlice so it won't allocate again when Lexeme is accessed.
        /// </summary>
        private void AddTokenWithString(TokenType type, object literal, string lexeme)
        {
            AddTokenCore(type, literal, StringSlice.FromString(lexeme));
        }

        private void AddTokenCore(TokenType type, object literal, StringSlice lexemeSlice)
        {
            SourceToken token = new SourceToken(type, lexemeSlice, literal, _line);

            if (_previousToken != null)
            {
                _previousToken.AddTrailingTrivia(_pendingTrivia);
            }

            token.AddLeadingTrivia(_pendingTrivia);
            _pendingTrivia.Clear();

            _tokens.Add(token);
            _previousToken = token;
        }

        // Characterization
        private bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
        private bool IsAlpha(char c)
        {
            return (c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                c == '_';
        }
        private bool IsAlphaNumeric(char c)
        {
            return IsAlpha(c) || IsDigit(c);
        }
    }
}

