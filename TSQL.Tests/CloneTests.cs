using static TSQL.Expr;

namespace TSQL.Tests
{
    public class CloneTests
    {
        #region Round-trip over sample statements

        // Clone must reproduce the source exactly — trivia included. Any node type
        // whose clone drops a token or a child fails this theory immediately.
        [Theory]
        // Literals, operators, identifiers
        [InlineData("SELECT 'hello' FROM T")]
        [InlineData("SELECT 1, 2.5, NULL FROM T")]
        [InlineData("SELECT (a + b) * c FROM T")]
        [InlineData("SELECT a + b * c FROM T")]
        [InlineData("SELECT ~a * b FROM T")]
        [InlineData("SELECT a * b & c FROM T")]
        [InlineData("SELECT d.s.o.c FROM T")]
        [InlineData("SELECT [a], b FROM [dbo].[T1]")]
        [InlineData("SELECT *, a FROM T")]
        [InlineData("SELECT T.* FROM T")]
        [InlineData("SELECT @x FROM T")]
        // Aliases
        [InlineData("SELECT a AS alias FROM T")]
        [InlineData("SELECT a alias FROM T")]
        [InlineData("SELECT a AS 'MyAlias' FROM T")]
        [InlineData("SELECT alias = a FROM T")]
        [InlineData("SELECT REQUESTID [REQUEST ID] FROM T")]
        // DISTINCT / TOP / INTO
        [InlineData("SELECT DISTINCT a FROM T")]
        [InlineData("SELECT TOP 10 PERCENT * FROM T")]
        [InlineData("SELECT TOP (10) WITH TIES * FROM T")]
        [InlineData("SELECT col1 INTO #Temp FROM T")]
        // Functions and window functions
        [InlineData("SELECT COUNT(*) FROM T")]
        [InlineData("SELECT COUNT(DISTINCT a) FROM T")]
        [InlineData("SELECT COALESCE(a, b, c) FROM T")]
        [InlineData("SELECT STRING_AGG(name, ', ') WITHIN GROUP (ORDER BY name) FROM T")]
        [InlineData("SELECT RANK() OVER (PARTITION BY dept ORDER BY salary DESC) FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y ROWS BETWEEN 2 PRECEDING AND CURRENT ROW) FROM T")]
        [InlineData("SELECT SUM(x) OVER (ORDER BY y RANGE BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING) FROM T")]
        [InlineData("SELECT AVG(price) OVER () FROM T")]
        // CASE / CAST / CONVERT / IIF / COLLATE / AT TIME ZONE / method calls / OPENXML
        [InlineData("SELECT CASE x WHEN 1 THEN 'a' WHEN 2 THEN 'b' ELSE 'z' END")]
        [InlineData("SELECT CASE WHEN x > 1 THEN 'a' WHEN x > 2 THEN 'b' ELSE 'z' END")]
        [InlineData("SELECT CAST(x AS VARCHAR(50))")]
        [InlineData("SELECT CONVERT(VARCHAR(10), x, 121)")]
        [InlineData("SELECT TRY_CAST(x AS INT)")]
        [InlineData("SELECT IIF(a > 0, 'yes', 'no') FROM T")]
        [InlineData("SELECT a COLLATE Latin1_General_CI_AS FROM T")]
        [InlineData("SELECT created AT TIME ZONE 'UTC' AT TIME ZONE 'PST' FROM T")]
        [InlineData("SELECT @xml.value(N'(/root)[1]', N'INT')")]
        [InlineData("SELECT STUFF((SELECT N',' + Name FROM T FOR XML PATH(N''), TYPE).value(N'.', N'NVARCHAR(MAX)'), 1, 1, N'')")]
        [InlineData("SELECT OPENXML(@idoc, '/root/row', 2) WITH (col1 varchar(50), col2 int) FROM T")]
        // Predicates
        [InlineData("SELECT a FROM T WHERE a = 1 AND b = 2 OR c = 3")]
        [InlineData("SELECT a FROM T WHERE NOT a = 1")]
        [InlineData("SELECT a FROM T WHERE (a = 1)")]
        [InlineData("SELECT a FROM T WHERE a LIKE '%test%' ESCAPE '\\'")]
        [InlineData("SELECT a FROM T WHERE a BETWEEN 1 AND 10")]
        [InlineData("SELECT a FROM T WHERE a IS NOT NULL")]
        [InlineData("SELECT a FROM T WHERE a IN (1, 2, 3)")]
        [InlineData("SELECT a FROM T WHERE a IN (SELECT b FROM T2)")]
        [InlineData("SELECT a FROM T WHERE EXISTS (SELECT 1 FROM T2)")]
        [InlineData("SELECT a FROM T WHERE a > = ALL (SELECT b FROM T2)")]
        [InlineData("SELECT a FROM T WHERE a < > ANY (SELECT b FROM T2)")]
        [InlineData("SELECT a FROM T WHERE CONTAINS((col1, col2), 'test')")]
        [InlineData("SELECT a FROM T WHERE CONTAINS(a, 'test', LANGUAGE 1033)")]
        [InlineData("SELECT a FROM T WHERE FREETEXT(col1, 'search')")]
        // Table sources
        [InlineData("SELECT a FROM MyTable t")]
        [InlineData("SELECT a FROM @TempTable AS t")]
        [InlineData("SELECT a FROM MyDb.dbo.MyTable")]
        [InlineData("SELECT a FROM (SELECT b FROM T) AS sub")]
        [InlineData("SELECT a FROM (SELECT 1, 2) AS t(col1, col2)")]
        [InlineData("SELECT a FROM T1 INNER JOIN T2 ON T1.id = T2.id INNER JOIN T3 ON T2.id = T3.id")]
        [InlineData("SELECT a FROM T1 LEFT MERGE JOIN T2 ON T1.id = T2.id")]
        [InlineData("SELECT a FROM T1 CROSS JOIN T2")]
        [InlineData("SELECT a FROM T1 CROSS APPLY (SELECT b FROM T2 WHERE T2.id = T1.id) AS sub")]
        [InlineData("SELECT a FROM T1 OUTER APPLY (SELECT b FROM T2) AS sub")]
        [InlineData("SELECT a FROM (T1 INNER JOIN T2 ON T1.id = T2.id)")]
        [InlineData("SELECT a FROM T1, T2, T3")]
        [InlineData("SELECT a FROM T PIVOT (SUM(Amount) FOR Month IN (Jan, Feb, Mar)) AS pvt")]
        [InlineData("SELECT a FROM T UNPIVOT (Value FOR Quarter IN (Q1, Q2, Q3, Q4)) AS unpvt")]
        [InlineData("SELECT a FROM (VALUES (1, 'x'), (2, 'y')) AS t(id, name)")]
        [InlineData("SELECT a FROM MyFunction(1, 2) AS f")]
        [InlineData("SELECT a FROM OPENQUERY(LinkedServer, 'SELECT 1') AS oq")]
        [InlineData("SELECT a FROM OPENJSON(@p) WITH (a INT '$.id', b NVARCHAR(50) '$.name' AS JSON) AS oj")]
        [InlineData("SELECT a FROM OPENJSON(@p) WITH (a INT) AS oj")]
        [InlineData("SELECT a FROM T WITH (NOLOCK, NOWAIT)")]
        [InlineData("SELECT a FROM T WITH (INDEX(1))")]
        [InlineData("SELECT a FROM T AS t TABLESAMPLE (10 PERCENT)")]
        [InlineData("SELECT a FROM T TABLESAMPLE SYSTEM (100 ROWS) REPEATABLE (42)")]
        [InlineData("SELECT a FROM T FOR SYSTEM_TIME BETWEEN '2020-01-01' AND '2021-01-01'")]
        // GROUP BY / HAVING
        [InlineData("SELECT a, SUM(b) FROM T GROUP BY a HAVING SUM(b) > 10")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY ROLLUP(a, b)")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY CUBE(a, b)")]
        [InlineData("SELECT a, b, SUM(c) FROM T GROUP BY GROUPING SETS((a, b), a, ())")]
        [InlineData("SELECT a, b, c, SUM(d) FROM T GROUP BY ROLLUP((a, b), c)")]
        // ORDER BY / OFFSET / FETCH
        [InlineData("SELECT a, b FROM T ORDER BY a ASC, b DESC")]
        [InlineData("SELECT a FROM T ORDER BY a ASC, b DESC OFFSET 10 ROWS FETCH NEXT 25 ROWS ONLY")]
        [InlineData("SELECT a FROM T ORDER BY a OFFSET (2 * 5) ROWS FETCH NEXT (10 + 5) ROWS ONLY")]
        // Set operations
        [InlineData("SELECT a FROM T1 UNION ALL SELECT b FROM T2")]
        [InlineData("SELECT a FROM T1 EXCEPT SELECT b FROM T2 INTERSECT SELECT c FROM T3")]
        [InlineData("(SELECT 1 UNION SELECT 2) INTERSECT SELECT 3")]
        [InlineData("SELECT a FROM T1 UNION SELECT b FROM T2 ORDER BY a OPTION (RECOMPILE)")]
        // CTEs
        [InlineData("WITH cte(x, y) AS (SELECT a, b FROM T) SELECT x, y FROM cte")]
        [InlineData("WITH c1 AS (SELECT a FROM T), c2 AS (SELECT b FROM U) SELECT a, b FROM c1, c2")]
        // FOR clause / OPTION hints
        [InlineData("SELECT a FROM T FOR BROWSE")]
        [InlineData("SELECT a FROM T FOR XML RAW('row'), ROOT('data'), ELEMENTS XSINIL, TYPE")]
        [InlineData("SELECT a FROM T FOR JSON PATH, ROOT('result'), INCLUDE_NULL_VALUES")]
        [InlineData("SELECT a FROM T OPTION (RECOMPILE, MAXDOP 1)")]
        [InlineData("SELECT a FROM T OPTION (OPTIMIZE FOR (@x = 1, @y UNKNOWN))")]
        [InlineData("SELECT a FROM T OPTION (TABLE HINT (T, NOLOCK))")]
        public void Clone_ReproducesExactSource_ForSelectStatements(string sql)
        {
            Stmt.Select stmt = Stmt.ParseSelect(sql);

            Stmt.Select clone = stmt.Clone();

            Assert.NotSame(stmt, clone);
            Assert.Equal(sql, clone.ToSource());
        }

        [Theory]
        [InlineData("INSERT INTO T (a, b, c) VALUES (1, 2, 3)")]
        [InlineData("INSERT INTO T VALUES (1, 'a'), (2, 'b'), (3, 'c')")]
        [InlineData("INSERT INTO T DEFAULT VALUES")]
        [InlineData("INSERT INTO #Temp (col1, col2) SELECT a, b FROM T")]
        [InlineData("INSERT INTO #Temp EXEC sp_GetData @Param1, @Param2")]
        [InlineData("DECLARE @X INT = 1, @Y INT = 2")]
        [InlineData("DECLARE @T TABLE (ID INT IDENTITY(1, 1) NOT NULL, Name NVARCHAR(255))")]
        [InlineData("DROP TABLE IF EXISTS dbo.T1, dbo.T2")]
        [InlineData("EXEC @ret = dbo.MyProc 1")]
        [InlineData("EXEC sp_Proc @p1 = @Var OUTPUT")]
        [InlineData("EXEC sp_Proc DEFAULT, @p2 = DEFAULT")]
        [InlineData("EXEC ('SELECT ?', @p1) AT LinkedServer")]
        [InlineData("EXEC ('SELECT 1') AS USER = 'dbo'")]
        [InlineData("BEGIN SELECT 1; SELECT 2 END")]
        public void Clone_ReproducesExactSource_ForOtherStatements(string sql)
        {
            Stmt stmt = Stmt.Parse(sql);

            Stmt clone = stmt.Clone();

            Assert.NotSame(stmt, clone);
            Assert.Equal(sql, clone.ToSource());
        }

        [Fact]
        public void Clone_PreservesCommentsAndIrregularWhitespace()
        {
            string sql = "SELECT  a,\tb /* keep me */\nFROM T -- trailing\n WHERE x\t= 1";
            Stmt.Select stmt = Stmt.ParseSelect(sql);

            Stmt.Select clone = stmt.Clone();

            Assert.Equal(sql, clone.ToSource());
        }

        #endregion

        #region Independence

        [Fact]
        public void Clone_MutatingClone_DoesNotAffectOriginal()
        {
            string sql = "SELECT a,  b FROM T WHERE x = 1 ORDER BY b";
            Stmt.Select stmt = Stmt.ParseSelect(sql);

            Stmt.Select clone = stmt.Clone();
            clone.Query.ReplaceColumns("COUNT(*)");
            clone.Query.ClearOrderBy();
            clone.NormalizeWhitespace();

            Assert.Equal(sql, stmt.ToSource());
        }

        [Fact]
        public void Clone_MutatingOriginal_DoesNotAffectClone()
        {
            string sql = "SELECT a,  b FROM T WHERE x = 1 ORDER BY b";
            Stmt.Select stmt = Stmt.ParseSelect(sql);

            Stmt.Select clone = stmt.Clone();
            stmt.Query.ReplaceColumns("COUNT(*)");
            stmt.NormalizeWhitespace();

            Assert.Equal(sql, clone.ToSource());
        }

        [Fact]
        public void Clone_Expression_ClonesIndependentSubtree()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT ISNULL(a, '') + b FROM T");
            SelectColumn column = (SelectColumn)stmt.Query.Columns[0];

            Expr clone = column.Expression.Clone();

            Assert.NotSame(column.Expression, clone);
            Assert.Equal(column.Expression.ToSource(), clone.ToSource());
        }

        [Fact]
        public void Clone_OrderByItem_ClonesIndependently()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT a FROM T ORDER BY a DESC");
            OrderByItem item = stmt.Query.OrderBy.Items[0];

            OrderByItem clone = item.Clone();

            Assert.NotSame(item, clone);
            Assert.Equal(item.ToSource(), clone.ToSource());
        }

        #endregion
    }
}
