using System.Diagnostics;
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


            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Stmt stmt = parser.Parse();
            stopwatch.Stop();


            SourceCodeVisitor sourceCodeVisitor = new SourceCodeVisitor();
            string sql = stmt.Accept(sourceCodeVisitor);

            Assert.Equal(source, sql);
        }
    }
}
