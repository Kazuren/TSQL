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
        private const string SimpleColumn = "SELECT col FROM T";
        private const string TableQualified = "SELECT t.col FROM T";
        private const string SchemaQualified = "SELECT s.t.col FROM T";
        private const string FullyQualified = "SELECT d.s.t.col FROM T";
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

    /// <summary>
    /// Benchmarks for WHERE clause predicate parsing
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Predicates")]
    public class PredicateBenchmarks
    {
        private const string SimpleComparison = "SELECT Id FROM T WHERE Active = 1";
        private const string MultiplePredicate = "SELECT Id FROM T WHERE Active = 1 AND Age > 18 AND Name LIKE 'J%' AND Status <> 'deleted'";
        private const string InList = "SELECT Id FROM T WHERE Status IN ('active', 'pending', 'review', 'approved', 'shipped')";
        private const string BetweenPredicate = "SELECT Id FROM T WHERE CreatedDate BETWEEN '2024-01-01' AND '2024-12-31' AND Amount BETWEEN 100 AND 500";
        private const string ExistsSubquery = "SELECT Id FROM T WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = T.Id AND o.Total > 100)";
        private const string ComplexBoolean = "SELECT Id FROM T WHERE (A = 1 OR B = 2) AND (C > 3 OR (D < 4 AND E = 5)) AND NOT F = 6";

        [Benchmark(Description = "Simple comparison")]
        public Stmt ParseSimpleComparison()
        {
            var scanner = new Scanner(SimpleComparison);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multiple AND predicates with LIKE")]
        public Stmt ParseMultiplePredicate()
        {
            var scanner = new Scanner(MultiplePredicate);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "IN list with 5 values")]
        public Stmt ParseInList()
        {
            var scanner = new Scanner(InList);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "BETWEEN predicates")]
        public Stmt ParseBetweenPredicate()
        {
            var scanner = new Scanner(BetweenPredicate);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "EXISTS subquery")]
        public Stmt ParseExistsSubquery()
        {
            var scanner = new Scanner(ExistsSubquery);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Complex boolean with NOT")]
        public Stmt ParseComplexBoolean()
        {
            var scanner = new Scanner(ComplexBoolean);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for JOIN parsing
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Joins")]
    public class JoinBenchmarks
    {
        private const string InnerJoin = "SELECT u.Id, o.Total FROM Users u INNER JOIN Orders o ON u.Id = o.UserId";
        private const string MultiJoin = "SELECT u.Id, o.Total, p.Name FROM Users u INNER JOIN Orders o ON u.Id = o.UserId LEFT JOIN Products p ON o.ProductId = p.Id LEFT JOIN Categories c ON p.CategoryId = c.Id";
        private const string CrossApply = "SELECT d.Name, e.TopSalary FROM Departments d CROSS APPLY (SELECT TOP 1 Salary AS TopSalary FROM Employees WHERE DeptId = d.Id ORDER BY Salary DESC) e";
        private const string JoinWithHints = "SELECT u.Id FROM Users u WITH (NOLOCK) INNER JOIN Orders o WITH (NOLOCK) ON u.Id = o.UserId";
        private const string FullOuterJoin = "SELECT a.Id, b.Id FROM TableA a FULL OUTER JOIN TableB b ON a.JoinKey = b.JoinKey";

        [Benchmark(Description = "INNER JOIN")]
        public Stmt ParseInnerJoin()
        {
            var scanner = new Scanner(InnerJoin);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multi JOIN chain (4 tables)")]
        public Stmt ParseMultiJoin()
        {
            var scanner = new Scanner(MultiJoin);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "CROSS APPLY with subquery")]
        public Stmt ParseCrossApply()
        {
            var scanner = new Scanner(CrossApply);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "JOIN with table hints")]
        public Stmt ParseJoinWithHints()
        {
            var scanner = new Scanner(JoinWithHints);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "FULL OUTER JOIN")]
        public Stmt ParseFullOuterJoin()
        {
            var scanner = new Scanner(FullOuterJoin);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for Common Table Expression parsing
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "CTEs")]
    public class CTEBenchmarks
    {
        private const string SimpleCTE = "WITH cte AS (SELECT Id, Name FROM Users WHERE Active = 1) SELECT Id, Name FROM cte";
        private const string MultipleCTEs = "WITH cte1 AS (SELECT Id FROM Users), cte2 AS (SELECT UserId, Total FROM Orders), cte3 AS (SELECT Id, Name FROM Products) SELECT c1.Id, c2.Total, c3.Name FROM cte1 c1 JOIN cte2 c2 ON c1.Id = c2.UserId JOIN cte3 c3 ON c3.Id = 1";
        private const string CTEWithColumns = "WITH ranked(Id, Name, Rnk) AS (SELECT Id, Name, ROW_NUMBER() OVER (ORDER BY Id) FROM Users) SELECT Id, Name FROM ranked WHERE Rnk <= 10";

        [Benchmark(Description = "Simple CTE")]
        public Stmt ParseSimpleCTE()
        {
            var scanner = new Scanner(SimpleCTE);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multiple CTEs (3)")]
        public Stmt ParseMultipleCTEs()
        {
            var scanner = new Scanner(MultipleCTEs);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "CTE with column list")]
        public Stmt ParseCTEWithColumns()
        {
            var scanner = new Scanner(CTEWithColumns);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for set operation parsing (UNION, INTERSECT, EXCEPT)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "SetOperations")]
    public class SetOperationBenchmarks
    {
        private const string UnionAll = "SELECT Id, Name FROM Users UNION ALL SELECT Id, Name FROM Customers";
        private const string MultiUnion = "SELECT Id FROM TableA UNION SELECT Id FROM TableB UNION SELECT Id FROM TableC UNION ALL SELECT Id FROM TableD";
        private const string UnionWithOrderBy = "SELECT Id, Name FROM Users UNION ALL SELECT Id, Name FROM Customers ORDER BY Name OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY";
        private const string IntersectExcept = "SELECT Id FROM TableA INTERSECT SELECT Id FROM TableB EXCEPT SELECT Id FROM TableC";

        [Benchmark(Description = "UNION ALL")]
        public Stmt ParseUnionAll()
        {
            var scanner = new Scanner(UnionAll);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multi UNION chain (4 selects)")]
        public Stmt ParseMultiUnion()
        {
            var scanner = new Scanner(MultiUnion);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "UNION with ORDER BY and OFFSET")]
        public Stmt ParseUnionWithOrderBy()
        {
            var scanner = new Scanner(UnionWithOrderBy);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "INTERSECT and EXCEPT")]
        public Stmt ParseIntersectExcept()
        {
            var scanner = new Scanner(IntersectExcept);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for table hints and query hints
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Hints")]
    public class HintBenchmarks
    {
        private const string TableHints = "SELECT Id FROM Users WITH (NOLOCK, INDEX(IX_Users_Name)) WHERE Active = 1";
        private const string QueryHintSimple = "SELECT Id FROM Users OPTION (RECOMPILE)";
        private const string QueryHintComplex = "SELECT Id FROM Users u INNER JOIN Orders o ON u.Id = o.UserId OPTION (HASH JOIN, MAXDOP 4, OPTIMIZE FOR UNKNOWN)";
        private const string QueryHintUseHint = "SELECT Id FROM Users OPTION (USE HINT ('FORCE_LEGACY_CARDINALITY_ESTIMATION', 'ENABLE_PARALLEL_PLAN_PREFERENCE'))";

        [Benchmark(Description = "Table hints (NOLOCK, INDEX)")]
        public Stmt ParseTableHints()
        {
            var scanner = new Scanner(TableHints);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Simple query hint (RECOMPILE)")]
        public Stmt ParseQueryHintSimple()
        {
            var scanner = new Scanner(QueryHintSimple);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Complex query hints")]
        public Stmt ParseQueryHintComplex()
        {
            var scanner = new Scanner(QueryHintComplex);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "USE HINT with strings")]
        public Stmt ParseQueryHintUseHint()
        {
            var scanner = new Scanner(QueryHintUseHint);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for advanced clauses: GROUP BY extensions, OFFSET/FETCH, FOR XML/JSON, PIVOT
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Clauses")]
    public class ClauseBenchmarks
    {
        private const string GroupByRollup = "SELECT Region, City, SUM(Sales) FROM Data GROUP BY ROLLUP (Region, City)";
        private const string GroupByCube = "SELECT Region, City, Year, SUM(Sales) FROM Data GROUP BY CUBE (Region, City, Year)";
        private const string GroupingSets = "SELECT Region, City, Year, SUM(Sales) FROM Data GROUP BY GROUPING SETS ((Region, City), (Region, Year), (City), ())";
        private const string OffsetFetch = "SELECT Id, Name FROM Users ORDER BY Name OFFSET 50 ROWS FETCH NEXT 25 ROWS ONLY";
        private const string ForXmlPath = "SELECT Id, Name FROM Users FOR XML PATH('User'), ROOT('Users'), ELEMENTS, TYPE";
        private const string ForJsonPath = "SELECT Id, Name FROM Users FOR JSON PATH, ROOT('users'), INCLUDE_NULL_VALUES";
        private const string Pivot = "SELECT Year, [Q1], [Q2], [Q3], [Q4] FROM SalesData PIVOT (SUM(Amount) FOR Quarter IN ([Q1], [Q2], [Q3], [Q4])) AS pvt";

        [Benchmark(Description = "GROUP BY ROLLUP")]
        public Stmt ParseGroupByRollup()
        {
            var scanner = new Scanner(GroupByRollup);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "GROUP BY CUBE")]
        public Stmt ParseGroupByCube()
        {
            var scanner = new Scanner(GroupByCube);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "GROUPING SETS")]
        public Stmt ParseGroupingSets()
        {
            var scanner = new Scanner(GroupingSets);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "OFFSET/FETCH paging")]
        public Stmt ParseOffsetFetch()
        {
            var scanner = new Scanner(OffsetFetch);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "FOR XML PATH with options")]
        public Stmt ParseForXmlPath()
        {
            var scanner = new Scanner(ForXmlPath);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "FOR JSON PATH with options")]
        public Stmt ParseForJsonPath()
        {
            var scanner = new Scanner(ForJsonPath);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "PIVOT")]
        public Stmt ParsePivot()
        {
            var scanner = new Scanner(Pivot);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for expression types: CASE, CAST/CONVERT, IIF
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Expressions")]
    public class ExpressionBenchmarks
    {
        private const string SearchedCase = "SELECT CASE WHEN Status = 1 THEN 'Active' WHEN Status = 2 THEN 'Inactive' WHEN Status = 3 THEN 'Suspended' ELSE 'Unknown' END FROM Users";
        private const string SimpleCase = "SELECT CASE Status WHEN 1 THEN 'Active' WHEN 2 THEN 'Inactive' WHEN 3 THEN 'Suspended' ELSE 'Unknown' END FROM Users";
        private const string NestedCase = "SELECT CASE WHEN A = 1 THEN CASE WHEN B = 2 THEN 'X' ELSE 'Y' END ELSE CASE WHEN C = 3 THEN 'Z' ELSE 'W' END END FROM T";
        private const string CastConvert = "SELECT CAST(Id AS VARCHAR(10)), CONVERT(DECIMAL(18, 2), Amount), TRY_CAST(Value AS INT), TRY_CONVERT(DATE, DateStr, 101) FROM T";
        private const string IifExpression = "SELECT IIF(Score >= 90, 'A', IIF(Score >= 80, 'B', IIF(Score >= 70, 'C', 'F'))) FROM Students";

        [Benchmark(Description = "Searched CASE (3 WHEN)")]
        public Stmt ParseSearchedCase()
        {
            var scanner = new Scanner(SearchedCase);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Simple CASE (3 WHEN)")]
        public Stmt ParseSimpleCase()
        {
            var scanner = new Scanner(SimpleCase);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Nested CASE expressions")]
        public Stmt ParseNestedCase()
        {
            var scanner = new Scanner(NestedCase);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "CAST/CONVERT/TRY variants")]
        public Stmt ParseCastConvert()
        {
            var scanner = new Scanner(CastConvert);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Nested IIF expressions")]
        public Stmt ParseIifExpression()
        {
            var scanner = new Scanner(IifExpression);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    /// <summary>
    /// Benchmarks for production-scale queries exercising many features simultaneously
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("RealWorld")]
    public class RealWorldBenchmarks
    {
        private const string ReportQuery = @"
            WITH MonthlySales AS (
                SELECT
                    s.RegionId,
                    DATEPART(MONTH, s.SaleDate) AS SaleMonth,
                    SUM(s.Amount) AS TotalAmount,
                    COUNT(*) AS SaleCount
                FROM Sales s
                INNER JOIN Products p ON s.ProductId = p.Id
                WHERE s.SaleDate BETWEEN '2024-01-01' AND '2024-12-31'
                    AND p.Active = 1
                    AND s.Status IN ('completed', 'shipped')
                GROUP BY s.RegionId, DATEPART(MONTH, s.SaleDate)
            )
            SELECT
                r.Name AS RegionName,
                ms.SaleMonth,
                ms.TotalAmount,
                ms.SaleCount,
                CASE
                    WHEN ms.TotalAmount > 100000 THEN 'High'
                    WHEN ms.TotalAmount > 50000 THEN 'Medium'
                    ELSE 'Low'
                END AS PerformanceTier,
                ROW_NUMBER() OVER (PARTITION BY ms.SaleMonth ORDER BY ms.TotalAmount DESC) AS MonthlyRank,
                SUM(ms.TotalAmount) OVER (PARTITION BY r.Name ORDER BY ms.SaleMonth ROWS UNBOUNDED PRECEDING) AS RunningTotal
            FROM MonthlySales ms
            INNER JOIN Regions r ON ms.RegionId = r.Id
            WHERE ms.SaleCount > 10
            GROUP BY r.Name, ms.SaleMonth, ms.TotalAmount, ms.SaleCount, ms.RegionId, r.Id
            HAVING SUM(ms.TotalAmount) > 5000
            ORDER BY ms.SaleMonth, ms.TotalAmount DESC
            OFFSET 0 ROWS FETCH NEXT 100 ROWS ONLY";

        private const string AnalyticsQuery = @"
            WITH UserMetrics AS (
                SELECT
                    u.Id AS UserId,
                    u.Name,
                    COUNT(o.Id) AS OrderCount,
                    SUM(o.Total) AS TotalSpent,
                    AVG(o.Total) AS AvgOrder
                FROM Users u
                LEFT JOIN Orders o ON u.Id = o.UserId
                WHERE u.CreatedDate >= '2023-01-01'
                GROUP BY u.Id, u.Name
            ),
            RankedUsers AS (
                SELECT
                    UserId,
                    Name,
                    OrderCount,
                    TotalSpent,
                    AvgOrder,
                    NTILE(10) OVER (ORDER BY TotalSpent DESC) AS SpendDecile,
                    DENSE_RANK() OVER (ORDER BY OrderCount DESC) AS FrequencyRank
                FROM UserMetrics
                WHERE TotalSpent > 0
            )
            SELECT
                ru.Name,
                ru.OrderCount,
                ru.TotalSpent,
                ru.SpendDecile,
                ru.FrequencyRank,
                d.TopCategory,
                d.TopCategorySpend
            FROM RankedUsers ru
            CROSS APPLY (
                SELECT TOP 1
                    c.Name AS TopCategory,
                    SUM(oi.Amount) AS TopCategorySpend
                FROM OrderItems oi
                INNER JOIN Products p ON oi.ProductId = p.Id
                INNER JOIN Categories c ON p.CategoryId = c.Id
                WHERE oi.OrderId IN (SELECT Id FROM Orders WHERE UserId = ru.UserId)
                GROUP BY c.Name
                ORDER BY SUM(oi.Amount) DESC
            ) d
            WHERE ru.SpendDecile <= 3
            ORDER BY ru.TotalSpent DESC
            FOR JSON PATH, ROOT('topCustomers')";

        [Benchmark(Description = "Report query (~1500 chars)")]
        public Stmt ParseReportQuery()
        {
            var scanner = new Scanner(ReportQuery);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Analytics query (~1200 chars)")]
        public Stmt ParseAnalyticsQuery()
        {
            var scanner = new Scanner(AnalyticsQuery);
            var tokens = scanner.ScanTokens();
            var parser = new Parser(tokens);
            return parser.Parse();
        }
    }
}
