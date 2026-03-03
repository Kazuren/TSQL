using System;
using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    /// <summary>
    /// A lightweight struct representing a slice of a string without allocation.
    /// Similar to ReadOnlySpan&lt;char&gt; but compatible with .NET Standard 2.0.
    /// </summary>
    internal readonly struct StringSlice : IEquatable<StringSlice>, IEquatable<string>
    {
        private readonly string _source;
        private readonly int _start;
        private readonly int _length;

        internal int Start { get => _start; }
        internal int End { get => _start + _length - 1; }
        internal string Source { get => _source; }

        public StringSlice(string source, int start, int length)
        {
            _source = source;
            _start = start;
            _length = length;
        }

        /// <summary>
        /// Creates a StringSlice that wraps an entire string.
        /// Useful when you already have an allocated string but need to pass it as a slice.
        /// </summary>
        public static StringSlice FromString(string value)
        {
            if (value == null) return default;
            return new StringSlice(value, 0, value.Length);
        }

        public int Length => _length;

        public char this[int index]
        {
            get
            {
                if (index < 0 || index >= _length)
                    throw new IndexOutOfRangeException();
                return _source[_start + index];
            }
        }

        /// <summary>
        /// Appends this slice's characters directly to a StringBuilder without allocating an intermediate string.
        /// </summary>
        internal void AppendTo(StringBuilder sb)
        {
            if (_source != null)
            {
                sb.Append(_source, _start, _length);
            }
        }

        /// <summary>
        /// Creates a string from this slice. Only allocates if the slice is a subset of the source.
        /// If the slice represents the entire source string, returns the source directly.
        /// </summary>
        public override string ToString()
        {
            if (_source == null) return string.Empty;
            // If we represent the entire string, just return it (no allocation)
            if (_start == 0 && _length == _source.Length)
                return _source;
            return _source.Substring(_start, _length);
        }

        public bool Equals(StringSlice other)
        {
            if (_length != other._length) return false;
            for (int i = 0; i < _length; i++)
            {
                if (_source[_start + i] != other._source[other._start + i])
                    return false;
            }
            return true;
        }

        public bool Equals(string other)
        {
            if (other == null) return _source == null;
            if (_length != other.Length) return false;
            for (int i = 0; i < _length; i++)
            {
                if (_source[_start + i] != other[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Case-insensitive comparison with a string (assumes the other string is lowercase).
        /// </summary>
        public bool EqualsIgnoreCase(string other)
        {
            if (other == null) return _source == null;
            if (_length != other.Length) return false;
            for (int i = 0; i < _length; i++)
            {
                char c = _source[_start + i];
                // Convert to lowercase for comparison
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                if (c != other[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Case-insensitive comparison that skips null and length checks.
        /// Only safe when the caller has already guaranteed non-null and matching length
        /// (e.g. the generated TryGetKeyword method).
        /// </summary>
        internal bool EqualsIgnoreCaseUnchecked(string other)
        {
            for (int i = 0; i < _length; i++)
            {
                char c = _source[_start + i];
                if (c >= 'A' && c <= 'Z')
                {
                    c = (char)(c + 32);
                }
                if (c != other[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Returns the lowercased character at the given index within the slice.
        /// Only converts ASCII uppercase (A-Z) to lowercase.
        /// </summary>
        internal char LowerAt(int index)
        {
            char c = _source[_start + index];
            if (c >= 'A' && c <= 'Z')
            {
                c = (char)(c + 32);
            }
            return c;
        }

        public override bool Equals(object obj)
        {
            if (obj is StringSlice slice) return Equals(slice);
            if (obj is string str) return Equals(str);
            return false;
        }

        public override int GetHashCode()
        {
            // Simple hash that matches string's behavior for comparison
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < _length; i++)
                {
                    hash = hash * 31 + _source[_start + i];
                }
                return hash;
            }
        }

        public static bool operator ==(StringSlice left, StringSlice right) => left.Equals(right);
        public static bool operator !=(StringSlice left, StringSlice right) => !left.Equals(right);
        public static bool operator ==(StringSlice left, string right) => left.Equals(right);
        public static bool operator !=(StringSlice left, string right) => !left.Equals(right);
    }

    /// <summary>
    /// A comparer that allows using StringSlice as a key to look up string values in a dictionary.
    /// </summary>
    internal sealed class StringSliceComparer : IEqualityComparer<string>
    {
        private StringSlice _currentSlice;

        public void SetSlice(StringSlice slice)
        {
            _currentSlice = slice;
        }

        public bool Equals(string x, string y)
        {
            // When one is null, use slice comparison
            if (x == null) return _currentSlice.Equals(y);
            if (y == null) return _currentSlice.Equals(x);
            return x == y;
        }

        public int GetHashCode(string obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }
}
