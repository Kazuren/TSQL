namespace TSQL.Tests
{
    public static class TokenAssertionExtensions
    {
        public static TokenAssertion Should(this List<Token> tokens)
        {
            return new TokenAssertion(tokens);
        }
    }

    public class TokenAssertion
    {
        private readonly List<Token> _tokens;

        public TokenAssertion(List<Token> tokens)
        {
            _tokens = tokens;
        }

        public TokenAssertion MatchExpectedTokens(ExpectedToken[] expected)
        {
            Assert.Equal(expected.Length, _tokens.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                HaveToken(i, expected[i].Type, expected[i].Lexeme, expected[i].Literal);
            }

            return this;
        }

        public TokenAssertion HaveToken(int index, TokenType type, string lexeme, object? literal = null)
        {
            Assert.True(index < _tokens.Count, $"Expected token at index {index}, but only {_tokens.Count} tokens were scanned.");

            var token = _tokens[index];

            Assert.Equal(type, token.Type);
            Assert.Equal(lexeme, token.Lexeme);

            if (literal != null)
            {
                Assert.Equal(literal, token.Literal);
            }

            return this;
        }
    }
}