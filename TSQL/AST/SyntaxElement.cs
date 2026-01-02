namespace TSQL
{
    public abstract class SyntaxElement
    {
        //public IReadOnlyList<Token> Tokens { get => _tokens.AsReadOnly(); }

        //protected List<Token> _tokens = new List<Token>();

        //public string ToSource()
        //{
        //    StringBuilder sb = new StringBuilder();

        //    foreach (Token token in Tokens)
        //    {
        //        sb.Append(token.ToSource());
        //    }

        //    return sb.ToString();
        //}

        public abstract string ToSource();
    }
}
