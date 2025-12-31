namespace TSQL.Tests
{
    public class ScannerTests
    {
        public static TheoryData<string, TokenType> SingleCharacterTokens()
        {
            return new TheoryData<string, TokenType>
            {
                { "(", TokenType.LEFT_PAREN },
                { ")", TokenType.RIGHT_PAREN },
                { ",", TokenType.COMMA },
                { ".", TokenType.DOT },
                { "-", TokenType.MINUS },
                { "+", TokenType.PLUS },
                { ";", TokenType.SEMICOLON },
                { "*", TokenType.STAR },
                { "=", TokenType.EQUAL },
                { "<", TokenType.LESS },
                { ">", TokenType.GREATER },
                { "/", TokenType.SLASH }
            };
        }

        public static TheoryData<string, TokenType> MultiCharacterOperators()
        {
            return new TheoryData<string, TokenType>
            {
                { "!=", TokenType.NOT_EQUAL },
                { "<>", TokenType.NOT_EQUAL },
                { "<=", TokenType.LESS_EQUAL },
                { ">=", TokenType.GREATER_EQUAL }
            };
        }

        public static TheoryData<string, TokenType, object> NumberTokens()
        {
            return new TheoryData<string, TokenType, object>
            {
                { "123", TokenType.WHOLE_NUMBER, 123 },
                { "0", TokenType.WHOLE_NUMBER, 0 },
                { "999999", TokenType.WHOLE_NUMBER, 999999 },
                { "19.99", TokenType.DECIMAL, 19.99 },
                { "0.5", TokenType.DECIMAL, 0.5 },
                { "100.001", TokenType.DECIMAL, 100.001 }
            };
        }

        public static TheoryData<string, string, string> StringTokens()
        {
            return new TheoryData<string, string, string>
            {
                { "'hello'", "'hello'", "hello" },
                { "'O''Reilly'", "'O''Reilly'", "O'Reilly" },
                { "''", "''", "" },
                { "'multiple words'", "'multiple words'", "multiple words" }
            };
        }

        public static TheoryData<string, string> IdentifierTokens()
        {
            return new TheoryData<string, string>
            {
                { "users", "users" },
                { "TABLE1", "TABLE1" },
                { "price", "price" },
                { "_test", "_test" },
                { "var123", "var123" }
            };
        }

        public static TheoryData<string, string, string> DelimitedIdentifierTokens()
        {
            return new TheoryData<string, string, string>
            {
                { "[users]", "[users]", "users" },
                { "[FROM]", "[FROM]", "FROM" },
                { "[User Name]", "[User Name]", "User Name" },
                { "[Column-1]", "[Column-1]", "Column-1" },
                { "\"users\"", "\"users\"", "users" },
                { "\"FROM\"", "\"FROM\"", "FROM" },
                { "\"User Name\"", "\"User Name\"", "User Name" },
                { "\"Column-1\"", "\"Column-1\"", "Column-1" },
                { "[[[users]]]", "[[[users]]]", "[[users]" },
                { "\"\"\"users\"\"\"", "\"\"\"users\"\"\"", "\"users\"" },
            };
        }

        [Theory]
        [MemberData(nameof(DelimitedIdentifierTokens))]
        public void ScanTokens_DelimitedIdentifier_RecognizedCorrectly(string input, string expectedLexeme, string expectedValue)
        {
            var scanner = new Scanner(input);
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, TokenType.IDENTIFIER, expectedLexeme, expectedValue)
                .HaveToken(1, TokenType.EOF, "");
        }

        [Theory]
        [MemberData(nameof(SingleCharacterTokens))]
        public void ScanTokens_SingleCharacterToken_ReturnsCorrectType(string input, TokenType expectedType)
        {
            var scanner = new Scanner(input);
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, expectedType, input)
                .HaveToken(1, TokenType.EOF, "");
        }

        [Theory]
        [MemberData(nameof(MultiCharacterOperators))]
        public void ScanTokens_MultiCharacterOperator_ReturnsCorrectType(string input, TokenType expectedType)
        {
            var scanner = new Scanner(input);
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, expectedType, input)
                .HaveToken(1, TokenType.EOF, "");
        }

        [Theory]
        [MemberData(nameof(NumberTokens))]
        public void ScanTokens_Number_ParsesCorrectly(string input, TokenType expectedType, object expectedLiteral)
        {
            var scanner = new Scanner(input);
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, expectedType, input, expectedLiteral)
                .HaveToken(1, TokenType.EOF, "");
        }

        [Theory]
        [MemberData(nameof(StringTokens))]
        public void ScanTokens_String_ParsesAndUnescapesCorrectly(string input, string expectedLexeme, string expectedLiteral)
        {
            var scanner = new Scanner(input);
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, TokenType.STRING, expectedLexeme, expectedLiteral)
                .HaveToken(1, TokenType.EOF, "");
        }


        [Theory]
        [MemberData(nameof(IdentifierTokens))]
        public void ScanTokens_Identifier_RecognizedCorrectly(string input, string expectedLexeme)
        {
            var scanner = new Scanner(input);
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, TokenType.IDENTIFIER, expectedLexeme)
                .HaveToken(1, TokenType.EOF, "");
        }

        [Fact]
        public void ScanTokens_Comment_IgnoresRestOfLine()
        {
            var scanner = new Scanner("SELECT -- this is a comment\nFROM");
            var tokens = scanner.ScanTokens();

            tokens.Should()
                .HaveToken(0, TokenType.SELECT, "SELECT")
                .HaveToken(1, TokenType.FROM, "FROM")
                .HaveToken(2, TokenType.EOF, "");
        }

        [Fact]
        public void ScanTokens_ComplexStatement_ParsesCorrectly()
        {
            var scanner = new Scanner("SELECT id, price FROM products WHERE price > 19.99");
            var tokens = scanner.ScanTokens();

            tokens.Should().MatchExpectedTokens(new[]
            {
                new ExpectedToken(TokenType.SELECT, "SELECT"),
                new ExpectedToken(TokenType.IDENTIFIER, "id"),
                new ExpectedToken(TokenType.COMMA, ","),
                new ExpectedToken(TokenType.IDENTIFIER, "price"),
                new ExpectedToken(TokenType.FROM, "FROM"),
                new ExpectedToken(TokenType.IDENTIFIER, "products"),
                new ExpectedToken(TokenType.WHERE, "WHERE"),
                new ExpectedToken(TokenType.IDENTIFIER, "price"),
                new ExpectedToken(TokenType.GREATER, ">"),
                new ExpectedToken(TokenType.DECIMAL, "19.99", 19.99),
                new ExpectedToken(TokenType.EOF, "")
            });
        }

        [Fact]
        public void ScanTokens_MultilineString_TracksLineNumbers()
        {
            var scanner = new Scanner("'line1\nline2'");
            var tokens = scanner.ScanTokens();

            Assert.Equal(2, tokens[0].Line);
        }
    }

    public record ExpectedToken(TokenType Type, string Lexeme, object? Literal = null);
}
