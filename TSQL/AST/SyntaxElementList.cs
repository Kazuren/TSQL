using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    /// <summary>
    /// A list of syntax nodes separated by tokens (like commas).
    /// Preserves the separator tokens to maintain trivia.
    /// </summary>
    public class SyntaxElementList<T> : SyntaxElement, IEnumerable<T> where T : SyntaxElement
    {
        public int Count => _items.Count;
        public T this[int index] => _items[index];

        private readonly List<T> _items = new List<T>();
        private readonly List<Token> _separators = new List<Token>();

        /// <summary>
        /// Add an item with its trailing separator token (if any).
        /// </summary>
        internal void Add(T item, Token separator = null)
        {
            _items.Add(item);
            if (separator != null)
            {
                _separators.Add(separator);
            }
        }

        public void AddItem(T item)
        {
            _items.Add(item);
        }

        public Token GetSeparator(int index)
        {
            if (index < _separators.Count)
            {
                return _separators[index];
            }

            return null;
        }

        public override string ToSource()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < _items.Count; i++)
            {
                sb.Append(_items[i].ToSource());

                if (i < _items.Count - 1)
                {
                    if (i < _separators.Count && _separators[i] != null)
                    {
                        sb.Append(_separators[i].ToSource());
                    }
                    else
                    {
                        sb.Append(", ");
                    }
                }
            }

            return sb.ToString();
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
