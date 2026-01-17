using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace TSQL.Benchmarks
{
    /// <summary>
    /// Benchmarks for the TSQL Scanner (lexer/tokenizer)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Scanner")]
    public class ScannerBenchmarks
    {
        private const string SimpleQuery = "SELECT Id, Name FROM Users";
        private const string MediumQuery = "SELECT u.Id, u.Name, u.Email, o.OrderId, o.Total FROM Users u INNER JOIN Orders o ON u.Id = o.UserId WHERE u.Active = 1";
        private const string ComplexQuery = @"
            SELECT 
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                COUNT(o.OrderId) AS OrderCount,
                SUM(o.Total) AS TotalSpent,
                AVG(o.Total) AS AvgOrderValue,
                ROW_NUMBER() OVER (PARTITION BY u.Region ORDER BY SUM(o.Total) DESC) AS RegionalRank
            FROM Users u
            LEFT JOIN Orders o ON u.Id = o.UserId
            WHERE u.CreatedDate >= '2024-01-01'
                AND u.Status IN ('active', 'premium', 'vip')
                AND (o.Total > 100 OR o.IsPromotional = 1)
            GROUP BY u.Id, u.FirstName, u.LastName, u.Email, u.Region
            HAVING COUNT(o.OrderId) > 5
            ORDER BY TotalSpent DESC, u.LastName ASC";

        [Benchmark(Description = "Scan simple query (~25 chars)")]
        public List<SourceToken> ScanSimpleQuery()
        {
            var scanner = new Scanner(SimpleQuery);
            return scanner.ScanTokens();
        }

        [Benchmark(Description = "Scan medium query (~130 chars)")]
        public List<SourceToken> ScanMediumQuery()
        {
            var scanner = new Scanner(MediumQuery);
            return scanner.ScanTokens();
        }

        [Benchmark(Description = "Scan complex query (~850 chars)")]
        public List<SourceToken> ScanComplexQuery()
        {
            var scanner = new Scanner(ComplexQuery);
            return scanner.ScanTokens();
        }
    }

    /// <summary>
    /// Benchmarks for basic parsing operations
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Basic")]
    public class ParserBasicBenchmarks
    {
        private const string SingleColumn = "SELECT Id FROM T";
        private const string MultipleColumns = "SELECT Id, Name, Email, CreatedDate, UpdatedDate FROM Users";
        private const string WithAlias = "SELECT u.Id, u.Name AS UserName, Total = o.Amount FROM Users u";
        private const string WithExpressions = "SELECT a + b * c, (x - y) / z, -value FROM T";
        private const string WithSubquery = "SELECT Id, (SELECT COUNT(*) FROM Orders WHERE UserId = u.Id) AS OrderCount FROM Users u";

        [Benchmark(Description = "Parse single column")]
        public Stmt ParseSingleColumn()
        {
            var scanner = new Scanner(SingleColumn);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Parse 5 columns")]
        public Stmt ParseMultipleColumns()
        {
            var scanner = new Scanner(MultipleColumns);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Parse with aliases")]
        public Stmt ParseWithAliases()
        {
            var scanner = new Scanner(WithAlias);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Parse with expressions")]
        public Stmt ParseWithExpressions()
        {
            var scanner = new Scanner(WithExpressions);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Parse with scalar subquery")]
        public Stmt ParseWithSubquery()
        {
            var scanner = new Scanner(WithSubquery);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for window function parsing
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "WindowFunctions")]
    public class WindowFunctionBenchmarks
    {
        private const string SimpleRowNumber = "SELECT ROW_NUMBER() OVER (ORDER BY Id) FROM T";
        private const string RankWithPartition = "SELECT RANK() OVER (PARTITION BY Department ORDER BY Salary DESC) FROM Employees";
        private const string AggregateWithOver = "SELECT SUM(Amount) OVER (PARTITION BY CustomerId ORDER BY OrderDate) FROM Orders";
        private const string WithFrameClause = "SELECT SUM(Amount) OVER (ORDER BY OrderDate ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM Orders";
        private const string ComplexFrame = "SELECT AVG(Price) OVER (PARTITION BY Category ORDER BY Date ROWS BETWEEN 3 PRECEDING AND 3 FOLLOWING) FROM Products";
        private const string MultipleWindowFunctions = @"
            SELECT 
                ROW_NUMBER() OVER (ORDER BY Id) AS RowNum,
                RANK() OVER (PARTITION BY Dept ORDER BY Salary DESC) AS DeptRank,
                DENSE_RANK() OVER (ORDER BY Score DESC) AS ScoreRank,
                NTILE(4) OVER (ORDER BY Revenue DESC) AS Quartile,
                SUM(Sales) OVER (PARTITION BY Region) AS RegionTotal,
                AVG(Sales) OVER (ORDER BY Month ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS MovingAvg
            FROM SalesData";

        [Benchmark(Description = "ROW_NUMBER() simple")]
        public Stmt ParseSimpleRowNumber()
        {
            var scanner = new Scanner(SimpleRowNumber);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "RANK() with PARTITION BY")]
        public Stmt ParseRankWithPartition()
        {
            var scanner = new Scanner(RankWithPartition);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "SUM() OVER with partition")]
        public Stmt ParseAggregateWithOver()
        {
            var scanner = new Scanner(AggregateWithOver);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Window with frame clause")]
        public Stmt ParseWithFrameClause()
        {
            var scanner = new Scanner(WithFrameClause);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Window with complex frame")]
        public Stmt ParseComplexFrame()
        {
            var scanner = new Scanner(ComplexFrame);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multiple window functions")]
        public Stmt ParseMultipleWindowFunctions()
        {
            var scanner = new Scanner(MultipleWindowFunctions);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for function call parsing
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Functions")]
    public class FunctionBenchmarks
    {
        private const string NoArgs = "SELECT GETDATE() FROM T";
        private const string SingleArg = "SELECT UPPER(Name) FROM T";
        private const string MultipleArgs = "SELECT COALESCE(a, b, c, d, e) FROM T";
        private const string NestedFunctions = "SELECT UPPER(TRIM(SUBSTRING(Name, 1, 10))) FROM T";
        private const string SchemaQualified = "SELECT dbo.MyFunction(a, b), sys.fn_listextendedproperty(NULL, NULL, NULL, NULL, NULL, NULL, NULL) FROM T";

        [Benchmark(Description = "Function with no args")]
        public Stmt ParseNoArgs()
        {
            var scanner = new Scanner(NoArgs);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Function with single arg")]
        public Stmt ParseSingleArg()
        {
            var scanner = new Scanner(SingleArg);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "COALESCE with 5 args")]
        public Stmt ParseMultipleArgs()
        {
            var scanner = new Scanner(MultipleArgs);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Nested function calls")]
        public Stmt ParseNestedFunctions()
        {
            var scanner = new Scanner(NestedFunctions);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Schema-qualified functions")]
        public Stmt ParseSchemaQualified()
        {
            var scanner = new Scanner(SchemaQualified);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for round-trip operations (parse + ToSource)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("RoundTrip")]
    public class RoundTripBenchmarks
    {
        private const string SimpleQuery = "SELECT Id, Name, Email FROM Users";
        private const string QueryWithWindowFunction = "SELECT ROW_NUMBER() OVER (PARTITION BY Dept ORDER BY Salary DESC) AS Rank FROM Employees";
        private const string QueryWithFrameClause = "SELECT SUM(Amount) OVER (ORDER BY Date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal FROM Orders";

        [Benchmark(Description = "Round-trip simple query")]
        public string RoundTripSimple()
        {
            var scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            var stmt = parser.Parse();
            return stmt.ToSource();
        }

        [Benchmark(Description = "Round-trip window function")]
        public string RoundTripWindowFunction()
        {
            var scanner = new Scanner(QueryWithWindowFunction);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            var stmt = parser.Parse();
            return stmt.ToSource();
        }

        [Benchmark(Description = "Round-trip with frame clause")]
        public string RoundTripFrameClause()
        {
            var scanner = new Scanner(QueryWithFrameClause);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            var stmt = parser.Parse();
            return stmt.ToSource();
        }
    }

    /// <summary>
    /// Benchmarks comparing pre-tokenized vs full pipeline performance
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Pipeline")]
    public class PipelineBenchmarks
    {
        private const string Query = "SELECT ROW_NUMBER() OVER (PARTITION BY Region ORDER BY Sales DESC) AS Rank, Name, Sales FROM SalesReps";
        private List<SourceToken> _preTokenized;

        [GlobalSetup]
        public void Setup()
        {
            var scanner = new Scanner(Query);
            _preTokenized = scanner.ScanTokens();
        }

        [Benchmark(Description = "Full pipeline (scan + parse)", Baseline = true)]
        public Stmt FullPipeline()
        {
            var scanner = new Scanner(Query);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Parse only (pre-tokenized)")]
        public Stmt ParseOnly()
        {
            var parser = new Parser(_preTokenized);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for identifier parsing with various qualification levels
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Identifiers")]
    public class IdentifierBenchmarks
    {
        private const string SimpleColumn = "SELECT column FROM T";
        private const string TableQualified = "SELECT table.column FROM T";
        private const string SchemaQualified = "SELECT schema.table.column FROM T";
        private const string FullyQualified = "SELECT database.schema.table.column FROM T";
        private const string MixedIdentifiers = "SELECT a, t.b, s.t.c, d.s.t.e FROM T";

        [Benchmark(Description = "Simple column")]
        public Stmt ParseSimpleColumn()
        {
            var scanner = new Scanner(SimpleColumn);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Table.column")]
        public Stmt ParseTableQualified()
        {
            var scanner = new Scanner(TableQualified);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Schema.table.column")]
        public Stmt ParseSchemaQualified()
        {
            var scanner = new Scanner(SchemaQualified);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Database.schema.table.column")]
        public Stmt ParseFullyQualified()
        {
            var scanner = new Scanner(FullyQualified);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Mixed qualification levels")]
        public Stmt ParseMixedIdentifiers()
        {
            var scanner = new Scanner(MixedIdentifiers);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Throughput benchmarks - parsing many queries
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Throughput")]
    public class ThroughputBenchmarks
    {
        private static readonly string[] Queries = new[]
        {
            "SELECT Id FROM Users",
            "SELECT Name, Email FROM Customers WHERE Active = 1",
            "SELECT ROW_NUMBER() OVER (ORDER BY Id) FROM Orders",
            "SELECT a + b * c FROM T",
            "SELECT COALESCE(a, b, c) FROM T",
            "SELECT u.Id, u.Name FROM Users u",
            "SELECT SUM(Amount) OVER (PARTITION BY CustomerId) FROM Orders",
            "SELECT * FROM Products",
            "SELECT TOP 10 Name FROM Categories",
            "SELECT GETDATE() FROM T"
        };

        [Benchmark(Description = "Parse 10 different queries")]
        public Stmt[] Parse10Queries()
        {
            var results = new Stmt[Queries.Length];
            for (int i = 0; i < Queries.Length; i++)
            {
                var scanner = new Scanner(Queries[i]);
                var tokens = scanner.ScanTokens();
                var parser = new Parser(tokens);
                results[i] = parser.Parse();
            }
            return results;
        }

        [Benchmark(Description = "Parse same query 100 times")]
        public Stmt[] ParseSameQuery100Times()
        {
            const string query = "SELECT ROW_NUMBER() OVER (ORDER BY Id) AS RowNum, Name FROM Users";
            var results = new Stmt[100];
            for (int i = 0; i < 100; i++)
            {
                var scanner = new Scanner(query);
                var tokens = scanner.ScanTokens();
                var parser = new Parser(tokens);
                results[i] = parser.Parse();
            }
            return results;
        }
    }
}
