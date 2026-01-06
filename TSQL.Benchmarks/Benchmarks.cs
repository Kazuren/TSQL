using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace TSQL.Benchmarks
{
    // For more information on the VS BenchmarkDotNet Diagnosers see https://learn.microsoft.com/visualstudio/profiling/profiling-with-benchmark-dotnet
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    public class Benchmarks
    {
        [Benchmark]
        public Stmt ParseSelectWithColumns()
        {
            var scanner = new Scanner("SELECT Id, Name, Email FROM Users");
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }
}
