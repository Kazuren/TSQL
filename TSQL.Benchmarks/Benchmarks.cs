using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using System.Collections.Generic;
using TSQL.StandardLibrary.Visitors;

namespace TSQL.Benchmarks
{
    #region Scanner

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

        [Benchmark(Description = "~25 chars")]
        public int Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            return scanner.ScanTokens().Count;
        }

        [Benchmark(Description = "~130 chars")]
        public int Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            return scanner.ScanTokens().Count;
        }

        [Benchmark(Description = "~850 chars")]
        public int Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            return scanner.ScanTokens().Count;
        }
    }

    #endregion

    #region Parser

    /// <summary>
    /// Benchmarks for basic parsing: columns, aliases, identifiers, expressions, subqueries
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("Parser", "Basic")]
    public class ParserBenchmarks
    {
        private const string SimpleQuery = "SELECT Id, Name FROM Users";
        private const string MediumQuery = "SELECT u.Id, u.Name AS UserName, d.s.t.col, Total = o.Amount FROM Users u";
        private const string ComplexQuery = "SELECT Id, (SELECT COUNT(*) FROM Orders WHERE UserId = u.Id) AS OrderCount, a + b * c, COALESCE(x, y, z) FROM Users u";

        [Benchmark(Description = "2 columns")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Aliases + qualified identifiers")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Subquery + expressions + COALESCE")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT ROW_NUMBER() OVER (ORDER BY Id) FROM T";
        private const string MediumQuery = "SELECT RANK() OVER (PARTITION BY Department ORDER BY Salary DESC), SUM(Amount) OVER (PARTITION BY CustomerId ORDER BY OrderDate ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) FROM T";
        private const string ComplexQuery = @"
            SELECT
                ROW_NUMBER() OVER (ORDER BY Id) AS RowNum,
                RANK() OVER (PARTITION BY Dept ORDER BY Salary DESC) AS DeptRank,
                DENSE_RANK() OVER (ORDER BY Score DESC) AS ScoreRank,
                NTILE(4) OVER (ORDER BY Revenue DESC) AS Quartile,
                SUM(Sales) OVER (PARTITION BY Region) AS RegionTotal,
                AVG(Sales) OVER (ORDER BY Month ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) AS MovingAvg
            FROM SalesData";

        [Benchmark(Description = "ROW_NUMBER() simple")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "PARTITION BY + frame clause")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "6 window functions")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT GETDATE(), UPPER(Name) FROM T";
        private const string MediumQuery = "SELECT COALESCE(a, b, c, d, e), UPPER(TRIM(SUBSTRING(Name, 1, 10))) FROM T";
        private const string ComplexQuery = "SELECT dbo.MyFunction(a, b), sys.fn_listextendedproperty(NULL, NULL, NULL, NULL, NULL, NULL, NULL) FROM T";

        [Benchmark(Description = "No-args + single arg")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multi-arg + nested calls")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Schema-qualified functions")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
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
        private const string SimpleQuery = "SELECT Id FROM T WHERE Active = 1 AND Age > 18";
        private const string MediumQuery = "SELECT Id FROM T WHERE Status IN ('active', 'pending', 'review') AND CreatedDate BETWEEN '2024-01-01' AND '2024-12-31' AND Name LIKE 'J%'";
        private const string ComplexQuery = "SELECT Id FROM T WHERE (A = 1 OR B = 2) AND NOT EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = T.Id AND o.Total > 100) AND (C > 3 OR (D < 4 AND E = 5))";

        [Benchmark(Description = "Comparison + AND")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "IN + BETWEEN + LIKE")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "EXISTS + complex boolean")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT u.Id, o.Total FROM Users u INNER JOIN Orders o ON u.Id = o.UserId";
        private const string MediumQuery = "SELECT u.Id, o.Total, p.Name FROM Users u INNER JOIN Orders o ON u.Id = o.UserId LEFT JOIN Products p ON o.ProductId = p.Id LEFT JOIN Categories c ON p.CategoryId = c.Id";
        private const string ComplexQuery = "SELECT d.Name, e.TopSalary FROM Departments d CROSS APPLY (SELECT TOP 1 Salary AS TopSalary FROM Employees WHERE DeptId = d.Id ORDER BY Salary DESC) e FULL OUTER JOIN Summary s ON d.Id = s.DeptId";

        [Benchmark(Description = "Single INNER JOIN")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "4-table join chain")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "CROSS APPLY + FULL OUTER JOIN")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT CASE Status WHEN 1 THEN 'Active' WHEN 2 THEN 'Inactive' ELSE 'Unknown' END, CAST(Id AS VARCHAR(10)) FROM T";
        private const string MediumQuery = "SELECT CASE WHEN Status = 1 THEN 'Active' WHEN Status = 2 THEN 'Inactive' WHEN Status = 3 THEN 'Suspended' ELSE 'Unknown' END, CONVERT(DECIMAL(18, 2), Amount), IIF(Score >= 90, 'A', 'B') FROM T";
        private const string ComplexQuery = "SELECT CASE WHEN A = 1 THEN CASE WHEN B = 2 THEN 'X' ELSE 'Y' END ELSE CASE WHEN C = 3 THEN 'Z' ELSE 'W' END END, IIF(Score >= 90, 'A', IIF(Score >= 80, 'B', IIF(Score >= 70, 'C', 'F'))), TRY_CAST(Value AS INT), TRY_CONVERT(DATE, DateStr, 101) FROM T";

        [Benchmark(Description = "Simple CASE + CAST")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Searched CASE + CONVERT + IIF")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Nested CASE + nested IIF + TRY variants")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "WITH cte AS (SELECT Id, Name FROM Users WHERE Active = 1) SELECT Id, Name FROM cte";
        private const string MediumQuery = "WITH cte1 AS (SELECT Id FROM Users), cte2 AS (SELECT UserId, Total FROM Orders), cte3 AS (SELECT Id, Name FROM Products) SELECT c1.Id, c2.Total, c3.Name FROM cte1 c1 JOIN cte2 c2 ON c1.Id = c2.UserId JOIN cte3 c3 ON c3.Id = 1";
        private const string ComplexQuery = "WITH ranked(Id, Name, Rnk) AS (SELECT Id, Name, ROW_NUMBER() OVER (ORDER BY Id) FROM Users) SELECT Id, Name FROM ranked WHERE Rnk <= 10";

        [Benchmark(Description = "Single CTE")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "3 CTEs with joins")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "CTE with column list + window function")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT Id, Name FROM Users UNION ALL SELECT Id, Name FROM Customers";
        private const string MediumQuery = "SELECT Id FROM TableA UNION SELECT Id FROM TableB INTERSECT SELECT Id FROM TableC EXCEPT SELECT Id FROM TableD";
        private const string ComplexQuery = "SELECT Id, Name FROM Users UNION ALL SELECT Id, Name FROM Customers ORDER BY Name OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY";

        [Benchmark(Description = "Single UNION ALL")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Mixed set operations chain")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "UNION + ORDER BY + OFFSET")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT Id FROM Users WITH (NOLOCK) WHERE Active = 1";
        private const string MediumQuery = "SELECT Id FROM Users WITH (NOLOCK, INDEX(IX_Users_Name)) WHERE Active = 1 OPTION (RECOMPILE)";
        private const string ComplexQuery = "SELECT Id FROM Users u WITH (NOLOCK) INNER JOIN Orders o WITH (ROWLOCK) ON u.Id = o.UserId OPTION (HASH JOIN, MAXDOP 4, OPTIMIZE FOR UNKNOWN, USE HINT ('FORCE_LEGACY_CARDINALITY_ESTIMATION'))";

        [Benchmark(Description = "Single table hint")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Table + query hint")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Multi-table + complex query hints")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
        private const string SimpleQuery = "SELECT Region, City, SUM(Sales) FROM Data GROUP BY ROLLUP (Region, City) ORDER BY Region OFFSET 50 ROWS FETCH NEXT 25 ROWS ONLY";
        private const string MediumQuery = "SELECT Region, City, Year, SUM(Sales) AS TotalSales FROM Data GROUP BY GROUPING SETS ((Region, City), (Region, Year), (City), ()) FOR JSON PATH, ROOT('data'), INCLUDE_NULL_VALUES";
        private const string ComplexQuery = "SELECT Year, [Q1], [Q2], [Q3], [Q4] FROM SalesData PIVOT (SUM(Amount) FOR Quarter IN ([Q1], [Q2], [Q3], [Q4])) AS pvt FOR XML PATH('Year'), ROOT('Sales'), ELEMENTS, TYPE";

        [Benchmark(Description = "ROLLUP + OFFSET/FETCH")]
        public Stmt Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "GROUPING SETS + FOR JSON")]
        public Stmt Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "PIVOT + FOR XML")]
        public Stmt Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }
    }

    #endregion

    #region Round-Trip & Utility

    /// <summary>
    /// Benchmarks for round-trip operations (parse + ToSource)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("RoundTrip")]
    public class RoundTripBenchmarks
    {
        private const string SimpleQuery = "SELECT Id, Name, Email FROM Users";
        private const string MediumQuery = "SELECT ROW_NUMBER() OVER (PARTITION BY Dept ORDER BY Salary DESC) AS Rank FROM Employees";
        private const string ComplexQuery = "SELECT SUM(Amount) OVER (ORDER BY Date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningTotal FROM Orders";

        [Benchmark(Description = "Simple query")]
        public string Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            var stmt = parser.Parse();
            return stmt.ToSource();
        }

        [Benchmark(Description = "Window function")]
        public string Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            var stmt = parser.Parse();
            return stmt.ToSource();
        }

        [Benchmark(Description = "Window with frame clause")]
        public string Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
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
            Scanner scanner = new Scanner(Query);
            _preTokenized = scanner.ScanTokens();
        }

        [Benchmark(Description = "Full pipeline (scan + parse)", Baseline = true)]
        public Stmt FullPipeline()
        {
            Scanner scanner = new Scanner(Query);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        [Benchmark(Description = "Parse only (pre-tokenized)")]
        public Stmt ParseOnly()
        {
            Parser parser = new Parser(_preTokenized);
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
            Stmt[] results = new Stmt[Queries.Length];
            for (int i = 0; i < Queries.Length; i++)
            {
                Scanner scanner = new Scanner(Queries[i]);
                var tokens = scanner.ScanTokens();
                Parser parser = new Parser(tokens);
                results[i] = parser.Parse();
            }
            return results;
        }

        [Benchmark(Description = "Parse same query 100 times")]
        public Stmt[] ParseSameQuery100Times()
        {
            const string query = "SELECT ROW_NUMBER() OVER (ORDER BY Id) AS RowNum, Name FROM Users";
            Stmt[] results = new Stmt[100];
            for (int i = 0; i < 100; i++)
            {
                Scanner scanner = new Scanner(query);
                var tokens = scanner.ScanTokens();
                Parser parser = new Parser(tokens);
                results[i] = parser.Parse();
            }
            return results;
        }
    }

    #endregion

    #region StandardLibrary

    /// <summary>
    /// Benchmarks for AddCondition (string-based WHERE clause appending)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("StandardLibrary")]
    public class AddConditionBenchmarks
    {
        private const string SimpleQuery = "SELECT Id, Name FROM Users";
        private const string MediumQuery = "SELECT u.Id, o.Total FROM Users u INNER JOIN Orders o ON u.Id = o.UserId WHERE o.Total > 100";
        private const string ComplexQuery = "WITH Active AS (SELECT Id, Name FROM Users WHERE Status = 'active') SELECT Id, Name FROM Active UNION ALL SELECT Id, Name FROM Customers";

        [Benchmark(Description = "No existing WHERE")]
        public string Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddCondition("Active = 1");
            return stmt.ToSource();
        }

        [Benchmark(Description = "Existing WHERE + compound condition")]
        public string Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddCondition("u.Active = 1 AND u.Status = 'verified'");
            return stmt.ToSource();
        }

        [Benchmark(Description = "CTE + UNION with WhereClauseTarget.All")]
        public string Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddCondition("CreatedDate > '2024-01-01'", WhereClauseTarget.All);
            return stmt.ToSource();
        }
    }

    /// <summary>
    /// Benchmarks for schema-aware condition appending (column existence checking + auto-prefixing)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("StandardLibrary")]
    public class SchemaAwareConditionBenchmarks
    {
        private const string SimpleQuery = "SELECT * FROM T1";
        private const string MediumQuery = "SELECT A.ID, B.ID FROM T1 AS A JOIN T2 AS B ON A.ID = B.T1_ID";
        private const string ComplexQuery = "WITH CTE AS (SELECT * FROM T1) SELECT * FROM CTE JOIN T2 ON CTE.ID = T2.ID";

        private static readonly Dictionary<string, HashSet<string>> SimpleSchema = new Dictionary<string, HashSet<string>>
        {
            ["T1"] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "I_ID" }
        };

        private static readonly Dictionary<string, HashSet<string>> MediumSchema = new Dictionary<string, HashSet<string>>
        {
            ["T1"] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ID", "I_ID" },
            ["T2"] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ID", "T1_ID", "I_ID" }
        };

        private static readonly Dictionary<string, HashSet<string>> ComplexSchema = new Dictionary<string, HashSet<string>>
        {
            ["T1"] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ID", "I_ID" },
            ["T2"] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "ID", "STATUS", "I_ID" }
        };

        private static readonly ColumnExistenceChecker SimpleChecker = CreateChecker(SimpleSchema);
        private static readonly ColumnExistenceChecker MediumChecker = CreateChecker(MediumSchema);
        private static readonly ColumnExistenceChecker ComplexChecker = CreateChecker(ComplexSchema);

        private static ColumnExistenceChecker CreateChecker(Dictionary<string, HashSet<string>> schema)
        {
            return (tableName, columnNames) =>
            {
                if (!schema.TryGetValue(tableName, out HashSet<string> columns))
                {
                    return false;
                }
                foreach (string col in columnNames)
                {
                    if (!columns.Contains(col))
                    {
                        return false;
                    }
                }
                return true;
            };
        }

        [Benchmark(Description = "Single table, single column")]
        public string Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddSchemaAwareCondition("I_ID = 0", SimpleChecker);
            return stmt.ToSource();
        }

        [Benchmark(Description = "Multi-table JOIN with alias resolution")]
        public string Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddSchemaAwareCondition("I_ID = 0 OR I_ID = 1", MediumChecker);
            return stmt.ToSource();
        }

        [Benchmark(Description = "CTE + JOIN with mixed prefixed/unprefixed columns")]
        public string Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddSchemaAwareCondition("T2.STATUS = 1 AND I_ID = 0", ComplexChecker, WhereClauseTarget.All);
            return stmt.ToSource();
        }
    }

    /// <summary>
    /// Benchmarks for parameterized AddCondition with collision resolution
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("StandardLibrary")]
    public class ParameterizedAddConditionBenchmarks
    {
        private const string SimpleQuery = "SELECT Id FROM Users";
        private const string MediumQuery = "SELECT Id FROM Users WHERE Status = @P0";
        private const string ComplexQuery = "SELECT Id FROM Users WHERE Status = @P0";

        private static readonly object[] SimpleValues = new object[] { ("@Active", (object)1) };
        private static readonly object[] MediumValues = new object[] { ("@P0", (object)1), ("@P1", (object)18) };
        private static readonly object[] ComplexValues1 = new object[] { ("@P0", (object)1) };
        private static readonly object[] ComplexValues2 = new object[] { ("@P0", (object)"active") };

        [Benchmark(Description = "No collision, single param")]
        public string Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddCondition("Active = @Active", out _, SimpleValues);
            return stmt.ToSource();
        }

        [Benchmark(Description = "@P0 collision + multiple params")]
        public string Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddCondition("Active = @P0 AND Age > @P1", out _, MediumValues);
            return stmt.ToSource();
        }

        [Benchmark(Description = "Chained calls with cascading collisions")]
        public string Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.AddCondition("Active = @P0", out _, ComplexValues1);
            stmt.AddCondition("Region = @P0", out _, ComplexValues2);
            return stmt.ToSource();
        }
    }

    /// <summary>
    /// Benchmarks for literal parameterization
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("StandardLibrary")]
    public class ParameterizeBenchmarks
    {
        private const string SimpleQuery = "SELECT Id FROM Users WHERE Active = 1";
        private const string MediumQuery = "SELECT Id FROM Users WHERE Status = 'active' AND Age > 18 AND Amount < 999.99 AND Name IN ('Alice', 'Bob', 'Charlie')";
        private const string ComplexQuery = @"
            SELECT Id,
                CASE WHEN Score > 90 THEN 'A' WHEN Score > 80 THEN 'B' WHEN Score > 70 THEN 'C' ELSE 'F' END AS Grade
            FROM Users
            WHERE Active = 1
                AND CreatedDate BETWEEN '2024-01-01' AND '2024-12-31'
                AND Status IN ('active', 'verified', 'premium')
                AND Amount > 50.00
                AND @P0 IS NOT NULL";

        [Benchmark(Description = "1 literal")]
        public string Simple()
        {
            Scanner scanner = new Scanner(SimpleQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.Parameterize(out _);
            return stmt.ToSource();
        }

        [Benchmark(Description = "6 literals across WHERE + IN")]
        public string Medium()
        {
            Scanner scanner = new Scanner(MediumQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.Parameterize(out _);
            return stmt.ToSource();
        }

        [Benchmark(Description = "10+ literals with CASE + BETWEEN + IN + collision")]
        public string Complex()
        {
            Scanner scanner = new Scanner(ComplexQuery);
            var tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            Stmt stmt = parser.Parse();
            stmt.Parameterize(out _);
            return stmt.ToSource();
        }
    }

    /// <summary>
    /// Benchmarks for table reference collection (read-only visitor)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("StandardLibrary")]
    public class CollectTableReferencesBenchmarks
    {
        private const string SimpleQuery = "SELECT Id FROM Users";
        private const string MediumQuery = "SELECT u.Id FROM Users u INNER JOIN Orders o ON u.Id = o.UserId LEFT JOIN Products p ON o.ProductId = p.Id";
        private const string ComplexQuery = @"
            WITH Active AS (
                SELECT Id, DeptId FROM Users WHERE Active = 1
            )
            SELECT a.Id, d.TopSalary
            FROM Active a
            INNER JOIN Departments dept ON a.DeptId = dept.Id
            LEFT JOIN Regions r ON dept.RegionId = r.Id
            CROSS APPLY (
                SELECT TOP 1 Salary AS TopSalary
                FROM Employees e
                WHERE e.DeptId = dept.Id
                ORDER BY Salary DESC
            ) d
            WHERE EXISTS (
                SELECT 1 FROM Orders o
                INNER JOIN OrderItems oi ON o.Id = oi.OrderId
                INNER JOIN Products p ON oi.ProductId = p.Id
                WHERE o.UserId = a.Id
            )";

        private Stmt _simpleStmt;
        private Stmt _mediumStmt;
        private Stmt _complexStmt;

        [GlobalSetup]
        public void Setup()
        {
            _simpleStmt = new Parser(new Scanner(SimpleQuery).ScanTokens()).Parse();
            _mediumStmt = new Parser(new Scanner(MediumQuery).ScanTokens()).Parse();
            _complexStmt = new Parser(new Scanner(ComplexQuery).ScanTokens()).Parse();
        }

        [Benchmark(Description = "1 table")]
        public TableReferences Simple()
        {
            return _simpleStmt.CollectTableReferences();
        }

        [Benchmark(Description = "3 tables, 2 joins")]
        public TableReferences Medium()
        {
            return _mediumStmt.CollectTableReferences();
        }

        [Benchmark(Description = "CTE + CROSS APPLY + EXISTS (8 tables)")]
        public TableReferences Complex()
        {
            return _complexStmt.CollectTableReferences();
        }
    }

    /// <summary>
    /// Benchmarks for column reference collection (read-only visitor)
    /// </summary>
    [CPUUsageDiagnoser]
    [MemoryDiagnoser]
    [BenchmarkCategory("StandardLibrary")]
    public class CollectColumnReferencesBenchmarks
    {
        private const string SimpleQuery = "SELECT Id, Name, Email FROM Users";
        private const string MediumQuery = @"
            SELECT u.Id, u.Name, o.Total
            FROM Users u
            INNER JOIN Orders o ON u.Id = o.UserId
            WHERE u.Active = 1 AND o.Total > 100
            ORDER BY o.Total DESC, u.Name";
        private const string ComplexQuery = @"
            WITH TopOrders AS (
                SELECT UserId, SUM(Total) AS OrderTotal
                FROM Orders
                GROUP BY UserId
                HAVING SUM(Total) > 1000
            )
            SELECT u.Id, u.Name, t.OrderTotal,
                (SELECT COUNT(*) FROM Reviews r WHERE r.UserId = u.Id) AS ReviewCount
            FROM Users u
            INNER JOIN TopOrders t ON u.Id = t.UserId
            WHERE u.Active = 1
            GROUP BY u.Id, u.Name, t.OrderTotal
            HAVING COUNT(*) > 0
            ORDER BY t.OrderTotal DESC";

        private Stmt _simpleStmt;
        private Stmt _mediumStmt;
        private Stmt _complexStmt;

        [GlobalSetup]
        public void Setup()
        {
            _simpleStmt = new Parser(new Scanner(SimpleQuery).ScanTokens()).Parse();
            _mediumStmt = new Parser(new Scanner(MediumQuery).ScanTokens()).Parse();
            _complexStmt = new Parser(new Scanner(ComplexQuery).ScanTokens()).Parse();
        }

        [Benchmark(Description = "3 columns, SELECT only")]
        public IReadOnlyList<Expr.ColumnIdentifier> Simple()
        {
            return _simpleStmt.CollectColumnReferences();
        }

        [Benchmark(Description = "JOIN + WHERE + ORDER BY, all clauses")]
        public IReadOnlyList<Expr.ColumnIdentifier> Medium()
        {
            return _mediumStmt.CollectColumnReferences(ColumnReferenceScope.OutermostQuery, ColumnReferenceClause.All);
        }

        [Benchmark(Description = "CTE + subquery + GROUP BY + HAVING, all scopes")]
        public IReadOnlyList<Expr.ColumnIdentifier> Complex()
        {
            return _complexStmt.CollectColumnReferences(ColumnReferenceScope.All, ColumnReferenceClause.All);
        }
    }

    #endregion
}
