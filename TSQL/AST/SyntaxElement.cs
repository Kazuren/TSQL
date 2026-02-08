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

        /// <summary>
        /// Attaches a single-line comment (-- comment) before this node.
        /// The comment appears on the line before the node in the output.
        /// </summary>
        public void AddLeadingComment(string comment)
        {
            foreach (Token token in DescendantTokens())
            {
                token.AddLeadingTrivia(new Comment("-- " + comment));
                token.AddLeadingTrivia(new Whitespace("\n"));
                return;
            }
        }

        /// <summary>
        /// Attaches an inline block comment (/* comment */) before this node.
        /// The comment appears immediately before the node in the output.
        /// </summary>
        public void AddLeadingBlockComment(string comment)
        {
            foreach (Token token in DescendantTokens())
            {
                token.AddLeadingTrivia(new Comment("/* " + comment + " */"));
                token.AddLeadingTrivia(new Whitespace(" "));
                return;
            }
        }

        public SyntaxElement Parent { get; internal set; }
        public SyntaxElement SiblingLeft { get; internal set; }
        public SyntaxElement SiblingRight { get; internal set; }
    }
}
