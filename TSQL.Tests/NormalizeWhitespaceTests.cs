using TSQL.AST;

namespace TSQL.Tests
{
    public class NormalizeWhitespaceTests
    {
        [Fact]
        public void NormalizeWhitespace_CollapsesTabs()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT\t*\tFROM\tT");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT * FROM T", stmt.ToSource());
        }

        [Fact]
        public void NormalizeWhitespace_CollapsesNewlines()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT *\n FROM T\n WHERE x = 1");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT * FROM T WHERE x = 1", stmt.ToSource());
        }

        [Fact]
        public void NormalizeWhitespace_CollapsesMixedWhitespaceRuns()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT   *  \t \n  FROM   T");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT * FROM T", stmt.ToSource());
        }

        [Fact]
        public void NormalizeWhitespace_PreservesBlockComments()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT * /* inline */ FROM T");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT * /* inline */ FROM T", stmt.ToSource());
        }

        [Fact]
        public void NormalizeWhitespace_LineCommentFollowedByNewline()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT *\n-- note\n\tFROM T");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT * -- note\nFROM T", stmt.ToSource());
        }

        [Fact]
        public void NormalizeWhitespace_StackedLineComments()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT *\n-- a\n-- b\n  FROM T");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT * -- a\n-- b\nFROM T", stmt.ToSource());
        }

        [Fact]
        public void NormalizeWhitespace_PreservesStringLiterals()
        {
            Stmt.Select stmt = Stmt.ParseSelect("SELECT 'foo   bar'\tFROM T");

            stmt.NormalizeWhitespace();

            Assert.Equal("SELECT 'foo   bar' FROM T", stmt.ToSource());
        }

    }
}
