# TSQL.Benchmarks

Performance benchmarks for the TSQL parser library, built with [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run --project TSQL.Benchmarks/TSQL.Benchmarks.csproj -c Release -- --filter *

# List available benchmarks
dotnet run --project TSQL.Benchmarks/TSQL.Benchmarks.csproj -c Release -- --list flat

# Run a specific benchmark class
dotnet run --project TSQL.Benchmarks/TSQL.Benchmarks.csproj -c Release -- --filter *ScannerBenchmarks*

# Run by category
dotnet run --project TSQL.Benchmarks/TSQL.Benchmarks.csproj -c Release -- --filter *Parser*
```

> **Note:** The `-c Release` flag is required. BenchmarkDotNet will refuse to run in Debug configuration.

## Benchmark Categories

| Category | Class | What it measures |
|---|---|---|
| Scanner | `ScannerBenchmarks` | Tokenization speed across query sizes |
| Parser / Basic | `ParserBenchmarks` | Columns, aliases, identifiers, subqueries |
| Parser / WindowFunctions | `WindowFunctionBenchmarks` | OVER clause with PARTITION BY and frame clauses |
| Parser / Functions | `FunctionBenchmarks` | Function calls including schema-qualified and nested |
| Parser / Predicates | `PredicateBenchmarks` | WHERE clauses: comparisons, IN, BETWEEN, EXISTS |
| Parser / Joins | `JoinBenchmarks` | JOIN types including CROSS APPLY |
| Parser / Expressions | `ExpressionBenchmarks` | CASE, CAST, CONVERT, IIF, TRY variants |
| Parser / CTEs | `CTEBenchmarks` | Common Table Expressions with column lists |
| Parser / SetOperations | `SetOperationBenchmarks` | UNION, INTERSECT, EXCEPT |
| Parser / Hints | `HintBenchmarks` | Table hints and query hints (OPTION clause) |
| Parser / Clauses | `ClauseBenchmarks` | GROUP BY extensions, OFFSET/FETCH, FOR XML/JSON, PIVOT |
| RoundTrip | `RoundTripBenchmarks` | Full parse + `ToSource()` reconstruction |
| Pipeline | `PipelineBenchmarks` | Full pipeline vs parse-only (pre-tokenized) |
| Throughput | `ThroughputBenchmarks` | Batch parsing of multiple queries |
| StandardLibrary | `AddConditionBenchmarks` | WHERE clause appending |
| StandardLibrary | `ParameterizedAddConditionBenchmarks` | Parameterized AddCondition with collision resolution |
| StandardLibrary | `ParameterizeBenchmarks` | Literal parameterization |
| StandardLibrary | `CollectTableReferencesBenchmarks` | Table reference collection via visitor |
| StandardLibrary | `CollectColumnReferencesBenchmarks` | Column reference collection via visitor |

Each benchmark class tests three complexity levels (Simple, Medium, Complex) and includes CPU and memory diagnostics.
