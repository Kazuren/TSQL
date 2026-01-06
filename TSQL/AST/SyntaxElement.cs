using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    public abstract class SyntaxElement
    {
        /// <summary>
        /// Returns all tokens under this node in document order.
        /// This is the single source of truth for source text.
        /// </summary>
        public virtual IEnumerable<Token> DescendantTokens()
        {
            // TODO: make abstract when done with coding
            throw new System.NotImplementedException();
        }

        public string ToSource()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Token token in DescendantTokens())
            {
                sb.Append(token.ToSource());
            }
            return sb.ToString();
        }

        public SyntaxElement Parent { get; internal set; }
        public SyntaxElement SiblingLeft { get; internal set; }
        public SyntaxElement SiblingRight { get; internal set; }
    }
}
