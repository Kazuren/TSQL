# TSQL

A T-SQL parser library targeting .NET Standard 2.0. Parse SQL into a full AST, traverse or transform it, and serialize it back to SQL with exact formatting preserved.

## Features

- **Exact source regeneration** — whitespace and comments are preserved as trivia on tokens, so `Parse` → `ToSource` reproduces the original text exactly
- **Comprehensive T-SQL support** — SELECT, INSERT, CTEs, window functions, PIVOT/UNPIVOT, temporal tables, FOR XML/JSON, query hints, and more
- **Visitor pattern** — `SqlWalker` base class for easy AST traversal; override only the node types you care about
- **Built-in transformations** — the `TSQL.StandardLibrary` package provides ready-made operations: add WHERE conditions, parameterize literals, collect references, and materialize temp tables
- **Error reporting** — `ParseError` includes line, column, and the original SQL text for diagnostics

## Quick Start

```csharp
using TSQL;

// Parse a SQL statement
Stmt stmt = Stmt.Parse("SELECT Id, Name FROM Users WHERE Active = 1");

// Serialize back to SQL (preserves original formatting)
string sql = stmt.ToSource();
// => "SELECT Id, Name FROM Users WHERE Active = 1"
```

## Parsing

### Entry Points

```csharp
// General statement (SELECT or INSERT)
Stmt stmt = Stmt.Parse("SELECT * FROM Orders");

// Specifically a SELECT
Stmt.Select select = Stmt.ParseSelect("SELECT * FROM Orders");

// Specifically an INSERT
Stmt.Insert insert = Stmt.ParseInsert("INSERT INTO Orders (Id) VALUES (1)");

// Multiple semicolon-separated statements
Script script = Script.Parse("SELECT 1; SELECT 2;");
foreach (Stmt s in script.Statements)
{
    Console.WriteLine(s.ToSource());
}

// Standalone predicate (WHERE condition)
Predicate predicate = Predicate.ParsePredicate("Active = 1 AND TenantId = 42");
```

### Error Handling

All `Parse` methods throw `ParseError` on invalid SQL:

```csharp
try
{
    Stmt stmt = Stmt.Parse("SELECT FROM");
}
catch (ParseError e)
{
    Console.WriteLine(e.Message);   // Error description
    Console.WriteLine(e.Line);      // 1-based line number
    Console.WriteLine(e.Column);    // 0-based column offset
    Console.WriteLine(e.SqlText);   // Original SQL text
}
```

## Working with the AST

The AST is composed of a few core node types:

| Type | Represents |
|------|-----------|
| `Stmt` | Statements — `Stmt.Select`, `Stmt.Insert` |
| `Expr` | Expressions — literals, column identifiers, functions, CASE, CAST, subqueries |
| `Predicate` | WHERE/HAVING conditions — comparison, LIKE, BETWEEN, IN, EXISTS, AND/OR |
| `TableSource` | FROM clause — table references, joins, subqueries, PIVOT/UNPIVOT |
| `QueryExpression` | Query structure — `SelectExpression`, `SetOperation` (UNION, EXCEPT) |

### Navigating the Tree

Cast to concrete types to access properties:

```csharp
Stmt.Select select = Stmt.ParseSelect(
    "SELECT u.Name FROM Users u WHERE u.Active = 1");

// Access the SelectExpression
var query = (SelectExpression)select.Query;

// FROM clause — first table source
var table = (TableReference)query.From.TableSources[0];
string tableName = table.TableName.ObjectName.Name; // "Users"
string alias = table.Alias.Name;                    // "u"

// WHERE clause
var where = (Predicate.Comparison)query.Where;
var column = (Expr.ColumnIdentifier)where.Left;
string columnName = column.ColumnName.Name;          // "Active"
```

### Walking the AST

Subclass `SqlWalker` and override the visit methods you need. The default implementations walk into child nodes, so call `base` to continue traversal:

```csharp
using TSQL;
using TSQL.AST;

class ColumnCollector : SqlWalker
{
    public List<string> Columns { get; } = new List<string>();

    protected override void VisitColumnIdentifier(Expr.ColumnIdentifier expr)
    {
        Columns.Add(expr.ColumnName.Name);
    }
}

var walker = new ColumnCollector();
walker.Walk(Stmt.Parse("SELECT a, b FROM T WHERE c = 1"));
// walker.Columns => ["a", "b", "c"]
```

`SqlWalker` implements all four visitor interfaces (`Expr.Visitor<T>`, `Stmt.Visitor<T>`, `Predicate.Visitor<T>`, `TableSource.Visitor<T>`), so a single walker traverses the entire tree.

## Transformations

The `TSQL.StandardLibrary` package provides extension methods on `Stmt` for common operations. All transformations mutate the AST in place and return the statement for chaining.

```csharp
using TSQL;
using TSQL.StandardLibrary.Visitors;
```

### Add WHERE Conditions

```csharp
var stmt = Stmt.Parse("SELECT * FROM Users");
stmt.AddCondition("Active = 1");
stmt.ToSource();
// => "SELECT * FROM Users WHERE Active = 1"

// Combine with existing WHERE
stmt = Stmt.Parse("SELECT * FROM Users WHERE Role = 'admin'");
stmt.AddCondition("TenantId = 5");
stmt.ToSource();
// => "SELECT * FROM Users WHERE Role = 'admin' AND TenantId = 5"
```

With parameterized values:

```csharp
stmt.AddCondition(
    "TenantId = @TenantId",
    new object[] { ("@TenantId", 42) },
    out IReadOnlyDictionary<string, object> parameters);
// parameters["@TenantId"] == 42
```

Use `WhereClauseTarget` flags to control which query levels receive the condition:

```csharp
// Add to both sides of a UNION
stmt = Stmt.Parse("SELECT * FROM Users UNION SELECT * FROM Admins");
stmt.AddCondition("Active = 1", WhereClauseTarget.OutermostQuery);

// Add everywhere: outermost, CTEs, and all subqueries
stmt.AddCondition("TenantId = 1", WhereClauseTarget.All);
```

### Parameterize Literals

Replace literal values with `@P0`, `@P1`, etc.:

```csharp
var stmt = Stmt.Parse("SELECT * FROM T WHERE x = 42 AND y = 'hello'");
stmt.Parameterize(out IReadOnlyDictionary<string, object> parameters);

stmt.ToSource();
// => "SELECT * FROM T WHERE x = @P0 AND y = @P1"
// parameters["@P0"] == 42
// parameters["@P1"] == "hello"
```

NULL literals are preserved (since `IS NULL` semantics differ from `= @param`).

### Schema-Aware Conditions

Add WHERE conditions only to tables that actually have the referenced columns. You provide a `ColumnExistenceChecker` callback:

```csharp
ColumnExistenceChecker checker = (tableName, columnNames) =>
{
    // Return true if the table has ALL of the given columns.
    // Query INFORMATION_SCHEMA, use an in-memory map, etc.
};

var stmt = Stmt.Parse(
    "SELECT * FROM Users u JOIN Orders o ON u.Id = o.UserId");
stmt.AddSchemaAwareCondition("TenantId = 1", checker);
// If both tables have TenantId:
// => "... WHERE u.TenantId = 1 AND o.TenantId = 1"
// If only Users has TenantId:
// => "... WHERE u.TenantId = 1"
```

### Temp Table Materialization

Replace table references with temp tables and prepend `SELECT INTO` statements:

```csharp
var stmt = Stmt.Parse(
    "SELECT u.Name, o.Total FROM Users u " +
    "JOIN Orders o ON u.Id = o.UserId " +
    "WHERE u.Active = 1 AND o.Total > 100");

Script script = stmt.ReplaceWithTempTables("Users", "Orders");
script.ToSource();
// SELECT Name, Active INTO #Users FROM Users WHERE Active = 1;
// SELECT Total, UserId INTO #Orders FROM Orders WHERE Total > 100;
// SELECT u.Name, o.Total FROM #Users u JOIN #Orders o ON u.Id = o.UserId
```

The transformation narrows columns to only those referenced in the query and pushes WHERE predicates into the `SELECT INTO` when safe.

### Collect References

```csharp
// All table references and joins
TableReferences refs = stmt.CollectTableReferences();
foreach (TableReference table in refs.Tables)
{
    Console.WriteLine(table.TableName.ObjectName.Name);
}

// Column references with scope/clause filtering
IReadOnlyList<Expr.ColumnIdentifier> columns =
    stmt.CollectColumnReferences(
        ColumnReferenceScope.All,
        ColumnReferenceClause.Where | ColumnReferenceClause.Select);
```

## Build

```bash
dotnet build TSQL.slnx
```

## Test

```bash
dotnet test TSQL.Tests/TSQL.Tests.csproj
```

## Benchmarks

See [TSQL.Benchmarks/README.md](TSQL.Benchmarks/README.md) for detailed benchmark documentation.

```bash
# Run all benchmarks
dotnet run --project TSQL.Benchmarks/TSQL.Benchmarks.csproj -c Release -- --filter *
```
