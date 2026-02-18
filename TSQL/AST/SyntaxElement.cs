using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    public interface ISyntaxElement
    {
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
            foreach (Token token in DescendantTokens())
            {
                token.AppendTo(sb);
            }
            return sb.ToString();
        }

        internal Token FirstToken()
        {
            foreach (Token t in DescendantTokens()) return t;
            return null;
        }

        internal static Token FirstTokenOf(ISyntaxElement element)
        {
            foreach (Token t in element.DescendantTokens()) return t;
            return null;
        }

        /// <summary>
        /// Attaches a single-line comment (-- comment) before this node.
        /// The comment appears on the line before the node in the output.
        /// </summary>
        public void AddLeadingComment(string comment)
        {
            Token token = FirstToken();
            if (token == null) return;
            token.AddLeadingTrivia(new Comment("-- " + comment));
            token.AddLeadingTrivia(new Whitespace("\n"));
        }

        /// <summary>
        /// Attaches an inline block comment (/* comment */) before this node.
        /// The comment appears immediately before the node in the output.
        /// </summary>
        public void AddLeadingBlockComment(string comment)
        {
            Token token = FirstToken();
            if (token == null) return;
            token.AddLeadingTrivia(new Comment("/* " + comment + " */"));
            token.AddLeadingTrivia(Whitespace.Space);
        }

        public override string ToString()
        {
            System.Type type = GetType();
            return $"[{type.BaseType.Name}.{type.Name}]: {ToSource()}";
        }

        /// <summary>
        /// Copies the leading trivia from one node's first token to another's,
        /// replacing any existing leading trivia on the target.
        /// Used by property setters to preserve whitespace when replacing child nodes.
        /// </summary>
        internal static void TransferLeadingTrivia(ISyntaxElement from, ISyntaxElement to)
        {
            Token fromToken = FirstTokenOf(from);
            Token toToken = FirstTokenOf(to);

            // Same-token guard: when a property setter is called with the same node
            // (e.g. pred.Left = TryReplace(pred.Left) where TryReplace returns the original),
            // ClearLeadingTrivia would destroy the trivia before AddLeadingTrivia can copy it.
            if (fromToken == null || toToken == null || ReferenceEquals(fromToken, toToken)) return;

            toToken.ClearLeadingTrivia();
            toToken.AddLeadingTrivia(fromToken.LeadingTrivia);
        }

        /// <summary>
        /// Transfers leading trivia from the old value to the new value when both are non-null,
        /// then returns the new value. This is the single source of truth for trivia-aware replacement.
        /// </summary>
        internal static T SetWithTrivia<T>(T oldValue, T newValue) where T : class, ISyntaxElement
        {
            if (oldValue != null && newValue != null) TransferLeadingTrivia(oldValue, newValue);
            return newValue;
        }

        /// <summary>
        /// Convenience overload that assigns directly to a backing field.
        /// </summary>
        internal static void SetWithTrivia<T>(ref T field, T value) where T : class, ISyntaxElement
        {
            field = SetWithTrivia(field, value);
        }

    }
}
