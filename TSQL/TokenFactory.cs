namespace TSQL
{
    public static class TokenFactory
    {
        public static ConcreteToken Alias()
        {
            return new ConcreteToken(TokenType.IDENTIFIER, "todo", null);
        }
    }
}
