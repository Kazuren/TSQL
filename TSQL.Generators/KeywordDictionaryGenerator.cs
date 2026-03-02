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
        private const int DiscriminatorThreshold = 4;

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
        /// For larger groups, emits a two-level dispatch: first a switch on one
        /// carefully chosen character, then linear if-chains within each case.
        /// See <see cref="EmitDiscriminatorSwitch"/> for details.
        /// </summary>
        private static void EmitKeywordGroup(StringBuilder sb, List<string> keywords, string indent)
        {
            if (keywords.Count <= DiscriminatorThreshold)
            {
                EmitLinearChain(sb, keywords, indent);
            }
            else
            {
                EmitDiscriminatorSwitch(sb, keywords, indent);
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
        /// Emits a switch that narrows down candidates by looking at a single character
        /// before doing full string comparisons.
        ///
        /// Problem: the length-4 bucket has ~28 keywords (CASE, CAST, FROM, INTO, JOIN,
        /// NULL, OVER, THEN, WHEN, WITH, ...). When the scanner sees an identifier like
        /// "col1" in <c>SELECT col1 FROM Orders</c>, it must rule out all 28 before
        /// concluding it's not a keyword.
        ///
        /// Solution: pick the character position where the keywords differ the most
        /// (the "discriminator"), then switch on that one character. For example, if
        /// position 2 is chosen, the runtime reads slice[2] once and jumps straight to
        /// the small subset of keywords that share that character — typically 1-6 instead
        /// of 28. Each case then does a short linear if-chain over just that subset.
        ///
        /// Example for length-4 keywords with discriminator at position 0:
        /// <code>
        /// switch (slice.LowerAt(0))
        /// {
        ///     case 'c':
        ///         if (slice.EqualsIgnoreCaseUnchecked("cast")) { ... }
        ///         if (slice.EqualsIgnoreCaseUnchecked("case")) { ... }
        ///         break;
        ///     case 'n':
        ///         if (slice.EqualsIgnoreCaseUnchecked("null")) { ... }
        ///         break;
        ///     ...
        /// }
        /// </code>
        /// </summary>
        private static void EmitDiscriminatorSwitch(StringBuilder sb, List<string> keywords, string indent)
        {
            int bestPos = FindBestDiscriminator(keywords);

            // Group keywords by their lowercased char at the discriminating position
            var groups = new SortedDictionary<char, List<string>>();
            foreach (var keyword in keywords)
            {
                char key = char.ToLowerInvariant(keyword[bestPos]);
                if (!groups.ContainsKey(key))
                    groups[key] = new List<string>();
                groups[key].Add(keyword);
            }

            sb.AppendLine($"{indent}switch (slice.LowerAt({bestPos}))");
            sb.AppendLine($"{indent}{{");

            foreach (var group in groups)
            {
                sb.AppendLine($"{indent}    case '{group.Key}':");
                EmitLinearChain(sb, group.Value, indent + "        ");
                sb.AppendLine($"{indent}        break;");
            }

            sb.AppendLine($"{indent}}}");
        }

        /// <summary>
        /// Finds the character position with the most distinct lowercased characters
        /// across all keywords. This position best discriminates between keywords.
        /// </summary>
        private static int FindBestDiscriminator(List<string> keywords)
        {
            int length = keywords[0].Length;
            int bestPos = 0;
            int bestDistinct = 0;

            for (int pos = 0; pos < length; pos++)
            {
                var seen = new HashSet<char>();
                foreach (var kw in keywords)
                {
                    seen.Add(char.ToLowerInvariant(kw[pos]));
                }
                if (seen.Count > bestDistinct)
                {
                    bestDistinct = seen.Count;
                    bestPos = pos;
                }
            }

            return bestPos;
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