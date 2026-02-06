using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TSQL
{
    public interface ISyntaxElement
    {
        SyntaxElement Parent { get; }
        SyntaxElement SiblingLeft { get; }
        SyntaxElement SiblingRight { get; }

        IEnumerable<Token> DescendantTokens();
        string ToSource();
    }

    public abstract class SyntaxElement : ISyntaxElement
    {
        /// <summary>
        /// Returns all tokens under this node in document order.
        /// This is the single source of truth for source text.
        /// </summary>
        public abstract IEnumerable<Token> DescendantTokens();

        public string ToSource()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                foreach (Token token in DescendantTokens())
                {
                    sb.Append(token.ToSource());
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                return sb.ToString();
            }
        }

        public SyntaxElement Parent { get; internal set; }
        public SyntaxElement SiblingLeft { get; internal set; }
        public SyntaxElement SiblingRight { get; internal set; }
    }
}
