namespace TSQL
{
    public interface Trivia
    {
        string Content { get; }
    }

    public readonly struct Whitespace : Trivia
    {
        public string Content { get; }
        public Whitespace(string content)
        {
            System.Diagnostics.Debug.Assert(!string.IsNullOrEmpty(content), "Content should not be null or empty.");
            System.Diagnostics.Debug.Assert(string.IsNullOrWhiteSpace(content), "Content should only contain whitespace.");
            Content = content;
        }
    }
    public readonly struct Comment : Trivia
    {
        public string Content { get; }
        public Comment(string content)
        {
            System.Diagnostics.Debug.Assert(
                (content.StartsWith("/*") && content.EndsWith("*/")) ||
                (content.StartsWith("--") && !content.Contains("\n")),
                "Content should be a comment"
            );

            Content = content;
        }
    }
}
