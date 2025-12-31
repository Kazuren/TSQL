namespace TSQL
{
    public readonly struct Trivia
    {
        public string Content { get; }
        internal TriviaType TriviaType { get; }

        public Trivia(string content)
        {
            Content = content;
            TriviaType = TriviaType.Whitespace;
        }

        internal Trivia(string content, TriviaType type)
        {
            Content = content;
            TriviaType = type;
        }
    }

    public enum TriviaType
    {
        Whitespace,
        Comment
    }
}
