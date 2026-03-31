using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    /// <summary>
    /// Read-only view of a syntax element list. Exposes inspection methods
    /// but no mutation operations (Insert, Prepend, Append).
    /// </summary>
    public interface IReadOnlySyntaxElementList<out T> : IEnumerable<T>
    {
        int Count { get; }
        T this[int index] { get; }
        bool Any<TResult>() where TResult : class;
        IEnumerable<TResult> OfType<TResult>() where TResult : class;
    }

    /// <summary>
    /// A list of syntax nodes separated by tokens (like commas).
    /// Preserves the separator tokens to maintain trivia.
    /// </summary>
    public class SyntaxElementList<T> : SyntaxElement, IReadOnlySyntaxElementList<T>, IEnumerable<T> where T : class, ISyntaxElement
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
        /// Inserts an item at the specified position, automatically inserting a comma separator.
        /// Out-of-range indices are clamped to the valid range.
        /// </summary>
        public void Insert(int index, T item)
        {
            if (_items == null)
            {
                _items = new List<T>();
            }

            index = Math.Max(0, Math.Min(index, _items.Count));

            if (_items.Count == 0)
            {
                _items.Add(item);
                return;
            }

            _items.Insert(index, item);

            if (_separators == null)
            {
                _separators = new List<Token>();
            }

            int sepIndex = Math.Min(index, _separators.Count);
            _separators.Insert(sepIndex, ConcreteToken.Comma);
        }

        /// <summary>
        /// Parses a SQL source fragment and inserts the resulting item at the specified position.
        /// Out-of-range indices are clamped to the valid range.
        /// </summary>
        /// <exception cref="ParseError">Thrown when the source is not valid SQL.</exception>
        public void Insert(int index, string source)
        {
            if (typeof(T) == typeof(SelectItem))
            {
                SelectItem item = Parser.CreateParser(source).ParseSelectItem();

                // Parsed fragments start at position 0 so their first token has no
                // leading whitespace. Add one to prevent merging with the preceding
                // token (e.g. SELECT keyword) when inserted into an existing list.
                SyntaxElement element = (SyntaxElement)(object)item;
                Token first = element.FirstToken();
                if (first != null && first.LeadingTrivia.Count == 0)
                {
                    first.AddLeadingTrivia(Whitespace.Space);
                }

                Insert(index, (T)(object)item);
            }
            else
            {
                throw new NotSupportedException("String parsing is not supported for " + typeof(T).Name);
            }
        }

        /// <summary>
        /// Inserts an item at the beginning of the list.
        /// </summary>
        public void Prepend(T item) => Insert(0, item);

        /// <summary>
        /// Parses a SQL source fragment and inserts the resulting item at the beginning of the list.
        /// </summary>
        /// <exception cref="ParseError">Thrown when the source is not valid SQL.</exception>
        public void Prepend(string source) => Insert(0, source);

        /// <summary>
        /// Adds an item to the end of the list, automatically inserting a comma separator if the list is non-empty.
        /// </summary>
        public void Append(T item) => Insert(Count, item);

        /// <summary>
        /// Parses a SQL source fragment and inserts the resulting item at the end of the list.
        /// </summary>
        /// <exception cref="ParseError">Thrown when the source is not valid SQL.</exception>
        public void Append(string source) => Insert(Count, source);

        /// <summary>
        /// Removes all items and separators from the list.
        /// </summary>
        public void Clear()
        {
            _items?.Clear();
            _separators?.Clear();
        }

        /// <summary>
        /// Inserts an item at the specified position with a specific separator token.
        /// If separator is null, no separator is added (used for the first item in a list).
        /// Out-of-range indices are clamped to the valid range.
        /// </summary>
        internal void Insert(int index, T item, Token separator)
        {
            if (_items == null)
            {
                _items = new List<T>();
            }

            index = Math.Max(0, Math.Min(index, _items.Count));

            _items.Insert(index, item);

            if (separator != null)
            {
                if (_separators == null)
                {
                    _separators = new List<Token>();
                }

                int sepIndex = Math.Min(index, _separators.Count);
                _separators.Insert(sepIndex, separator);
            }
        }

        /// <summary>
        /// Appends an item to the end of the list with a specific separator token.
        /// </summary>
        internal void Append(T item, Token separator) => Insert(Count, item, separator);

        internal Token GetSeparator(int index)
        {
            if (_separators != null && index < _separators.Count)
            {
                return _separators[index];
            }

            return null;
        }

        /// <summary>
        /// Returns true if any item in the list is of the specified type.
        /// </summary>
        public bool Any<TResult>() where TResult : class
        {
            if (_items == null) return false;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] is TResult)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns all items in the list that are of the specified type.
        /// </summary>
        public IEnumerable<TResult> OfType<TResult>() where TResult : class
        {
            if (_items != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i] is TResult result)
                    {
                        yield return result;
                    }
                }
            }
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

        // T is constrained to ISyntaxElement (an interface) because SelectItem and Alias
        // are interfaces, not classes — so the constraint can't be T : SyntaxElement.
        // Every ISyntaxElement implementation is a SyntaxElement subclass, so this cast is safe.
        internal override IEnumerable<Token> DescendantTokens()
        {
            if (_items == null)
            {
                yield break;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                // Yield all tokens from the item
                SyntaxElement element = (SyntaxElement)(object)_items[i];
                foreach (Token token in element.DescendantTokens())
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

        internal override void WriteTo(StringBuilder sb)
        {
            if (_items == null)
            {
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                ((SyntaxElement)(object)_items[i]).WriteTo(sb);

                if (_separators != null && i < _separators.Count && _separators[i] != null)
                {
                    _separators[i].AppendTo(sb);
                }
            }
        }
    }
}
