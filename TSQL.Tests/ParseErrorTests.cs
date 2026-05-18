namespace TSQL.Tests
{
    public class ParseErrorTests
    {
        [Fact]
        public void TooLargeLiteral_ParseError_HasSpanLength()
        {
            var scanner = new Scanner("SELECT 99999999999999999999");

            ParseError ex = Assert.Throws<ParseError>(() => scanner.ScanTokens());

            Assert.Equal(20, ex.Length);
        }

        [Fact]
        public void UnexpectedCharacter_ParseError_HasSingleCharLength()
        {
            var scanner = new Scanner("SELECT é");

            ParseError ex = Assert.Throws<ParseError>(() => scanner.ScanTokens());

            Assert.Equal(1, ex.Length);
        }

        [Fact]
        public void ToString_TooLargeLiteral_RendersCaretDiagnostic()
        {
            var scanner = new Scanner("SELECT 99999999999999999999");

            ParseError ex = Assert.Throws<ParseError>(() => scanner.ScanTokens());
            string text = ex.ToString();

            // The literal starts at column index 7, so the caret line is
            // "   | " + 7 spaces + 20 carets.
            Assert.Contains("TSQL.ParseError: Numeric literal too large: 99999999999999999999", text);
            Assert.Contains("  --> line 1, column 8", text);
            Assert.Contains(" 1 | SELECT 99999999999999999999", text);
            Assert.Contains("   | " + new string(' ', 7) + new string('^', 20), text);
        }

        [Fact]
        public void ToString_MultiLineSql_ShowsOffendingLine()
        {
            var scanner = new Scanner("SELECT 1\nFROM 99999999999999999999");

            ParseError ex = Assert.Throws<ParseError>(() => scanner.ScanTokens());
            string text = ex.ToString();

            Assert.Contains("  --> line 2, column 6", text);
            Assert.Contains(" 2 | FROM 99999999999999999999", text);
            Assert.Contains("   | " + new string(' ', 5) + new string('^', 20), text);
        }

        [Fact]
        public void ToString_UnexpectedCharacter_RendersSingleCaret()
        {
            var scanner = new Scanner("SELECT é");

            ParseError ex = Assert.Throws<ParseError>(() => scanner.ScanTokens());
            string text = ex.ToString();

            Assert.Contains(" 1 | SELECT é", text);
            Assert.Contains("   |        ^", text);
            Assert.DoesNotContain("^^", text);
        }

        [Fact]
        public void ToString_NoLocation_FallsBackToBaseException()
        {
            ParseError ex = new ParseError("plain failure");
            string text = ex.ToString();

            Assert.Contains("TSQL.ParseError: plain failure", text);
            Assert.DoesNotContain("-->", text);
        }

        [Fact]
        public void ToString_SpanPastLineEnd_ClampsCaretToLineLength()
        {
            // Column 5, length 100, on the 8-character line "SELECT 1":
            // the caret span clamps to the 3 remaining characters.
            ParseError ex = new ParseError("oversize span", 1, 5, 100, "SELECT 1");
            string text = ex.ToString();

            Assert.Contains(" 1 | SELECT 1", text);
            Assert.Contains("   |      ^^^", text);
            Assert.DoesNotContain("^^^^", text);
        }
    }
}
