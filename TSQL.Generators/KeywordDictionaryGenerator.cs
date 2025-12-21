using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;

namespace TSQL.Generators
{
    [Generator]
    public class KeywordDictionaryGenerator : ISourceGenerator
    {
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

            foreach (var keyword in keywords)
            {
                var lowercaseKeyword = keyword.ToLowerInvariant();
                sb.AppendLine($"            {{\"{lowercaseKeyword}\", TokenType.{keyword}}},");
            }

            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
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