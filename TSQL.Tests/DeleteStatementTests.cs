using TSQL.AST;

namespace TSQL.Tests
{
    public class DeleteStatementTests
    {
        #region Round-trip

        [Theory]
        [InlineData("DELETE FROM T")]
        [InlineData("DELETE T")]
        [InlineData("DELETE FROM T WHERE a = 1")]
        [InlineData("DELETE FROM T WHERE a IN (@P0, @P1)")]
        [InlineData("DELETE TOP (10) FROM T WHERE a = 1")]
        [InlineData("DELETE FROM T FROM T INNER JOIN U ON T.id = U.id WHERE U.x = 1")]
        public void ParseDelete_RoundTrips(string source)
        {
            Stmt.Delete stmt = Stmt.ParseDelete(source);

            Assert.Equal(source, stmt.ToSource());
        }

        [Fact]
        public void ParseDelete_WithCte_RoundTrips()
        {
            string source = "WITH C AS (SELECT a FROM U) DELETE FROM T WHERE a IN (SELECT a FROM C)";
            Stmt stmt = Stmt.Parse(source);

            Assert.IsType<Stmt.Delete>(stmt);
            Assert.Equal(source, stmt.ToSource());
        }

        #endregion

        #region SqlWalker

        [Fact]
        public void SqlWalker_FindsInPredicate_InsideDeleteWhereClause()
        {
            // This is the case the whole task exists for: the IN-list rewriter walks
            // DELETE statements looking for IN predicates to rewrite as OPENJSON
            // subqueries. If SqlWalker doesn't reach the WHERE clause of a DELETE,
            // the rewriter silently does nothing and the oversized IN list still
            // fails against SQL Server.
            Stmt stmt = Stmt.Parse("DELETE FROM T WHERE a IN (@P0, @P1)");

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
