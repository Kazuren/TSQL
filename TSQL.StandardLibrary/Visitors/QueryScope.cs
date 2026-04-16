using System;

namespace TSQL.StandardLibrary.Visitors
{
    [Flags]
    public enum QueryScope
    {
        None = 0,
        OutermostQuery = 1,
        Ctes = 2,
        FromSubqueries = 4,
        InSubqueries = 8,
        ExistsSubqueries = 16,
        ScalarSubqueries = 32,
        AllSubqueries = Ctes | FromSubqueries | InSubqueries | ExistsSubqueries | ScalarSubqueries,
        All = OutermostQuery | AllSubqueries
    }
}
