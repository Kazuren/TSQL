using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests.Visitors
{
    public class SourceCodeVisitorTests
    {
        [Fact]
        public void SourceCodeVisitor_RegeneratesSourceAccurately()
        {
            string source = "SELECT * FROM T";
            Scanner scanner = new Scanner(source);
            Parser parser = new Parser(scanner.ScanTokens());

            Stmt stmt = parser.Parse();

            SourceCodeVisitor sourceCodeVisitor = new SourceCodeVisitor();
            string sql = stmt.Accept(sourceCodeVisitor);

            Assert.Equal(source, sql);
        }


        [Fact]
        public void SourceCodeVisitor_RegeneratesSourceAccurately_Alias()
        {
            string source = "SELECT a, b AS bAlias FROM T";
            Scanner scanner = new Scanner(source);
            Parser parser = new Parser(scanner.ScanTokens());

            Stmt stmt = parser.Parse();

            SourceCodeVisitor sourceCodeVisitor = new SourceCodeVisitor();
            string sql = stmt.Accept(sourceCodeVisitor);

            Assert.Equal(source, sql);
        }

        [Fact]
        public void SourceCodeVisitor_RegeneratesSourceAccurately_AliasAlternate()
        {
            string source = "SELECT a, bAlias = b FROM T";
            Scanner scanner = new Scanner(source);
            Parser parser = new Parser(scanner.ScanTokens());

            Stmt stmt = parser.Parse();

            SourceCodeVisitor sourceCodeVisitor = new SourceCodeVisitor();
            string sql = stmt.Accept(sourceCodeVisitor);

            Assert.Equal(source, sql);
        }
    }
}
