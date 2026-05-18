using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TSQL
{
    internal partial class Scanner
    {
        // Pre-boxed whole numbers 0-127. SQL commonly uses small integer literals (WHERE x = 1,
        // TOP 10, NTILE(4), etc.). Caching the boxed values avoids a heap allocation per literal.
        // Every WHOLE_NUMBER literal carries a long (a SQL int or bigint), so the cache boxes
        // longs to keep the token's literal type consistent regardless of magnitude.
        private static readonly object[] BoxedWholeNumbers = InitBoxedWholeNumbers();

        private static object[] InitBoxedWholeNumbers()
        {
            object[] cache = new object[128];
            for (int i = 0; i < cache.Length; i++)
            {
                cache[i] = (long)i;
            }
            return cache;
        }

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
                    else
                    {
                        AddToken(TokenType.BANG);
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
                    if (Match('='))
                    {
                        AddToken(TokenType.GREATER_EQUAL);
                    }
                    else
                    {
                        AddToken(TokenType.GREATER);
                    }
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
                case '\n':
                    IncrementLineNumber();
                    Whitespace();
                    break;
                case ' ':
                case '\t':
                case '\r':
                case '\u00A0': // NO-BREAK SPACE
                case '\u2002': // EN SPACE
                case '\u2003': // EM SPACE
                case '\u2009': // THIN SPACE
                case '\u202F': // NARROW NO-BREAK SPACE
                case '\u3000': // IDEOGRAPHIC SPACE
                    Whitespace();
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
                        throw new ParseError($"Unexpected character: {DescribeChar(c)}", _line, ColumnAtStart(), 1, _source);
                    }
                    break;
            }
        }

        private void Variable()
        {
            if (Peek() == '@')
            {
                Advance();
            }

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
                throw new ParseError("Unterminated multi-line comment.", _line, ColumnAtStart(), "/*".Length, _source);
            }

            // Consume the closing "*/" of the multi-line comment
            Consume(2);

            AddTrivia(new BlockComment(_source, _start, _current - _start));
        }

        private void SingleLineComment()
        {
            while (Peek() != '\n' && !IsAtEnd())
            {
                Advance();
            }

            // Don't include trailing \r in the comment — it belongs to the \r\n line ending.
            if (_current > _start && _source[_current - 1] == '\r')
            {
                _current--;
            }

            AddTrivia(new LineComment(_source, _start, _current - _start));
        }

        private void Whitespace()
        {
            char c = Peek();
            while (IsWhiteSpace(c))
            {
                if (c == '\n')
                {
                    IncrementLineNumber();
                }

                Advance();
                c = Peek();
            }

            // Single spaces are the most common whitespace in SQL. Reuse the static
            // singleton to avoid allocating a new Whitespace object (~40 bytes) per token gap.
            int length = _current - _start;
            if (length == 1 && _source[_start] == ' ')
            {
                AddTrivia(TSQL.Whitespace.Space);
            }
            else
            {
                AddTrivia(new Whitespace(_source, _start, length));
            }
        }

        private void Number()
        {
            // Phase 1: Consume integer and optional fractional digits
            while (IsDigit(Peek()))
            {
                Advance();
            }

            bool hasDecimalPoint = false;
            if (Peek() == '.' && IsDigit(PeekNext()))
            {
                hasDecimalPoint = true;
                Advance();

                while (IsDigit(Peek()))
                {
                    Advance();
                }
            }

            // Phase 2: Check for exponent (E/e followed by optional sign and digits).
            // Only consume if a valid exponent pattern is confirmed by lookahead.
            bool hasExponent = false;
            char peek = Peek();
            if (peek == 'E' || peek == 'e')
            {
                char afterE = PeekNext();
                if (IsDigit(afterE))
                {
                    hasExponent = true;
                    Advance(); // consume E/e
                    while (IsDigit(Peek()))
                    {
                        Advance();
                    }
                }
                else if ((afterE == '+' || afterE == '-') && IsDigit(PeekAt(2)))
                {
                    hasExponent = true;
                    Advance(); // consume E/e
                    Advance(); // consume +/-
                    while (IsDigit(Peek()))
                    {
                        Advance();
                    }
                }
            }

            // Phase 3: Create token
            if (hasDecimalPoint || hasExponent)
            {
                string literal = _source.Substring(_start, _current - _start);
                try
                {
                    AddToken(TokenType.DECIMAL, Double.Parse(literal, CultureInfo.InvariantCulture));
                }
                catch (OverflowException ex)
                {
                    throw new ParseError($"Numeric literal too large: {literal}", _line, ColumnAtStart(), literal.Length, _source, ex);
                }
            }
            else
            {
                // Parse directly from source characters to avoid allocating a Substring.
                // The lexeme is already captured by StringSlice. A whole-number literal may
                // be a SQL int or bigint, so the value is held as a long; ParseNumberFromSource
                // throws OverflowException only past long range.
                int length = _current - _start;
                long value;
                try
                {
                    value = ParseNumberFromSource(_start, length);
                }
                catch (OverflowException ex)
                {
                    string literal = _source.Substring(_start, length);
                    throw new ParseError($"Numeric literal too large: {literal}", _line, ColumnAtStart(), length, _source, ex);
                }
                object boxed;
                if (value >= 0 && value < BoxedWholeNumbers.Length)
                {
                    boxed = BoxedWholeNumbers[(int)value];
                }
                else
                {
                    boxed = value;
                }
                AddToken(TokenType.WHOLE_NUMBER, boxed);
            }
        }

        /// <summary>
        /// Parses a whole number directly from the source string into a long, avoiding the
        /// Substring allocation that long.Parse() would require. Throws OverflowException
        /// when the literal exceeds long range.
        /// </summary>
        private long ParseNumberFromSource(int start, int length)
        {
            long result = 0;
            for (int i = start; i < start + length; i++)
            {
                result = checked(result * 10 + (_source[i] - '0'));
            }
            return result;
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
                throw new ParseError(unterminatedError, _line, ColumnAtStart(), 1, _source);
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

        private int ColumnAtStart()
        {
            if (_start == 0)
            {
                return 0;
            }

            int lastNewline = _source.LastIndexOf('\n', _start - 1);
            if (lastNewline != -1)
            {
                return _start - lastNewline - 1;
            }
            else
            {
                return _start;
            }
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

        private char PeekAt(int offset)
        {
            int index = _current + offset;
            if ((uint)index >= (uint)_source.Length)
            {
                return '\0';
            }

            return _source[index];
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

        // Character classification lookup table — replaces range-check methods with
        // a single bounds check + array access per call (same principle as .NET 8's SearchValues<char>).
        private const byte DIGIT = 1;
        private const byte ALPHA = 2;
        private static readonly byte[] CharKind = InitCharKind();

        private static byte[] InitCharKind()
        {
            byte[] table = new byte[128];
            for (char c = '0'; c <= '9'; c++) table[c] = DIGIT;
            for (char c = 'a'; c <= 'z'; c++) table[c] = ALPHA;
            for (char c = 'A'; c <= 'Z'; c++) table[c] = ALPHA;
            table['_'] = ALPHA;
            table['#'] = ALPHA;
            table['@'] = ALPHA;
            return table;
        }

        // (uint)c < 128 lets the JIT elide the separate array bounds check.
        private static bool IsDigit(char c) { return (uint)c < 128 && (CharKind[c] & DIGIT) != 0; }
        private static bool IsAlpha(char c) { return (uint)c < 128 && (CharKind[c] & ALPHA) != 0; }
        private static bool IsAlphaNumeric(char c) { return (uint)c < 128 && CharKind[c] != 0; }

        /// <summary>
        /// Inline ASCII-only whitespace check. Avoids char.IsWhiteSpace which checks the full
        /// Unicode whitespace category — T-SQL only uses ASCII whitespace characters.
        /// </summary>
        private static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\r'
                || c == '\u00A0' || c == '\u2002' || c == '\u2003'
                || c == '\u2009' || c == '\u202F' || c == '\u3000';
        }

        /// <summary>
        /// Human-readable names for Unicode characters that commonly appear in copy-pasted SQL
        /// but are not valid T-SQL. Used in error messages to help users identify the problem character.
        /// </summary>
        private static readonly Dictionary<char, string> UnicodeCharNames = new Dictionary<char, string>
        {
            // Zero-width
            { '\u200B', "ZERO WIDTH SPACE" },
            { '\u200C', "ZERO WIDTH NON-JOINER" },
            { '\u200D', "ZERO WIDTH JOINER" },
            { '\u2060', "WORD JOINER" },
            { '\uFEFF', "BYTE ORDER MARK" },
            // Smart quotes
            { '\u2018', "LEFT SINGLE QUOTATION MARK" },
            { '\u2019', "RIGHT SINGLE QUOTATION MARK" },
            { '\u201C', "LEFT DOUBLE QUOTATION MARK" },
            { '\u201D', "RIGHT DOUBLE QUOTATION MARK" },
            // Dashes
            { '\u2013', "EN DASH" },
            { '\u2014', "EM DASH" },
            { '\u2212', "MINUS SIGN" },
            // Other
            { '\u00AD', "SOFT HYPHEN" },
            { '\u2011', "NON-BREAKING HYPHEN" },
            { '\u2026', "HORIZONTAL ELLIPSIS" },
            { '\uFFFD', "REPLACEMENT CHARACTER" },
        };

        private static string DescribeChar(char c)
        {
            string codePoint = $"U+{(int)c:X4}";
            string charDisplay = char.IsControl(c) ? "" : $"'{c}'";

            if (UnicodeCharNames.TryGetValue(c, out string name))
            {
                return $"{charDisplay} {codePoint} {name}";
            }
            else
            {
                return $"{charDisplay} {codePoint} ({CharUnicodeInfo.GetUnicodeCategory(c)})";
            }
        }
    }
}

