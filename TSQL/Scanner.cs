using System;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    public partial class Scanner
    {
        private readonly string _source;
        private readonly List<Token> _tokens = new List<Token>();
        private readonly List<Trivia> _pendingTrivia = new List<Trivia>();

        private int _start = 0;
        private int _current = 0;
        private int _line = 1;

        public Scanner(string source)
        {
            _source = source;
        }

        public List<Token> ScanTokens()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                ScanToken();
            }

            AddToken(TokenType.EOF, null, "");

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
                case '=':
                    AddToken(TokenType.EQUAL);
                    break;
                case '!':
                    if (Match('='))
                    {
                        AddToken(TokenType.NOT_EQUAL);
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
                }
                else if (Peek() == '\n')
                {
                    AdvanceLineNumber();
                }
            }

            if (IsAtEnd())
            {
                throw new Exception("Unterminated multi-line comment.");
            }

            // Consume the closing "*/" of the multi-line comment
            Consume(2);

            AddTrivia(new Comment(_source.Substring(_start, _current - _start)));
        }

        private void SingleLineComment()
        {
            while (Peek() != '\n' && !IsAtEnd())
            {
                Advance();
            }

            AddTrivia(new Comment(_source.Substring(_start, _current - _start)));
        }

        private void Whitespace()
        {
            char c = Peek();
            while (char.IsWhiteSpace(c))
            {
                // repeating check to see if current character is a new line to increment line counter
                if (c == '\n')
                {
                    AdvanceLineNumber();
                }

                Advance();
            }

            AddTrivia(new Whitespace(_source.Substring(_start, _current - _start)));
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

            string text = _source.Substring(_start, _current - _start);

            TokenType type;
            if (_keywords.TryGetValue(text, out TokenType value))
            {
                type = value;
            }
            else
            {
                type = TokenType.IDENTIFIER;
            }

            AddToken(type, text);
        }
        private void BracketDelimitedIdentifier()
        {
            StringBuilder builder = new StringBuilder();

            // consume characters until we hit a closing bracket
            while (!IsAtEnd())
            {
                if (Peek() == ']')
                {
                    // Check if this is an escaped bracket (two consecutive closing brackets)
                    if (PeekNext() == ']')
                    {
                        // consume both brackets
                        Consume(2);

                        // This is an escaped bracket, add a single bracket to the result
                        builder.Append(']');
                    }
                    else
                    {
                        // This is the actual closing bracket, we're done
                        break;
                    }
                }
                else
                {
                    char currentChar = Peek();
                    if (currentChar == '\n')
                    {
                        AdvanceLineNumber();
                    }
                    builder.Append(currentChar);
                    Advance();
                }
            }

            if (IsAtEnd())
            {
                throw new Exception("Unterminated delimited identifier.");
            }

            // Consume the closing delimiter
            Advance();

            AddToken(TokenType.IDENTIFIER, builder.ToString());
        }

        private void QuoteDelimitedIdentifier()
        {
            StringBuilder builder = new StringBuilder();

            // consume characters until we hit a closing quote
            while (!IsAtEnd())
            {
                if (Peek() == '"')
                {
                    // Check if this is an escaped quote (two consecutive double quotes)
                    if (PeekNext() == '"')
                    {
                        // consume both quotes
                        Consume(2);

                        // This is an escaped quote, add a single quote to the result
                        builder.Append('"');
                    }
                    else
                    {
                        // This is the actual closing quote, we're done
                        break;
                    }
                }
                else
                {
                    char currentChar = Peek();
                    if (currentChar == '\n')
                    {
                        AdvanceLineNumber();
                    }
                    builder.Append(currentChar);
                    Advance();
                }
            }

            if (IsAtEnd())
            {
                throw new Exception("Unterminated delimited identifier.");
            }

            // Consume the closing delimiter
            Advance();

            AddToken(TokenType.IDENTIFIER, builder.ToString());
        }

        private void String()
        {
            StringBuilder builder = new StringBuilder();

            // consume characters until we hit another ''' that ends the string
            while (!IsAtEnd())
            {
                if (Peek() == '\'')
                {
                    // Check if this is an escaped quote (two consecutive single quotes)
                    if (PeekNext() == '\'')
                    {
                        // consume both quotes
                        Consume(2);

                        // This is an escaped quote, add a single quote to the result
                        builder.Append('\'');
                    }
                    else
                    {
                        // This is the actual closing quote, we're done
                        break;
                    }
                }
                else
                {
                    char currentChar = Peek();
                    if (currentChar == '\n')
                    {
                        AdvanceLineNumber();
                    }
                    builder.Append(currentChar);
                    Advance();
                }
            }


            if (IsAtEnd())
            {
                throw new Exception("Unterminated string.");
            }

            // The closing '
            Advance();

            AddToken(TokenType.STRING, builder.ToString());
        }

        private void AdvanceLineNumber()
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
            string text = _source.Substring(_start, _current - _start);
            AddToken(type, literal, text);
        }

        private void AddToken(TokenType type, object literal, string lexeme)
        {
            Token token = new Token(type, lexeme, literal, _line);

            token.AddLeadingTrivia(_pendingTrivia);
            _pendingTrivia.Clear();

            _tokens.Add(token);
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

