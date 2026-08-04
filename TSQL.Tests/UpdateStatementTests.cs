using TSQL.AST;

namespace TSQL.Tests
{
    public class UpdateStatementTests
    {
        #region Round-trip

        [Theory]
        [InlineData("UPDATE T SET a = 1")]
        [InlineData("UPDATE T SET a = 1, b = 'x'")]
        [InlineData("UPDATE T SET a = @P0 WHERE b IN (@P1, @P2)")]
        [InlineData("UPDATE TOP (5) T SET a = 1")]
        [InlineData("UPDATE T SET a = U.b FROM T INNER JOIN U ON T.id = U.id WHERE U.x = 1")]
        public void ParseUpdate_RoundTrips(string source)
        {
            Stmt.Update stmt = Stmt.ParseUpdate(source);

            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseUpdate_WithCte_RoundTrips()
        {
            string source = "WITH C AS (SELECT a FROM U) UPDATE T SET a = 1 WHERE a IN (SELECT a FROM C)";
            Stmt stmt = Stmt.Parse(source);

            Assert.IsType<Stmt.Update>(stmt);
            Assert.Equal(source, stmt.ToSource());
        }

        #endregion

        #region SqlWalker

        [Fact]
        public void SqlWalker_FindsInPredicate_InsideUpdateWhereClause()
        {
            // This is the case the whole task exists for: the IN-list rewriter walks
            // UPDATE statements looking for IN predicates to rewrite as OPENJSON
            // subqueries. If SqlWalker doesn't reach the WHERE clause of an UPDATE,
            // the rewriter silently does nothing and the oversized IN list still
            // fails against SQL Server.
            Stmt stmt = Stmt.Parse("UPDATE T SET a = 1 WHERE b IN (@P0, @P1)");

            InPredicateCounter counter = new InPredicateCounter();
            counter.Walk(stmt);

            Assert.Equal(1, counter.InCount);
        }

        [Fact]
        public void SqlWalker_FindsInPredicate_InsideUpdateSetValueExpression()
        {
            // Extra surface versus DELETE: an assignment's *value* expression can itself
            // contain a subquery with an IN predicate. If VisitUpdate doesn't walk the
            // assignment values, the rewriter misses IN lists hiding in a SET clause.
            Stmt stmt = Stmt.Parse("UPDATE T SET a = (SELECT MAX(x) FROM U WHERE U.id IN (@P0, @P1))");

            InPredicateCounter counter = new InPredicateCounter();
            counter.Walk(stmt);

            Assert.Equal(1, counter.InCount);
        }

        private class InPredicateCounter : SqlWalker
        {
            public int InCount { get; private set; }

            protected override void VisitIn(Predicate.In pred)
            {
                InCount++;
                base.VisitIn(pred);
            }
        }

        #endregion
    }
}
