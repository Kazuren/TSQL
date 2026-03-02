using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSQL.Generators
{
    [Generator]
    public class KeywordDictionaryGenerator : ISourceGenerator
    {
        private const int TrieThreshold = 4;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            var tokenTypeEnum = receiver.TokenTypeEnum;
            if (tokenTypeEnum == null)
                return;

            var keywords = new List<string>();
            bool inKeywordsSection = false;

            foreach (var member in tokenTypeEnum.Members)
            {
                // Check if we've reached the keywords section
                var trivia = member.GetLeadingTrivia().ToString();
                if (trivia.Contains("Keywords"))
                {
                    inKeywordsSection = true;
                }

                if (inKeywordsSection && member is EnumMemberDeclarationSyntax enumMember)
                {
                    var name = enumMember.Identifier.Text;
                    keywords.Add(name);
                }
            }

            var source = GenerateKeywordsDictionary(keywords);
            context.AddSource("Scanner.Keywords.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private string GenerateKeywordsDictionary(List<string> keywords)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine("namespace TSQL");
            sb.AppendLine("{");
            sb.AppendLine("    partial class Scanner");
            sb.AppendLine("    {");
            sb.AppendLine("        private static readonly Dictionary<string, TokenType> _keywords = new Dictionary<string, TokenType>(StringComparer.OrdinalIgnoreCase)");
            sb.AppendLine("        {");

            // Sort for deterministic output
            var sortedKeywords = keywords.OrderBy(k => k).ToList();
            foreach (var keyword in sortedKeywords)
            {
                var lowercaseKeyword = keyword.ToLowerInvariant();
                sb.AppendLine($"            {{\"{lowercaseKeyword}\", TokenType.{keyword}}},");
            }

            sb.AppendLine("        };");
            sb.AppendLine();

            // Generate a TryGetKeyword method that works with StringSlice
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Attempts to match a StringSlice to a keyword without allocating a string.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        private static bool TryGetKeyword(StringSlice slice, out TokenType tokenType)");
            sb.AppendLine("        {");
            sb.AppendLine("            switch (slice.Length)");
            sb.AppendLine("            {");

            // Group keywords by length for efficient lookup
            var keywordsByLength = new SortedDictionary<int, List<string>>();
            foreach (var keyword in keywords)
            {
                if (!keywordsByLength.ContainsKey(keyword.Length))
                    keywordsByLength[keyword.Length] = new List<string>();
                keywordsByLength[keyword.Length].Add(keyword);
            }

            foreach (var kvp in keywordsByLength)
            {
                sb.AppendLine($"                case {kvp.Key}:");
                // Sort keywords within each bucket for deterministic output
                var sorted = kvp.Value.OrderBy(k => k.ToLowerInvariant()).ToList();
                EmitKeywordGroup(sb, sorted, "                    ");
                sb.AppendLine("                    break;");
            }

            sb.AppendLine("            }");
            sb.AppendLine("            tokenType = default;");
            sb.AppendLine("            return false;");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Emits matching code for a group of same-length keywords.
        /// For small groups (≤4), emits a linear if-chain.
        /// For larger groups, emits a trie-based dispatch where each character
        /// position is checked exactly once via nested switch/if chains.
        /// </summary>
        private static void EmitKeywordGroup(StringBuilder sb, List<string> keywords, string indent)
        {
            if (keywords.Count <= TrieThreshold)
            {
                EmitLinearChain(sb, keywords, indent);
            }
            else
            {
                EmitTrie(sb, keywords, indent);
            }
        }

        private static void EmitLinearChain(StringBuilder sb, List<string> keywords, string indent)
        {
            foreach (var keyword in keywords)
            {
                var lowercaseKeyword = keyword.ToLowerInvariant();
                sb.AppendLine($"{indent}if (slice.EqualsIgnoreCaseUnchecked(\"{lowercaseKeyword}\")) {{ tokenType = TokenType.{keyword}; return true; }}");
            }
        }

        /// <summary>
        /// Emits a trie-based dispatch for a group of same-length keywords.
        /// Each character position is checked exactly once via nested switch/if chains.
        /// Non-keywords exit at the first character that doesn't match any keyword path.
        /// </summary>
        private static void EmitTrie(StringBuilder sb, List<string> keywords, string indent)
        {
            var root = BuildTrie(keywords);
            EmitTrieNode(sb, root, 0, indent);
        }

        /// <returns>true if every code path through this node ends with a return statement.</returns>
        private static bool EmitTrieNode(StringBuilder sb, TrieNode node, int depth, string indent)
        {
            if (node.Keyword != null)
            {
                sb.AppendLine($"{indent}tokenType = TokenType.{node.Keyword}; return true;");
                return true;
            }

            if (node.Children.Count == 0)
            {
                return false;
            }

            if (node.Children.Count == 1)
            {
                var kvp = node.Children.First();
                sb.AppendLine($"{indent}if (slice.LowerAt({depth}) == '{kvp.Key}')");
                sb.AppendLine($"{indent}{{");
                EmitTrieNode(sb, kvp.Value, depth + 1, indent + "    ");
                sb.AppendLine($"{indent}}}");
                return false;
            }
            else
            {
                sb.AppendLine($"{indent}switch (slice.LowerAt({depth}))");
                sb.AppendLine($"{indent}{{");

                foreach (var kvp in node.Children.OrderBy(c => c.Key))
                {
                    sb.AppendLine($"{indent}    case '{kvp.Key}':");
                    bool returns = EmitTrieNode(sb, kvp.Value, depth + 1, indent + "        ");
                    if (!returns)
                    {
                        sb.AppendLine($"{indent}        break;");
                    }
                }

                sb.AppendLine($"{indent}}}");
                return false;
            }
        }

        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children = new Dictionary<char, TrieNode>();
            public string Keyword; // non-null at leaf — the TokenType enum name
        }

        private static TrieNode BuildTrie(List<string> keywords)
        {
            var root = new TrieNode();
            foreach (var keyword in keywords)
            {
                var node = root;
                foreach (char c in keyword.ToLowerInvariant())
                {
                    if (!node.Children.TryGetValue(c, out var child))
                    {
                        child = new TrieNode();
                        node.Children[c] = child;
                    }
                    node = child;
                }
                node.Keyword = keyword;
            }
            return root;
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public EnumDeclarationSyntax TokenTypeEnum { get; private set; }

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is EnumDeclarationSyntax enumDeclaration && enumDeclaration.Identifier.Text == "TokenType")
                {
                    TokenTypeEnum = enumDeclaration;
                }
            }
        }
    }
}