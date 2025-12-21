using System;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    public partial class Scanner
    {
        private readonly string _source;
        private readonly List<Token> _tokens = new List<Token>();

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

            _tokens.Add(new Token(TokenType.EOF, "", null, _line));
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
                    AddToken(TokenType.MINUS);
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
                    if (Match('/'))
                    {
                        // we matched a second '/' so the current line is a comment
                        while (Peek() != '\n' && !IsAtEnd())
                        {
                            Advance();
                        }
                    }
                    else
                    {
                        AddToken(TokenType.SLASH);
                    }
                    break;
                case ' ':
                case '\r':
                case '\t':
                    // ignore whitespace
                    break;
                case '\n':
                    ++_line;
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
                    if (IsDigit(c))
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

            AddToken(type);
        }

        //private void DelimitedIdentifier(char closingDelimiter)
        //{
        //    StringBuilder builder = new StringBuilder();

        //    // consume characters until we hit a closing bracket
        //    while (!IsAtEnd() && Peek() != closingDelimiter)
        //    {
        //        char currentChar = Peek();
        //        if (currentChar == '\n')
        //        {
        //            _line++;
        //        }
        //        builder.Append(currentChar);
        //        Advance();
        //    }

        //    if (IsAtEnd())
        //    {
        //        throw new Exception("Unterminated delimited identifier.");
        //    }

        //    // Consume the closing delimiter
        //    Advance();

        //    // Trim the delimiters
        //    string value = _source.Substring(_start + 1, (_current - 1) - (_start + 1));
        //    // Store as IDENTIFIER or DELIMITED_IDENTIFIER token
        //    AddToken(TokenType.DELIMITED_IDENTIFIER, value);
        //}


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
                        _current += 2;

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
                        _line++;
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

            // Store as DELIMITED_IDENTIFIER token
            AddToken(TokenType.DELIMITED_IDENTIFIER, builder.ToString());
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
                        _current += 2;

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
                        _line++;
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

            // Store as DELIMITED_IDENTIFIER token
            AddToken(TokenType.DELIMITED_IDENTIFIER, builder.ToString());
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
                        _current += 2;

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
                        _line++;
                    }
                    builder.Append(currentChar);
                    Advance();
                }
            }


            if (IsAtEnd())
            {
                //Lox.Error(_line, "Unterminated string.");
                return;
            }

            // The closing '
            Advance();

            AddToken(TokenType.STRING, builder.ToString());
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

            _current++;
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

        private void AddToken(TokenType type)
        {
            AddToken(type, null);
        }

        private void AddToken(TokenType type, object literal)
        {
            string text = _source.Substring(_start, _current - _start);
            _tokens.Add(new Token(type, text, literal, _line));
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

