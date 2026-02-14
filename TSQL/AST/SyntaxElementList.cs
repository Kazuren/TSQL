using System;
using System.Collections;
using System.Collections.Generic;

namespace TSQL
{
    /// <summary>
    /// A list of syntax nodes separated by tokens (like commas).
    /// Preserves the separator tokens to maintain trivia.
    /// </summary>
    public class SyntaxElementList<T> : SyntaxElement, IEnumerable<T> where T : class, ISyntaxElement
    {
        public int Count => _items != null ? _items.Count : 0;
        public T this[int index]
        {
            get { return _items[index]; }
            set => _items[index] = SetWithTrivia(_items[index], value);
        }

        private List<T> _items;
        private List<Token> _separators;

        /// <summary>
        /// Add an item with its trailing separator token (if any).
        /// </summary>
        internal void Add(T item, Token separator = null)
        {
            if (_items == null)
            {
                _items = new List<T>();
            }
            _items.Add(item);
            if (separator != null)
            {
                if (_separators == null)
                {
                    _separators = new List<Token>();
                }
                _separators.Add(separator);
            }
        }

        public Token GetSeparator(int index)
        {
            if (_separators != null && index < _separators.Count)
            {
                return _separators[index];
            }

            return null;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_items == null)
            {
                return ((IEnumerable<T>)Array.Empty<T>()).GetEnumerator();
            }
            return _items.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override IEnumerable<Token> DescendantTokens()
        {
            if (_items == null)
            {
                yield break;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                // Yield all tokens from the item
                foreach (Token token in _items[i].DescendantTokens())
                {
                    yield return token;
                }

                // Yield the separator (comma) if present
                if (_separators != null && i < _separators.Count && _separators[i] != null)
                {
                    yield return _separators[i];
                }
            }
        }
    }
}
