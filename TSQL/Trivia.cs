namespace TSQL
{
    public interface Trivia
    {
        string Content { get; }
    }

    public class Whitespace : Trivia
    {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;
        private string _contentCache;

        public Whitespace(string source, int start, int length)
        {
            _source = source;
            _start = start;
            _length = length;
        }

        public Whitespace(string content)
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
    }

    public class Comment : Trivia
    {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;
        private string _contentCache;

        public Comment(string source, int start, int length)
        {
            _source = source;
            _start = start;
            _length = length;
        }

        public Comment(string content)
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
    }
}
