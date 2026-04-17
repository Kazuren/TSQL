using System.Text;

namespace TSQL
{
    internal interface Trivia
    {
        string Content { get; }

        /// <summary>
        /// Appends this trivia's content directly to a StringBuilder, bypassing the Content property
        /// to avoid allocating an intermediate string.
        /// </summary>
        void AppendTo(StringBuilder sb);
    }

    internal abstract class TriviaBase : Trivia
    {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;
        private string _contentCache;

        protected TriviaBase(string source, int start, int length)
        {
            _source = source;
            _start = start;
            _length = length;
        }

        protected TriviaBase(string content)
        {
            _source = content;
            _start = 0;
            _length = content.Length;
            _contentCache = content;
        }

        public string Content
        {
            get
            {
                if (_contentCache == null)
                {
                    _contentCache = _source.Substring(_start, _length);
                }
                return _contentCache;
            }
        }

        public void AppendTo(StringBuilder sb)
        {
            sb.Append(_source, _start, _length);
        }
    }

    internal sealed class Whitespace : TriviaBase
    {
        internal static readonly Whitespace Space = new Whitespace(" ");
        internal static readonly Whitespace Newline = new Whitespace("\n");

        public Whitespace(string source, int start, int length) : base(source, start, length) { }
        public Whitespace(string content) : base(content) { }
    }

    internal abstract class Comment : TriviaBase
    {
        protected Comment(string source, int start, int length) : base(source, start, length) { }
        protected Comment(string content) : base(content) { }
    }

    /// <summary>
    /// A single-line comment (<c>-- ...</c>). Terminates at end-of-line, so a newline
    /// must follow it in normalized output to prevent the next token being swallowed.
    /// </summary>
    internal sealed class LineComment : Comment
    {
        public LineComment(string source, int start, int length) : base(source, start, length) { }
        public LineComment(string content) : base(content) { }
    }

    /// <summary>
    /// A block comment (<c>/* ... */</c>). Self-delimiting, so it can appear inline
    /// between tokens without affecting surrounding whitespace.
    /// </summary>
    internal sealed class BlockComment : Comment
    {
        public BlockComment(string source, int start, int length) : base(source, start, length) { }
        public BlockComment(string content) : base(content) { }
    }
}
