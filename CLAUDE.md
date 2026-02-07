# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build all projects
dotnet build TSQL.slnx

# Run tests
dotnet test TSQL.Tests/TSQL.Tests.csproj

# Run specific test class
dotnet test TSQL.Tests/TSQL.Tests.csproj --filter "ClassName=ParserTests"

# Run benchmarks
dotnet run --project TSQL.Benchmarks/TSQL.Benchmarks.csproj -c Release
```

## Architecture

This is a T-SQL parser library targeting .NET Standard 2.0 for broad compatibility.

### Parsing Pipeline

1. **Scanner** (`Scanner.cs`) - Tokenizes SQL source into `SourceToken` list, preserving trivia (whitespace, comments)
2. **Parser** (`Parser.cs`) - Recursive descent parser that builds an AST from tokens

### AST Structure

- `SyntaxElement` - Base class for all AST nodes
- `Expr` (`AST/Expr.cs`) - Expression nodes: literals, column identifiers, binary/unary operations, function calls, window functions, subqueries
- `Stmt` (`AST/Stmt.cs`) - Statement nodes: SELECT with CTEs
- `Predicate` (`AST/Predicates/Predicate.cs`) - WHERE clause predicates: comparison, LIKE, BETWEEN, IN, EXISTS, etc.
- `SyntaxElementList<T>` - Generic list that preserves separator tokens for round-trip fidelity

### Visitor Pattern

All AST nodes implement `Accept<T>()` for visitor traversal:
- `Expr.Visitor<T>` - Expression visitor interface
- `Stmt.Visitor<T>` - Statement visitor interface
### Trivia Preservation

Tokens carry leading/trailing trivia (whitespace, comments) enabling exact source reconstruction via `ToSource()`.

### Source Generation

`TSQL.Generators` contains a Roslyn source generator (`KeywordDictionaryGenerator`) that auto-generates keyword dictionary from `TokenType` enum, producing `Scanner.Keywords.g.cs`.

### Performance Patterns

- `StringSlice` - Lightweight struct for deferred string allocation during lexing
- Keywords matched by length to minimize comparisons

## Project Structure

- **TSQL** - Core parser library (.NET Standard 2.0)
- **TSQL.Generators** - Roslyn source generator for keyword dictionary
- **TSQL.StandardLibrary** - Visitor implementations
- **TSQL.Tests** - xUnit tests for scanner, parser, and AST
- **TSQL.Benchmarks** - BenchmarkDotNet performance profiling

## Development Workflow

### Git Repository
This project uses Git for version control with `master` as the main branch.

**Commit guidelines:**
- Do NOT add "Co-Authored-By: Claude" to commit messages
- Write clear, concise commit messages describing the "why" not the "what"
- Run tests before committing: `dotnet test`

### Confirm Bugs with Tests First
When a bug is discovered, **write a failing test that reproduces it before fixing**. This:
- Proves the bug exists and is understood
- Prevents regression
- Validates the fix actually works

Example workflow:
1. Bug found: downloads fail when API starts from different directory
2. Write test that uses relative paths (like production) → test fails
3. Apply fix (`Path.GetFullPath()`)
4. Test passes → bug confirmed fixed

### Update CLAUDE.md After Each Session
At the end of each development session, update this file with:
- **New lessons learned** - gotchas, bugs encountered, solutions found
- **Project structure changes** - new files, moved files, deleted files
- **New patterns or conventions** - if new architectural decisions were made
- **Updated commands** - if build/test/run commands changed

This keeps the documentation current and helps future sessions start with full context.

## C# Design Principles

### 1. The Embedded Design Principle

Design should be apparent in code. Make programmer intent recoverable.

- Create explicit types/classes for domain concepts rather than using primitives
- Name things to reveal design intent, not implementation details
- Prefer hierarchical organization within files (sections/regions) before splitting into multiple files
- Code must be correct at the specification level, not just "works for current inputs"
- When a concept appears in multiple conditionals, extract it into a first-class type

```csharp
// Bad: Primitive obsession hides design intent
public void CreateUser(string email, string visibleName, string loginName) { }
public decimal CalculatePrice(decimal amount, string currency) { }

// Good: Domain concepts are explicit types
public void CreateUser(Email email, DisplayName visibleName, LoginName loginName) { }
public decimal CalculatePrice(Money amount) { }

public readonly record struct Email(string Value)
{
    public Email(string value) : this(value)
    {
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format");
    }
}

public readonly record struct Money(decimal Amount, Currency Currency);
```

### 2. The Representable/Valid Principle

Make invalid states unrepresentable. Use the type system to prevent bugs at compile time.

**MIRO framework:**
- **Missing:** Every abstract state must have a concrete representation (use nullable types, Option patterns)
- **Illegal:** Structure types so impossible states cannot be constructed
- **Redundant:** Avoid multiple ways to represent the same logical state
- **Overloaded:** One concrete state should not represent multiple abstract meanings (e.g., distinguish "empty list" from "not yet loaded")

**Avoid boolean blindness:** Prefer enums/discriminated unions over booleans. Booleans lose their meaning (provenance) as they flow through code.

```csharp
// Bad: Invalid states are representable
public class Order
{
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }  // Can set DeliveredDate without ShippedDate!
}

// Good: State machine makes invalid states impossible
public abstract record OrderState;
public record PendingOrder() : OrderState;
public record ShippedOrder(DateTime ShippedDate) : OrderState;
public record DeliveredOrder(DateTime ShippedDate, DateTime DeliveredDate) : OrderState;

// Bad: Boolean blindness - what does 'true' mean here?
public void ProcessUser(bool isActive, bool isVerified, bool isAdmin) { }
bool result = CheckPermission(user);  // What permission?

// Good: Enums preserve meaning
public enum UserStatus { Active, Inactive, Suspended }
public enum VerificationState { Unverified, EmailVerified, FullyVerified }
public enum Permission { Read, Write, Admin }

public void ProcessUser(UserStatus status, VerificationState verification, Permission permission) { }

// Bad: Overloaded state - null means what?
public List<Item>? Items { get; set; }  // null = not loaded? empty? error?

// Good: Explicit loading states
public abstract record LoadState<T>;
public record NotLoaded<T>() : LoadState<T>;
public record Loading<T>() : LoadState<T>;
public record Loaded<T>(T Data) : LoadState<T>;
public record LoadError<T>(string Message) : LoadState<T>;

public LoadState<List<Item>> Items { get; set; } = new NotLoaded<List<Item>>();
```

### 3. The Data Over Code Principle

Encode design in data structures rather than control flow.

- Prefer declarative data that describes *what* over imperative code that does *how*
- Abstractions must be sound (mapping works reliably) and precise (captures relevant details)
- Modules should hide design decisions likely to change (information hiding)
- Consolidate control points in data structures to reduce coupling

```csharp
// Bad: Logic scattered in control flow
public decimal CalculateDiscount(Customer customer, Order order)
{
    if (customer.Type == CustomerType.Gold && order.Total > 100)
        return 0.20m;
    else if (customer.Type == CustomerType.Gold)
        return 0.15m;
    else if (customer.Type == CustomerType.Silver && order.Total > 100)
        return 0.10m;
    else if (customer.Type == CustomerType.Silver)
        return 0.05m;
    return 0;
}

// Good: Rules encoded in data
public record DiscountRule(CustomerType Type, decimal MinOrderTotal, decimal DiscountRate);

private static readonly DiscountRule[] DiscountRules =
[
    new(CustomerType.Gold, 100m, 0.20m),
    new(CustomerType.Gold, 0m, 0.15m),
    new(CustomerType.Silver, 100m, 0.10m),
    new(CustomerType.Silver, 0m, 0.05m),
];

public decimal CalculateDiscount(Customer customer, Order order) =>
    DiscountRules
        .Where(r => r.Type == customer.Type && order.Total >= r.MinOrderTotal)
        .Select(r => r.DiscountRate)
        .FirstOrDefault();

// Good: Declarative validation rules
public record ValidationRule<T>(Func<T, bool> Predicate, string ErrorMessage);

private static readonly ValidationRule<User>[] UserValidationRules =
[
    new(u => !string.IsNullOrEmpty(u.Name), "Name is required"),
    new(u => u.Age >= 18, "Must be 18 or older"),
    new(u => u.Email.Contains('@'), "Invalid email format"),
];

public IEnumerable<string> Validate(User user) =>
    UserValidationRules
        .Where(rule => !rule.Predicate(user))
        .Select(rule => rule.ErrorMessage);
```

### 4. The Simplicity Principle

Complexity is the central problem in software design. Fight it at every turn. The goal is not just working code — it is code that is *easy to understand and modify*.

**Deep modules over shallow ones.** A well-designed module has a simple interface that hides a complex implementation. A method that does significant work behind a clean signature is worth more than ten tiny methods that scatter logic and force callers to assemble the pieces. Before splitting a function, ask: does this make the system simpler for the *reader*, or just shorter per function?

```csharp
// Shallow: caller must orchestrate multiple steps, complexity leaks out
var scanner = new Scanner(sql);
var tokens = scanner.ScanTokens();
var parser = new Parser(tokens);
var ast = parser.ParseSelect();
var source = ast.ToSource();

// Deep: one method, clear intent, all complexity hidden inside
string source = SqlFormatter.Format(sql);
```

**Define errors out of existence.** The best way to deal with edge cases is to define the problem so they don't exist. Design abstractions where the "special cases" are naturally handled by the normal code path, rather than requiring separate detection and handling.

```csharp
// Bad: edge cases require special handling everywhere
public SyntaxElementList<T> ParseList()
{
    // Must handle: empty list, single item, trailing comma, null separators...
    if (IsAtEnd()) return new SyntaxElementList<T>();
    var first = ParseItem();
    if (!Check(COMMA)) return new SyntaxElementList<T>(first);
    // ...more special cases
}

// Good: define the structure so one code path handles all cases
// SyntaxElementList<T> stores items and separators together —
// an empty list, single item, and multi-item list are all just
// "zero or more (separator, item) pairs after an optional first item."
// No special cases needed.
```

**Information hiding.** Each module should encapsulate a design decision — a data format, an algorithm, a policy — so that changing it does not ripple through the codebase. When internal details leak into the interface (through parameter types, naming, or documentation), every caller becomes coupled to the implementation.

**Prefer somewhat general-purpose.** When building a module, make its interface slightly more general than the immediate use case demands, without adding unused functionality. A general-purpose interface is often *simpler* than a special-purpose one because it replaces multiple narrow interfaces with one clean one.

```csharp
// Too special-purpose: one method per hint category
public QueryHint ParseSimpleHint() { ... }
public QueryHint ParseValueHint() { ... }
public QueryHint ParseTwoTokenHint() { ... }

// More general: single entry point, dispatches internally
public QueryHint ParseQueryHint() { ... }
```

**Tactical vs. strategic programming.** Working code is necessary but not sufficient. Every change should leave the system a little cleaner than you found it. Tactical programming — "just make it work, clean up later" — accumulates complexity that never gets cleaned up. Think about the *right* design before writing code, even when the quick fix is obvious.

**Say no to complexity you don't need.** If there are two ways to solve a problem and one is simpler, pick the simpler one. Don't add layers of abstraction for a single use case. Don't add configuration for things that can be constants. Don't add indirection that exists only to "keep options open." Wait until a pattern repeats naturally before extracting it. Three similar lines are better than a premature abstraction.
