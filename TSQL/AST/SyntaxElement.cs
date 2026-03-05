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

        internal Token LastToken()
        {
            Token last = null;
            foreach (Token t in DescendantTokens()) last = t;
            return last;
        }

        internal static Token LastTokenOf(ISyntaxElement element)
        {
            Token last = null;
            foreach (Token t in element.DescendantTokens()) last = t;
            return last;
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
        /// Builds a doubly-linked list of tokens in document order. Called once after parsing.
        /// </summary>
        internal static void BuildTokenChain(ISyntaxElement root)
        {
            Token prev = null;
            foreach (Token token in root.DescendantTokens())
            {
                if (prev != null)
                {
                    prev.NextToken = token;
                    token.PreviousToken = prev;
                }
                prev = token;
            }
        }

        /// <summary>
        /// Returns true if the character could be part of a word-like token (identifiers,
        /// keywords, variables, temp table names) that would merge with an adjacent
        /// word-like token when no whitespace separates them.
        /// </summary>
        private static bool IsWordChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9') || c == '_' || c == '@' || c == '#';
        }

        /// <summary>
        /// Adds a leading space to the right token if it would merge with the left token
        /// (both end/start with word characters and no trivia separates them).
        /// </summary>
        private static void EnsureSeparation(Token left, Token right)
        {
            if (left == null || right == null) return;
            if (right.LeadingTrivia.Count > 0) return;
            if (IsWordChar(left.LastChar) && IsWordChar(right.FirstChar))
            {
                right.AddLeadingTrivia(Whitespace.Space);
            }
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
        /// maintains the token linked list, and ensures adjacent tokens don't merge.
        /// This is the single source of truth for trivia-aware replacement.
        /// </summary>
        internal static T SetWithTrivia<T>(T oldValue, T newValue) where T : class, ISyntaxElement
        {
            if (oldValue != null && newValue != null)
            {
                TransferLeadingTrivia(oldValue, newValue);
                SpliceTokenChain(oldValue, newValue);
            }
            return newValue;
        }

        /// <summary>
        /// Replaces the old element's span in the token linked list with the new element's tokens.
        /// Builds internal prev/next links for the new tokens and splices them in place,
        /// then ensures adjacent tokens at the boundaries don't merge.
        /// </summary>
        private static void SpliceTokenChain(ISyntaxElement oldValue, ISyntaxElement newValue)
        {
            // Find the first and last tokens of the old element in a single pass
            Token oldFirst = null;
            Token oldLast = null;
            foreach (Token t in oldValue.DescendantTokens())
            {
                if (oldFirst == null)
                {
                    oldFirst = t;
                }
                oldLast = t;
            }

            // Only splice if the chain was built (PreviousToken/NextToken set)
            if (oldFirst == null || oldLast == null
                || (oldFirst.PreviousToken == null && oldLast.NextToken == null))
            {
                return;
            }

            Token before = oldFirst.PreviousToken;
            Token after = oldLast.NextToken;

            // Build internal chain for the new element's tokens
            Token newFirst = null;
            Token newLast = null;
            Token prev = null;
            foreach (Token t in newValue.DescendantTokens())
            {
                if (newFirst == null)
                {
                    newFirst = t;
                }
                if (prev != null)
                {
                    prev.NextToken = t;
                    t.PreviousToken = prev;
                }
                prev = t;
                newLast = t;
            }

            if (newFirst == null || newLast == null)
            {
                return;
            }

            // Splice into the chain
            newFirst.PreviousToken = before;
            if (before != null)
            {
                before.NextToken = newFirst;
            }

            newLast.NextToken = after;
            if (after != null)
            {
                after.PreviousToken = newLast;
            }

            // Fix merging at both boundaries
            EnsureSeparation(before, newFirst);
            EnsureSeparation(newLast, after);
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
