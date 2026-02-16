# TSQL

A T-SQL parser library targeting .NET Standard 2.0.

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