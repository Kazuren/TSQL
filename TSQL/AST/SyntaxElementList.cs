using System.Collections;
using System.Collections.Generic;

namespace TSQL
{
    /// <summary>
    /// A list of syntax nodes separated by tokens (like commas).
    /// Preserves the separator tokens to maintain trivia.
    /// </summary>
    public class SyntaxElementList<T> : SyntaxElement, IEnumerable<T> where T : ISyntaxElement
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

        public Token GetSeparator(int index)
        {
            if (index < _separators.Count)
            {
                return _separators[index];
            }

            return null;
        }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override IEnumerable<Token> DescendantTokens()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                // Yield all tokens from the item
                foreach (Token token in _items[i].DescendantTokens())
                {
                    yield return token;
                }

                // Yield the separator (comma) if present
                if (i < _separators.Count && _separators[i] != null)
                {
                    yield return _separators[i];
                }
            }
        }
    }
}
