using System;
using TSQL.StandardLibrary.Visitors;

namespace TSQL.Tests
{
    public class JoinAppenderTests
    {
        private static Stmt Parse(string sql)
        {
            return Stmt.Parse(sql);
        }

        #region Basic INNER JOIN

        [Fact]
        public void AddJoin_InnerJoin_AppendsToFromClause()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u INNER JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        [Fact]
        public void AddJoin_BareJoin_DefaultsToInner()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        #endregion

        #region Different Join Types

        [Fact]
        public void AddJoin_LeftJoin_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("LEFT JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u LEFT JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        [Fact]
        public void AddJoin_LeftOuterJoin_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("LEFT OUTER JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u LEFT OUTER JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        [Fact]
        public void AddJoin_RightJoin_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("RIGHT JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u RIGHT JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        [Fact]
        public void AddJoin_FullOuterJoin_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("FULL OUTER JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u FULL OUTER JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        #endregion

        #region Join Hints

        [Fact]
        public void AddJoin_LoopHint_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER LOOP JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u INNER LOOP JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        [Fact]
        public void AddJoin_HashHint_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER HASH JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u INNER HASH JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        [Fact]
        public void AddJoin_MergeHint_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER MERGE JOIN Orders o ON o.UserId = u.Id");

            Assert.Equal("SELECT * FROM Users u INNER MERGE JOIN Orders o ON o.UserId = u.Id", stmt.ToSource());
        }

        #endregion

        #region Existing Joins

        [Fact]
        public void AddJoin_ExistingJoin_ChainsJoins()
        {
            string sql = "SELECT * FROM Users u INNER JOIN Orders o ON o.UserId = u.Id";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Products p ON p.OrderId = o.Id");

            Assert.Equal(
                "SELECT * FROM Users u INNER JOIN Orders o ON o.UserId = u.Id INNER JOIN Products p ON p.OrderId = o.Id",
                stmt.ToSource());
        }

        [Fact]
        public void AddJoin_MultipleJoins_ChainsAll()
        {
            string sql = "SELECT * FROM A";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("JOIN B ON B.Id = A.BId");
            stmt.AddJoin("JOIN C ON C.Id = B.CId");

            Assert.Equal(
                "SELECT * FROM A JOIN B ON B.Id = A.BId JOIN C ON C.Id = B.CId",
                stmt.ToSource());
        }

        #endregion

        #region Complex ON Conditions

        [Fact]
        public void AddJoin_ComplexOnCondition_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Orders o ON o.UserId = u.Id AND o.Status = 'Active'");

            Assert.Equal(
                "SELECT * FROM Users u INNER JOIN Orders o ON o.UserId = u.Id AND o.Status = 'Active'",
                stmt.ToSource());
        }

        [Fact]
        public void AddJoin_OnConditionWithOr_ParsesCorrectly()
        {
            string sql = "SELECT * FROM Users u";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("LEFT JOIN Orders o ON o.UserId = u.Id OR o.AdminId = u.Id");

            Assert.Equal(
                "SELECT * FROM Users u LEFT JOIN Orders o ON o.UserId = u.Id OR o.AdminId = u.Id",
                stmt.ToSource());
        }

        #endregion

        #region UNION / Set Operations

        [Fact]
        public void AddJoin_Union_AddsToBothBranches()
        {
            string sql = "SELECT * FROM Users UNION SELECT * FROM Admins";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Roles r ON r.UserId = Users.Id");

            Assert.Equal(
                "SELECT * FROM Users INNER JOIN Roles r ON r.UserId = Users.Id UNION SELECT * FROM Admins INNER JOIN Roles r ON r.UserId = Users.Id",
                stmt.ToSource());
        }

        #endregion

        #region QueryScope Control

        [Fact]
        public void AddJoin_OutermostQueryOnly_DoesNotEnterSubqueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Users) sub";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Orders o ON o.Id = sub.OrderId", QueryScope.OutermostQuery);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users) sub INNER JOIN Orders o ON o.Id = sub.OrderId",
                stmt.ToSource());
        }

        [Fact]
        public void AddJoin_FromSubqueries_OnlyModifiesSubqueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Users u) sub";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Orders o ON o.UserId = u.Id", QueryScope.FromSubqueries);

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users u INNER JOIN Orders o ON o.UserId = u.Id) sub",
                stmt.ToSource());
        }

        #endregion

        #region Target Table Overload

        [Fact]
        public void AddJoin_TargetTable_OnlyModifiesMatchingQueries()
        {
            string sql = "SELECT * FROM (SELECT * FROM Users u) sub1 JOIN (SELECT * FROM Products p) sub2 ON sub1.Id = sub2.UserId";
            Stmt stmt = Parse(sql);

            stmt.AddJoin("INNER JOIN Orders o ON o.UserId = u.Id", "Users");

            Assert.Equal(
                "SELECT * FROM (SELECT * FROM Users u INNER JOIN Orders o ON o.UserId = u.Id) sub1 JOIN (SELECT * FROM Products p) sub2 ON sub1.Id = sub2.UserId",
                stmt.ToSource());
        }

        #endregion

        #region Error Cases

        [Fact]
        public void AddJoin_NoFromClause_ThrowsInvalidOperation()
        {
            string sql = "SELECT 1";
            Stmt stmt = Parse(sql);

            Assert.Throws<InvalidOperationException>(() => stmt.AddJoin("INNER JOIN Orders o ON o.Id = 1"));
        }

        #endregion
    }
}
