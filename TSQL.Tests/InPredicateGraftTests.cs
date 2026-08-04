using TsqlPredicate = TSQL.AST.Predicate;

namespace TSQL.Tests
{
    public class InPredicateGraftTests
    {
        private const string Json = "SELECT v FROM OPENJSON(@__inlist0) WITH (v INT '$')";

        private static TsqlPredicate.In DonorSubquerySource() =>
            (TsqlPredicate.In)TsqlPredicate.ParsePredicate("x IN (" + Json + ")");

        [Fact]
        public void ParsePredicate_ProducesInPredicateWithSubquery()
        {
            TsqlPredicate.In pred = DonorSubquerySource();

            Assert.NotNull(pred.Subquery);
            Assert.Equal("x IN (" + Json + ")", pred.ToSource());
        }

        [Fact]
        public void GraftedSubquery_ReplacesValueList()
        {
            Stmt stmt = Stmt.Parse("SELECT c.NAME FROM CONTAINER c WHERE c.CONT_ID IN (@P0, @P1, @P2)");

            InFinder finder = new InFinder();
            finder.Walk(stmt);
            finder.Found[0].Subquery = DonorSubquerySource().Subquery;
            finder.Found[0].ValueList = null;

            Assert.Equal(
                "SELECT c.NAME FROM CONTAINER c WHERE c.CONT_ID IN (" + Json + ")",
                stmt.ToSource());
        }

        [Fact]
        public void GraftedSubquery_PreservesNegation()
        {
            Stmt stmt = Stmt.Parse("SELECT c.NAME FROM CONTAINER c WHERE c.CONT_ID NOT IN (@P0, @P1)");

            InFinder finder = new InFinder();
            finder.Walk(stmt);
            finder.Found[0].Subquery = DonorSubquerySource().Subquery;
            finder.Found[0].ValueList = null;

            Assert.Equal(
                "SELECT c.NAME FROM CONTAINER c WHERE c.CONT_ID NOT IN (" + Json + ")",
                stmt.ToSource());
        }

        [Fact]
        public void Walker_ReachesInPredicatesInsideCtesAndSubqueries()
        {
            Stmt stmt = Stmt.Parse(
                "WITH X AS (SELECT a FROM T WHERE a IN (@P0, @P1)) " +
                "SELECT * FROM X WHERE a IN (SELECT b FROM U WHERE b IN (@P2, @P3))");

            InFinder finder = new InFinder();
            finder.Walk(stmt);

            Assert.Equal(3, finder.Found.Count);
        }

        private class InFinder : SqlWalker
        {
            public readonly List<TsqlPredicate.In> Found = new List<TsqlPredicate.In>();

            protected override void VisitIn(TsqlPredicate.In pred)
            {
                Found.Add(pred);
                base.VisitIn(pred);
            }
        }
    }
}
