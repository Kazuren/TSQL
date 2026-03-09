using TSQL;
using TSQL.AST;
using TSQL.StandardLibrary.Visitors;

// ###########################################################################
// ########################## TSQL Parser — Live Demo ########################
// ###########################################################################

PrintBanner("TSQL Parser Library — Live Demo");

while (true)
{
    ShowMenu();
    string input = Console.ReadLine()?.Trim().ToUpperInvariant() ?? "";

    if (input == "Q")
    {
        break;
    }
    else if (input == "A")
    {
        Console.Clear();
        PrintBanner("TSQL Parser Library — Live Demo");
        Demo1_ExactSourceRegeneration();
        Demo2_Parameterize();
        Demo3_AddCondition();
        Demo4_SchemaAwareCondition();
        PrintLine();
        WriteColor("All demos complete!", ConsoleColor.Green);
        Console.WriteLine();
    }
    else if (input == "1")
    {
        Console.Clear();
        PrintBanner("TSQL Parser Library — Live Demo");
        Demo1_ExactSourceRegeneration();
    }
    else if (input == "2")
    {
        Console.Clear();
        PrintBanner("TSQL Parser Library — Live Demo");
        Demo2_Parameterize();
    }
    else if (input == "3")
    {
        Console.Clear();
        PrintBanner("TSQL Parser Library — Live Demo");
        Demo3_AddCondition();
    }
    else if (input == "4")
    {
        Console.Clear();
        PrintBanner("TSQL Parser Library — Live Demo");
        Demo4_SchemaAwareCondition();
    }
    else
    {
        Console.Clear();
        PrintBanner("TSQL Parser Library — Live Demo");
    }
}

// ###########################################################################
// ########################## Menu ###############################################
// ###########################################################################

void ShowMenu()
{
    Console.WriteLine();
    WriteColor("  Choose a demo to run:", ConsoleColor.Cyan);
    Console.WriteLine();
    Console.WriteLine("    1. Exact Source Regeneration");
    Console.WriteLine("    2. Parameterize Literals");
    Console.WriteLine("    3. Add WHERE Condition");
    Console.WriteLine("    4. Schema-Aware Condition");
    Console.WriteLine();
    Console.WriteLine("    A. Run All");
    Console.WriteLine("    Q. Quit");
    Console.WriteLine();
    WriteColor("  > ", ConsoleColor.Yellow);
}

// ###########################################################################
// ########################## Demo 1: Exact Source Regeneration ##############
// ###########################################################################

void Demo1_ExactSourceRegeneration()
{
    PrintDemoHeader("1", "Exact Source Regeneration",
        "Parse SQL with comments and odd whitespace, then reproduce it exactly via ToSource().");

    string sql = @"SELECT   e.Name,       -- employee name
    e.Salary * 1.1   AS  Raise,   /* 10% raise */
    d.DeptName
FROM     Employee   e
    INNER  JOIN   Department d   ON e.DeptId = d.Id
WHERE    e.Active = 1
    AND  e.Salary  >  50000
ORDER BY e.Salary   DESC";

    var stmt = Stmt.ParseSelect(sql);
    string roundTripped = stmt.ToSource();

    PrintLabel("Original");
    PrintSqlWithAst(stmt);

    PrintLabel("ToSource()");
    PrintSqlWithAst(stmt);

    bool match = sql == roundTripped;
    PrintLabel("Exact match?");
    if (match)
    {
        WriteColor("  YES — every space, comment, and token preserved", ConsoleColor.Green);
    }
    else
    {
        WriteColor("  NO — output differs", ConsoleColor.Red);
    }
    Console.WriteLine();
}

// ###########################################################################
// ########################## Demo 2: Parameterize ###########################
// ###########################################################################

void Demo2_Parameterize()
{
    PrintDemoHeader("2", "Literal Parameterizer",
        "Replace all literals with @P0, @P1, ... and extract a parameter dictionary.");

    string sql = @"SELECT * FROM Orders
WHERE CustomerId = 42
  AND Status = 'Active'
  AND Total > 99.95
  AND Region = 'Active'";

    var stmt = Stmt.ParseSelect(sql);

    PrintLabel("Original");
    PrintSqlWithAst(stmt);

    stmt.Parameterize(out var parameters);

    PrintLabel("After Parameterize()");
    PrintSqlWithAst(stmt);

    PrintLabel("Parameter dictionary");
    foreach (var kvp in parameters)
    {
        string valueDisplay;
        if (kvp.Value is string s)
        {
            valueDisplay = $"'{s}'";
        }
        else
        {
            valueDisplay = kvp.Value.ToString()!;
        }
        Console.Write("  ");
        WriteColor(kvp.Key, ConsoleColor.Cyan);
        Console.Write(" = ");
        WriteColor(valueDisplay, ConsoleColor.Yellow);
        Console.Write($"  ({kvp.Value.GetType().Name})");
        Console.WriteLine();
    }

    PrintLabel("Note");
    Console.WriteLine("  Duplicate literals ('Active' appears twice) reuse the same parameter name.");
    Console.WriteLine();
}

// ###########################################################################
// ########################## Demo 3: AddCondition ###########################
// ###########################################################################

void Demo3_AddCondition()
{
    PrintDemoHeader("3", "AddCondition (WHERE injection)",
        "Append a WHERE clause — works whether the query already has one or not.");

    // --- Case A: Query without WHERE ---
    PrintLabel("Case A: Query with no WHERE");

    string sqlNoWhere = "SELECT * FROM Products";

    var stmtA = Stmt.ParseSelect(sqlNoWhere);

    PrintLabel("  Original");
    PrintSqlWithAst(stmtA);

    stmtA.AddCondition("CategoryId = 5");

    PrintLabel("  After AddCondition(\"CategoryId = 5\")");
    PrintSqlWithAst(stmtA);

    // --- Case B: Query with existing WHERE ---
    PrintLabel("Case B: Query with existing WHERE");

    string sqlWithWhere = @"SELECT * FROM Products
WHERE Price > 10.00";

    var stmtB = Stmt.ParseSelect(sqlWithWhere);

    PrintLabel("  Original");
    PrintSqlWithAst(stmtB);

    stmtB.AddCondition("CategoryId = 5");

    PrintLabel("  After AddCondition(\"CategoryId = 5\")");
    PrintSqlWithAst(stmtB);

    // --- Case C: Condition with OR precedence ---
    PrintLabel("Case C: Existing WHERE with OR (precedence preserved)");

    string sqlWithOr = @"SELECT * FROM Products
WHERE Price > 10.00 OR InStock = 1";

    var stmtC = Stmt.ParseSelect(sqlWithOr);

    PrintLabel("  Original");
    PrintSqlWithAst(stmtC);

    stmtC.AddCondition("CategoryId = 5");

    PrintLabel("  After AddCondition(\"CategoryId = 5\")");
    PrintSqlWithAst(stmtC);

    Console.WriteLine();
}

// ###########################################################################
// ########################## Demo 4: Schema-Aware Condition #################
// ###########################################################################

void Demo4_SchemaAwareCondition()
{
    PrintDemoHeader("4", "SchemaAwareCondition (company filter)",
        "Inject CompanyId = @CompanyId only into tables that have a CompanyId column.");

    // Simulated schema: only some tables have CompanyId
    var tablesWithCompanyId = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Orders", "Customers"
    };

    ColumnExistenceChecker columnExists = (tableName, columns) =>
    {
        return tablesWithCompanyId.Contains(tableName);
    };

    PrintLabel("Schema");
    Console.WriteLine("  Orders     — has CompanyId");
    Console.WriteLine("  Customers  — has CompanyId");
    Console.WriteLine("  Products   — no CompanyId");
    Console.WriteLine();

    string sql = @"SELECT o.OrderId, c.Name, p.ProductName
FROM Orders o
    INNER JOIN Customers c ON o.CustomerId = c.Id
    INNER JOIN Products p  ON o.ProductId  = p.Id
WHERE o.Total > 100";

    var stmt = Stmt.ParseSelect(sql);

    PrintLabel("Original");
    PrintSqlWithAst(stmt);

    stmt.AddSchemaAwareCondition("CompanyId = @CompanyId",
        new object[] { ("@CompanyId", 42) },
        columnExists,
        out var parameters);

    PrintLabel("After AddSchemaAwareCondition(\"CompanyId = @CompanyId\", ...)");
    PrintSqlWithAst(stmt);

    PrintLabel("Parameters");
    foreach (var kvp in parameters)
    {
        Console.Write("  ");
        WriteColor(kvp.Key, ConsoleColor.Cyan);
        Console.Write(" = ");
        WriteColor(kvp.Value.ToString()!, ConsoleColor.Yellow);
        Console.WriteLine();
    }

    PrintLabel("Note");
    Console.WriteLine("  Orders and Customers got the filter (prefixed with alias).");
    Console.WriteLine("  Products was skipped — it has no CompanyId column.");
    Console.WriteLine();
}

// ###########################################################################
// ########################## Syntax-Highlighted SQL #########################
// ###########################################################################

List<List<ColorSegment>> RenderColoredSqlLines(SyntaxElement element, Dictionary<Token, ConsoleColor> tokenColors)
{
    var lines = new List<List<ColorSegment>>();
    var currentLine = new List<ColorSegment>();
    lines.Add(currentLine);

    foreach (Token token in element.DescendantTokens())
    {
        // Leading trivia (whitespace, comments)
        foreach (Trivia trivia in token.LeadingTrivia)
        {
            if (trivia is Comment)
            {
                AddTextWithNewlines(trivia.Content, ConsoleColor.DarkGreen, lines, ref currentLine);
            }
            else
            {
                AddTextWithNewlines(trivia.Content, ConsoleColor.White, lines, ref currentLine);
            }
        }

        // Token lexeme — use AST-driven color
        ConsoleColor color;
        if (!tokenColors.TryGetValue(token, out color))
        {
            color = ConsoleColor.Gray;
        }
        AddTextWithNewlines(token.Lexeme, color, lines, ref currentLine);
    }

    return lines;
}

void AddTextWithNewlines(string text, ConsoleColor color,
    List<List<ColorSegment>> lines, ref List<ColorSegment> currentLine)
{
    string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
    string[] parts = normalized.Split('\n');

    for (int i = 0; i < parts.Length; i++)
    {
        if (parts[i].Length > 0)
        {
            currentLine.Add(new ColorSegment(parts[i], color));
        }
        if (i < parts.Length - 1)
        {
            currentLine = new List<ColorSegment>();
            lines.Add(currentLine);
        }
    }
}

// ###########################################################################
// ########################## AST Tree Rendering #############################
// ###########################################################################

List<List<ColorSegment>> RenderTree(TreeNode root)
{
    var lines = new List<List<ColorSegment>>();
    RenderTreeNode(root, lines, "", "", true);
    return lines;
}

void RenderTreeNode(TreeNode node, List<List<ColorSegment>> lines,
    string connector, string childPrefix, bool isRoot)
{
    var line = new List<ColorSegment>();

    if (!isRoot)
    {
        line.Add(new ColorSegment(connector, ConsoleColor.DarkGray));
    }

    if (node.Label != null)
    {
        line.Add(new ColorSegment(node.Label, node.Color));
    }

    if (node.Detail != null)
    {
        if (node.Label != null)
        {
            line.Add(new ColorSegment(": ", ConsoleColor.DarkGray));
        }
        line.Add(new ColorSegment(node.Detail, ConsoleColor.White));
    }

    lines.Add(line);

    if (node.Children != null)
    {
        for (int i = 0; i < node.Children.Count; i++)
        {
            bool isLast = i == node.Children.Count - 1;
            string nextConnector = childPrefix + (isLast ? "└── " : "├── ");
            string nextChildPrefix = childPrefix + (isLast ? "    " : "│   ");
            RenderTreeNode(node.Children[i], lines, nextConnector, nextChildPrefix, false);
        }
    }
}

// ###########################################################################
// ########################## Side-by-Side Printing ##########################
// ###########################################################################

void PrintSqlWithAst(SyntaxElement element)
{
    // Only build AST tree for Stmt nodes
    Dictionary<Token, ConsoleColor> tokenColors;
    TreeNode tree;
    if (element is Stmt stmt)
    {
        var result = AstTreeBuilder.Build(stmt);
        tree = result.Tree;
        tokenColors = result.TokenColors;
    }
    else
    {
        // Fallback for non-Stmt elements
        tokenColors = new Dictionary<Token, ConsoleColor>();
        foreach (Token token in element.DescendantTokens())
        {
            tokenColors[token] = ConsoleColor.White;
        }
        tree = TreeNode.Leaf(element.GetType().Name, null, ConsoleColor.Gray);
    }

    var sqlLines = RenderColoredSqlLines(element, tokenColors);
    var astLines = RenderTree(tree);
    PrintSideBySide(sqlLines, astLines, "SQL", "AST");
}

void PrintSideBySide(List<List<ColorSegment>> leftLines, List<List<ColorSegment>> rightLines,
    string leftHeader, string rightHeader)
{
    const int indent = 4;
    const int minWidth = 30;
    const int maxWidth = 70;
    const int gap = 2;
    string indentStr = new string(' ', indent);

    // Compute left column width from content
    int leftWidth = leftHeader.Length;
    foreach (var line in leftLines)
    {
        int len = 0;
        foreach (var seg in line)
        {
            len += seg.Text.Length;
        }
        if (len > leftWidth)
        {
            leftWidth = len;
        }
    }
    leftWidth = Math.Clamp(leftWidth + gap, minWidth, maxWidth);

    int maxRows = Math.Max(leftLines.Count, rightLines.Count);

    // Header row
    Console.Write(indentStr);
    WriteColor(leftHeader.PadRight(leftWidth), ConsoleColor.Yellow);
    WriteColor("│ ", ConsoleColor.DarkGray);
    WriteColor(rightHeader, ConsoleColor.Yellow);
    Console.WriteLine();

    // Separator row
    Console.Write(indentStr);
    WriteColor(new string('─', leftWidth), ConsoleColor.DarkGray);
    WriteColor("┼", ConsoleColor.DarkGray);
    WriteColor(new string('─', 50), ConsoleColor.DarkGray);
    Console.WriteLine();

    // Data rows
    for (int i = 0; i < maxRows; i++)
    {
        Console.Write(indentStr);
        int visibleLen = 0;

        if (i < leftLines.Count)
        {
            foreach (var seg in leftLines[i])
            {
                WriteColor(seg.Text, seg.Color);
                visibleLen += seg.Text.Length;
            }
        }

        if (visibleLen < leftWidth)
        {
            Console.Write(new string(' ', leftWidth - visibleLen));
        }

        WriteColor("│ ", ConsoleColor.DarkGray);

        if (i < rightLines.Count)
        {
            foreach (var seg in rightLines[i])
            {
                WriteColor(seg.Text, seg.Color);
            }
        }

        Console.WriteLine();
    }

    Console.WriteLine();
}

// ###########################################################################
// ########################## Console Helpers ################################
// ###########################################################################

void PrintBanner(string title)
{
    Console.WriteLine();
    int width = 70;
    string border = new string('=', width);
    string padded = title.PadLeft((width + title.Length) / 2).PadRight(width);

    WriteColor(border, ConsoleColor.Magenta);
    Console.WriteLine();
    WriteColor(padded, ConsoleColor.Magenta);
    Console.WriteLine();
    WriteColor(border, ConsoleColor.Magenta);
    Console.WriteLine();
}

void PrintDemoHeader(string number, string title, string description)
{
    PrintLine();
    WriteColor($"  Demo {number}: {title}", ConsoleColor.Cyan);
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"  {description}");
    Console.ResetColor();
    PrintLine();
    Console.WriteLine();
}

void PrintLine()
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(new string('-', 70));
    Console.ResetColor();
}

void PrintLabel(string label)
{
    WriteColor($"  [{label}]", ConsoleColor.Yellow);
    Console.WriteLine();
}

void WriteColor(string text, ConsoleColor color)
{
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ResetColor();
}

// ###########################################################################
// ########################## Types ##########################################
// ###########################################################################

record ColorSegment(string Text, ConsoleColor Color);

enum NodeCategory
{
    Select,
    Column,
    From,
    Table,
    Join,
    Where,
    Predicate,
    OrderBy,
    GroupBy,
    Literal,
    Variable,
    Identifier,
    Function,
    Default
}

class TreeNode
{
    public string Label { get; }
    public string Detail { get; }
    public ConsoleColor Color { get; }
    public List<TreeNode> Children { get; }

    public static TreeNode Leaf(string label, string detail, ConsoleColor color)
    {
        return new TreeNode(label, detail, null, color);
    }

    public TreeNode(string label, List<TreeNode> children, ConsoleColor color)
    {
        Label = label;
        Children = children;
        Color = color;
    }

    private TreeNode(string label, string detail, List<TreeNode> children, ConsoleColor color)
    {
        Label = label;
        Detail = detail;
        Children = children;
        Color = color;
    }
}

class AstTreeBuilder : SqlWalker
{
    readonly Dictionary<Token, ConsoleColor> _tokenColors = new();
    readonly Stack<List<TreeNode>> _childrenStack = new();

    TreeNode _root;

    public static (TreeNode Tree, Dictionary<Token, ConsoleColor> TokenColors) Build(Stmt stmt)
    {
        var builder = new AstTreeBuilder();
        builder._childrenStack.Push(new List<TreeNode>());
        builder.Walk(stmt);
        var topChildren = builder._childrenStack.Pop();
        if (builder._root == null && topChildren.Count > 0)
        {
            builder._root = topChildren[0];
        }
        return (builder._root ?? TreeNode.Leaf("(empty)", null, ConsoleColor.Gray), builder._tokenColors);
    }

    // ###########
    // ### Helpers
    // ###########

    static ConsoleColor Color(NodeCategory category)
    {
        switch (category)
        {
            case NodeCategory.Select: return ConsoleColor.Cyan;
            case NodeCategory.Column: return ConsoleColor.White;
            case NodeCategory.From: return ConsoleColor.DarkYellow;
            case NodeCategory.Table: return ConsoleColor.Green;
            case NodeCategory.Join: return ConsoleColor.DarkMagenta;
            case NodeCategory.Where: return ConsoleColor.Red;
            case NodeCategory.Predicate: return ConsoleColor.DarkRed;
            case NodeCategory.OrderBy: return ConsoleColor.DarkCyan;
            case NodeCategory.GroupBy: return ConsoleColor.DarkGreen;
            case NodeCategory.Literal: return ConsoleColor.Yellow;
            case NodeCategory.Variable: return ConsoleColor.Magenta;
            case NodeCategory.Identifier: return ConsoleColor.Blue;
            case NodeCategory.Function: return ConsoleColor.White;
            default: return ConsoleColor.Gray;
        }
    }

    void PaintTokens(SyntaxElement element, ConsoleColor color)
    {
        foreach (Token token in element.DescendantTokens())
        {
            _tokenColors[token] = color;
        }
    }

    void PaintKeywordToken(SyntaxElement parent, TokenType type, ConsoleColor color)
    {
        foreach (Token token in parent.DescendantTokens())
        {
            if (token.Type == type)
            {
                _tokenColors[token] = color;
                return;
            }
        }
    }

    void AddLeaf(string label, string detail, ConsoleColor color)
    {
        _childrenStack.Peek().Add(TreeNode.Leaf(label, detail, color));
    }

    void PushChildren()
    {
        _childrenStack.Push(new List<TreeNode>());
    }

    List<TreeNode> PopChildren()
    {
        return _childrenStack.Pop();
    }

    void AddBranch(string label, ConsoleColor color, List<TreeNode> children)
    {
        _childrenStack.Peek().Add(new TreeNode(label, children, color));
    }

    static string LexemeText(SyntaxElement element)
    {
        var sb = new System.Text.StringBuilder();
        bool needsSpace = false;
        foreach (Token token in element.DescendantTokens())
        {
            string lexeme = token.Lexeme;
            bool isGlue = token.Type == TokenType.DOT
                || token.Type == TokenType.LEFT_PAREN
                || token.Type == TokenType.RIGHT_PAREN;
            bool prevWasGlue = sb.Length > 0
                && (sb[sb.Length - 1] == '.' || sb[sb.Length - 1] == '(');

            if (needsSpace && !isGlue && !prevWasGlue)
            {
                sb.Append(' ');
            }
            sb.Append(lexeme);
            needsSpace = true;
        }
        return sb.ToString();
    }

    static string OperatorSymbol(Expr.ArithmeticOperator op)
    {
        switch (op)
        {
            case Expr.ArithmeticOperator.Add: return "+";
            case Expr.ArithmeticOperator.Subtract: return "-";
            case Expr.ArithmeticOperator.Multiply: return "*";
            case Expr.ArithmeticOperator.Divide: return "/";
            case Expr.ArithmeticOperator.Modulo: return "%";
            case Expr.ArithmeticOperator.BitwiseAnd: return "&";
            case Expr.ArithmeticOperator.BitwiseOr: return "|";
            case Expr.ArithmeticOperator.BitwiseXor: return "^";
            default: return "?";
        }
    }

    static string OperatorSymbol(Expr.UnaryOperator op)
    {
        switch (op)
        {
            case Expr.UnaryOperator.Negate: return "-";
            case Expr.UnaryOperator.BitwiseNot: return "~";
            default: return "?";
        }
    }

    static string OperatorSymbol(ComparisonOperator op)
    {
        switch (op)
        {
            case ComparisonOperator.Equal: return "=";
            case ComparisonOperator.NotEqual: return "<>";
            case ComparisonOperator.LessThan: return "<";
            case ComparisonOperator.LessThanOrEqual: return "<=";
            case ComparisonOperator.GreaterThan: return ">";
            case ComparisonOperator.GreaterThanOrEqual: return ">=";
            case ComparisonOperator.NotLessThan: return "!<";
            case ComparisonOperator.NotGreaterThan: return "!>";
            default: return "?";
        }
    }

    static string Truncate(string text, int maxLength)
    {
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        while (text.Contains("  "))
        {
            text = text.Replace("  ", " ");
        }
        text = text.Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }
        return text[..(maxLength - 1)] + "…";
    }

    // #################
    // ### Stmt Visitors
    // #################

    protected override void VisitSelect(Stmt.Select stmt)
    {
        if (stmt.CteStmt != null)
        {
            PaintTokens(stmt.CteStmt, Color(NodeCategory.Select));
            PushChildren();
            foreach (CteDefinition cd in stmt.CteStmt.Ctes)
            {
                PaintTokens(cd, Color(NodeCategory.Select));
                AddLeaf("CTE", cd.Name, Color(NodeCategory.Select));
            }
            AddBranch("WITH", Color(NodeCategory.Select), PopChildren());
        }
        WalkQueryExpression(stmt.Query);
    }

    // #############################
    // ### Query Expression Handling
    // #############################

    new void WalkQueryExpression(QueryExpression queryExpr)
    {
        if (queryExpr is SelectExpression selectExpr)
        {
            // ORDER BY is handled inside WalkSelectExpression
            WalkSelectExpression(selectExpr);
        }
        else if (queryExpr is SetOperation setOp)
        {
            WalkSetOperation(setOp);
            // For SetOperation, ORDER BY lives outside both branches
            if (setOp.OrderBy != null)
            {
                WalkOrderByClause(setOp.OrderBy);
            }
        }
    }

    new void WalkSelectExpression(SelectExpression se)
    {
        // Paint all tokens under SelectExpression with Select color first
        PaintTokens(se, Color(NodeCategory.Select));

        PushChildren();

        // TOP
        if (se.Top != null)
        {
            AddLeaf("TOP", Truncate(LexemeText(se.Top), 40), Color(NodeCategory.Select));
        }

        // Columns
        foreach (SelectItem item in se.Columns)
        {
            WalkSelectItem(item);
        }

        // FROM
        if (se.From != null)
        {
            WalkFromClause(se.From);
        }

        // WHERE
        if (se.Where != null)
        {
            PaintTokens(se.Where, Color(NodeCategory.Where));
            PaintKeywordToken(se, TokenType.WHERE, Color(NodeCategory.Where));
            PushChildren();
            Walk(se.Where);
            var whereChildren = PopChildren();
            AddBranch("WHERE", Color(NodeCategory.Where), whereChildren);
        }

        // GROUP BY
        if (se.GroupBy != null)
        {
            PaintTokens(se.GroupBy, Color(NodeCategory.GroupBy));
            AddLeaf("GROUP BY", Truncate(LexemeText(se.GroupBy), 50), Color(NodeCategory.GroupBy));
        }

        // HAVING
        if (se.Having != null)
        {
            PaintTokens(se.Having, Color(NodeCategory.Where));
            PaintKeywordToken(se, TokenType.HAVING, Color(NodeCategory.Where));
            PushChildren();
            Walk(se.Having);
            var havingChildren = PopChildren();
            AddBranch("HAVING", Color(NodeCategory.Where), havingChildren);
        }

        // ORDER BY (lives on QueryExpression, but display inside SelectExpression node)
        if (se.OrderBy != null)
        {
            WalkOrderByClause(se.OrderBy);
        }

        AddBranch("SelectExpression", Color(NodeCategory.Select), PopChildren());
    }

    void WalkSetOperation(SetOperation so)
    {
        PaintTokens(so, Color(NodeCategory.Select));

        string label = so.OperationType switch
        {
            SetOperationType.Union => "UNION",
            SetOperationType.UnionAll => "UNION ALL",
            SetOperationType.Intersect => "INTERSECT",
            SetOperationType.Except => "EXCEPT",
            _ => "SET_OP"
        };

        PushChildren();
        WalkQueryExpression(so.Left);
        WalkQueryExpression(so.Right);
        AddBranch(label, Color(NodeCategory.Select), PopChildren());
    }

    void WalkSelectItem(SelectItem item)
    {
        switch (item)
        {
            case SelectColumn sc:
            {
                PaintTokens(sc, Color(NodeCategory.Column));
                PushChildren();
                Walk(sc.Expression);
                if (sc.Alias != null)
                {
                    AddLeaf("Alias", sc.Alias.Lexeme, Color(NodeCategory.Column));
                }
                AddBranch("SelectColumn", Color(NodeCategory.Column), PopChildren());
                break;
            }
            case Expr.Wildcard w:
            {
                PaintTokens(w, Color(NodeCategory.Column));
                AddLeaf("Wildcard", "*", Color(NodeCategory.Column));
                break;
            }
            case Expr.QualifiedWildcard qw:
            {
                PaintTokens(qw, Color(NodeCategory.Column));
                AddLeaf("QualifiedWildcard", LexemeText(qw), Color(NodeCategory.Column));
                break;
            }
            default:
            {
                if (item is SyntaxElement se)
                {
                    PaintTokens(se, Color(NodeCategory.Column));
                    AddLeaf("SelectItem", Truncate(LexemeText(se), 50), Color(NodeCategory.Column));
                }
                break;
            }
        }
    }

    void WalkFromClause(FromClause fc)
    {
        PaintTokens(fc, Color(NodeCategory.From));

        PushChildren();
        foreach (TableSource ts in fc.TableSources)
        {
            Walk(ts);
        }
        AddBranch("FROM", Color(NodeCategory.From), PopChildren());
    }

    void WalkOrderByClause(OrderByClause ob)
    {
        PaintTokens(ob, Color(NodeCategory.OrderBy));

        PushChildren();
        foreach (OrderByItem item in ob.Items)
        {
            AddLeaf(null, Truncate(LexemeText(item), 50), Color(NodeCategory.OrderBy));
        }
        AddBranch("ORDER BY", Color(NodeCategory.OrderBy), PopChildren());
    }

    // ####################
    // ### Expr Visitors
    // ####################

    protected override void VisitBinary(Expr.Binary expr)
    {
        PaintTokens(expr, Color(NodeCategory.Identifier));

        string opLexeme = OperatorSymbol(expr.Operator);

        PushChildren();
        Walk(expr.Left);
        Walk(expr.Right);
        AddBranch($"Binary: {opLexeme}", Color(NodeCategory.Identifier), PopChildren());
    }

    protected override void VisitColumnIdentifier(Expr.ColumnIdentifier expr)
    {
        PaintTokens(expr, Color(NodeCategory.Identifier));
        AddLeaf("ColumnIdentifier", LexemeText(expr), Color(NodeCategory.Identifier));
    }

    protected override void VisitObjectIdentifier(Expr.ObjectIdentifier expr)
    {
        PaintTokens(expr, Color(NodeCategory.Identifier));
        AddLeaf("ObjectIdentifier", LexemeText(expr), Color(NodeCategory.Identifier));
    }

    protected override void VisitIntLiteral(Expr.IntLiteral expr)
    {
        PaintTokens(expr, Color(NodeCategory.Literal));
        AddLeaf("IntLiteral", LexemeText(expr), Color(NodeCategory.Literal));
    }

    protected override void VisitDecimalLiteral(Expr.DecimalLiteral expr)
    {
        PaintTokens(expr, Color(NodeCategory.Literal));
        AddLeaf("DecimalLiteral", LexemeText(expr), Color(NodeCategory.Literal));
    }

    protected override void VisitStringLiteral(Expr.StringLiteral expr)
    {
        PaintTokens(expr, Color(NodeCategory.Literal));
        AddLeaf("StringLiteral", LexemeText(expr), Color(NodeCategory.Literal));
    }

    protected override void VisitNullLiteral(Expr.NullLiteral expr)
    {
        PaintTokens(expr, Color(NodeCategory.Literal));
        AddLeaf("NullLiteral", "NULL", Color(NodeCategory.Literal));
    }

    protected override void VisitVariable(Expr.Variable expr)
    {
        PaintTokens(expr, Color(NodeCategory.Variable));
        AddLeaf("Variable", expr.Name, Color(NodeCategory.Variable));
    }

    protected override void VisitWildcard(Expr.Wildcard expr)
    {
        PaintTokens(expr, Color(NodeCategory.Column));
        AddLeaf("Wildcard", "*", Color(NodeCategory.Column));
    }

    protected override void VisitQualifiedWildcard(Expr.QualifiedWildcard expr)
    {
        PaintTokens(expr, Color(NodeCategory.Column));
        AddLeaf("QualifiedWildcard", LexemeText(expr), Color(NodeCategory.Column));
    }

    protected override void VisitFunctionCall(Expr.FunctionCall expr)
    {
        PaintTokens(expr, Color(NodeCategory.Function));

        PushChildren();
        foreach (Expr arg in expr.Arguments)
        {
            Walk(arg);
        }
        string funcName = LexemeText(expr.Callee);
        AddBranch($"FunctionCall: {funcName}", Color(NodeCategory.Function), PopChildren());
    }

    protected override void VisitWindowFunction(Expr.WindowFunction expr)
    {
        PaintTokens(expr, Color(NodeCategory.Function));

        PushChildren();
        foreach (Expr arg in expr.Function.Arguments)
        {
            Walk(arg);
        }
        string funcName = LexemeText(expr.Function.Callee);
        AddBranch($"WindowFunction: {funcName}", Color(NodeCategory.Function), PopChildren());
    }

    protected override void VisitSubquery(Expr.Subquery expr)
    {
        PaintTokens(expr, Color(NodeCategory.Select));
        PushChildren();
        WalkQueryExpression(expr.Query);
        AddBranch("Subquery", Color(NodeCategory.Select), PopChildren());
    }

    protected override void VisitGrouping(Expr.Grouping expr)
    {
        // Transparent — just walk through
        Walk(expr.Expression);
    }

    protected override void VisitUnary(Expr.Unary expr)
    {
        PaintTokens(expr, Color(NodeCategory.Identifier));
        PushChildren();
        Walk(expr.Right);
        string opLexeme = OperatorSymbol(expr.Operator);
        AddBranch($"Unary: {opLexeme}", Color(NodeCategory.Identifier), PopChildren());
    }

    protected override void VisitSimpleCase(Expr.SimpleCase expr)
    {
        PaintTokens(expr, Color(NodeCategory.Select));
        PushChildren();
        Walk(expr.Operand);
        foreach (var when in expr.WhenClauses)
        {
            PushChildren();
            Walk(when.Value);
            Walk(when.Result);
            AddBranch("WHEN", Color(NodeCategory.Select), PopChildren());
        }
        if (expr.ElseResult != null)
        {
            PushChildren();
            Walk(expr.ElseResult);
            AddBranch("ELSE", Color(NodeCategory.Select), PopChildren());
        }
        AddBranch("CASE", Color(NodeCategory.Select), PopChildren());
    }

    protected override void VisitSearchedCase(Expr.SearchedCase expr)
    {
        PaintTokens(expr, Color(NodeCategory.Select));
        PushChildren();
        foreach (var when in expr.WhenClauses)
        {
            PushChildren();
            Walk(when.Condition);
            Walk(when.Result);
            AddBranch("WHEN", Color(NodeCategory.Select), PopChildren());
        }
        if (expr.ElseResult != null)
        {
            PushChildren();
            Walk(expr.ElseResult);
            AddBranch("ELSE", Color(NodeCategory.Select), PopChildren());
        }
        AddBranch("CASE", Color(NodeCategory.Select), PopChildren());
    }

    protected override void VisitCast(Expr.CastExpression expr)
    {
        PaintTokens(expr, Color(NodeCategory.Function));
        PushChildren();
        Walk(expr.Expression);
        AddBranch("CAST", Color(NodeCategory.Function), PopChildren());
    }

    protected override void VisitConvert(Expr.ConvertExpression expr)
    {
        PaintTokens(expr, Color(NodeCategory.Function));
        PushChildren();
        Walk(expr.Expression);
        AddBranch("CONVERT", Color(NodeCategory.Function), PopChildren());
    }

    protected override void VisitIif(Expr.Iif expr)
    {
        PaintTokens(expr, Color(NodeCategory.Function));
        PushChildren();
        Walk(expr.Condition);
        Walk(expr.TrueValue);
        Walk(expr.FalseValue);
        AddBranch("IIF", Color(NodeCategory.Function), PopChildren());
    }

    protected override void VisitCollate(Expr.Collate expr)
    {
        PaintTokens(expr, Color(NodeCategory.Identifier));
        PushChildren();
        Walk(expr.Expression);
        AddBranch("COLLATE", Color(NodeCategory.Identifier), PopChildren());
    }

    protected override void VisitAtTimeZone(Expr.AtTimeZone expr)
    {
        PaintTokens(expr, Color(NodeCategory.Function));
        PushChildren();
        Walk(expr.Expression);
        Walk(expr.TimeZone);
        AddBranch("AT TIME ZONE", Color(NodeCategory.Function), PopChildren());
    }

    // ########################
    // ### Predicate Visitors
    // ########################

    protected override void VisitComparison(Predicate.Comparison pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));

        string opLexeme = OperatorSymbol(pred.Operator);

        PushChildren();
        Walk(pred.Left);
        Walk(pred.Right);
        AddBranch($"Comparison: {opLexeme}", Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitLike(Predicate.Like pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        string label = pred.Negated == Negation.Negated ? "NOT LIKE" : "LIKE";
        PushChildren();
        Walk(pred.Left);
        Walk(pred.Pattern);
        AddBranch(label, Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitBetween(Predicate.Between pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        string label = pred.Negated == Negation.Negated ? "NOT BETWEEN" : "BETWEEN";
        PushChildren();
        Walk(pred.Expr);
        Walk(pred.LowRangeExpr);
        Walk(pred.HighRangeExpr);
        AddBranch(label, Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitNull(Predicate.Null pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        string label = pred.Negated == Negation.Negated ? "IS NOT NULL" : "IS NULL";
        PushChildren();
        Walk(pred.Expr);
        AddBranch(label, Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitIn(Predicate.In pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        string label = pred.Negated == Negation.Negated ? "NOT IN" : "IN";
        PushChildren();
        Walk(pred.Expr);
        if (pred.Subquery != null)
        {
            Walk(pred.Subquery);
        }
        else if (pred.ValueList != null)
        {
            foreach (Expr expr in pred.ValueList)
            {
                Walk(expr);
            }
        }
        AddBranch(label, Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitExists(Predicate.Exists pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        PushChildren();
        Walk(pred.Subquery);
        AddBranch("EXISTS", Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitAnd(Predicate.And pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));

        var flatChildren = new List<Predicate>();
        FlattenAnd(pred, flatChildren);

        PushChildren();
        foreach (Predicate child in flatChildren)
        {
            Walk(child);
        }
        AddBranch("AND", Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitOr(Predicate.Or pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));

        var flatChildren = new List<Predicate>();
        FlattenOr(pred, flatChildren);

        PushChildren();
        foreach (Predicate child in flatChildren)
        {
            Walk(child);
        }
        AddBranch("OR", Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitNot(Predicate.Not pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        PushChildren();
        Walk(pred.Predicate);
        AddBranch("NOT", Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitPredicateGrouping(Predicate.Grouping pred)
    {
        // Transparent — walk through parentheses
        Walk(pred.Predicate);
    }

    protected override void VisitQuantifier(Predicate.Quantifier pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        string label = pred.QuantifierKind.ToString().ToUpper();
        PushChildren();
        Walk(pred.Left);
        Walk(pred.Subquery);
        AddBranch(label, Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitContains(Predicate.Contains pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        PushChildren();
        Walk(pred.SearchCondition);
        AddBranch("CONTAINS", Color(NodeCategory.Predicate), PopChildren());
    }

    protected override void VisitFreetext(Predicate.Freetext pred)
    {
        PaintTokens(pred, Color(NodeCategory.Predicate));
        PushChildren();
        Walk(pred.SearchCondition);
        AddBranch("FREETEXT", Color(NodeCategory.Predicate), PopChildren());
    }

    // ########################
    // ### AND/OR Flattening
    // ########################

    static void FlattenAnd(Predicate pred, List<Predicate> result)
    {
        switch (pred)
        {
            case Predicate.And and:
                FlattenAnd(and.Left, result);
                FlattenAnd(and.Right, result);
                break;
            case Predicate.Grouping g when g.Predicate is Predicate.And:
                FlattenAnd(g.Predicate, result);
                break;
            default:
                result.Add(pred);
                break;
        }
    }

    static void FlattenOr(Predicate pred, List<Predicate> result)
    {
        switch (pred)
        {
            case Predicate.Or or:
                FlattenOr(or.Left, result);
                FlattenOr(or.Right, result);
                break;
            case Predicate.Grouping g when g.Predicate is Predicate.Or:
                FlattenOr(g.Predicate, result);
                break;
            default:
                result.Add(pred);
                break;
        }
    }

    // ##########################
    // ### TableSource Visitors
    // ##########################

    protected override void VisitTableReference(TableReference source)
    {
        PaintTokens(source, Color(NodeCategory.Table));

        string detail = LexemeText(source.TableName);
        if (source.Alias != null)
        {
            detail += " " + source.Alias.Lexeme;
        }
        AddLeaf("Table", detail, Color(NodeCategory.Table));
    }

    protected override void VisitSubqueryReference(SubqueryReference source)
    {
        PaintTokens(source, Color(NodeCategory.Table));

        PushChildren();
        Walk(source.Subquery);
        string label = "(subquery)";
        if (source.Alias != null)
        {
            label += " " + source.Alias.Lexeme;
        }
        AddBranch(label, Color(NodeCategory.Table), PopChildren());
    }

    protected override void VisitTableVariableReference(TableVariableReference source)
    {
        PaintTokens(source, Color(NodeCategory.Variable));

        string detail = source.VariableName;
        if (source.Alias != null)
        {
            detail += " " + source.Alias.Lexeme;
        }
        AddLeaf("TableVariable", detail, Color(NodeCategory.Variable));
    }

    protected override void VisitQualifiedJoin(QualifiedJoin source)
    {
        PaintTokens(source, Color(NodeCategory.Join));

        string label = source.JoinType switch
        {
            JoinType.Inner => "INNER JOIN",
            JoinType.LeftOuter => "LEFT JOIN",
            JoinType.RightOuter => "RIGHT JOIN",
            JoinType.FullOuter => "FULL JOIN",
            _ => "JOIN"
        };

        PushChildren();
        Walk(source.Left);
        Walk(source.Right);

        // ON condition
        PaintTokens(source.OnCondition, Color(NodeCategory.Predicate));
        PushChildren();
        Walk(source.OnCondition);
        var onChildren = PopChildren();
        AddBranch("ON", Color(NodeCategory.Join), onChildren);

        AddBranch(label, Color(NodeCategory.Join), PopChildren());
    }

    protected override void VisitCrossJoin(CrossJoin source)
    {
        PaintTokens(source, Color(NodeCategory.Join));

        PushChildren();
        Walk(source.Left);
        Walk(source.Right);
        AddBranch("CROSS JOIN", Color(NodeCategory.Join), PopChildren());
    }

    protected override void VisitApplyJoin(ApplyJoin source)
    {
        PaintTokens(source, Color(NodeCategory.Join));

        string label = source.ApplyType == ApplyType.Cross ? "CROSS APPLY" : "OUTER APPLY";
        PushChildren();
        Walk(source.Left);
        Walk(source.Right);
        AddBranch(label, Color(NodeCategory.Join), PopChildren());
    }

    protected override void VisitParenthesizedTableSource(ParenthesizedTableSource source)
    {
        Walk(source.Inner);
    }

    protected override void VisitPivotTableSource(PivotTableSource source)
    {
        PaintTokens(source, Color(NodeCategory.Table));
        PushChildren();
        Walk(source.Source);
        AddBranch("PIVOT", Color(NodeCategory.Table), PopChildren());
    }

    protected override void VisitUnpivotTableSource(UnpivotTableSource source)
    {
        PaintTokens(source, Color(NodeCategory.Table));
        PushChildren();
        Walk(source.Source);
        AddBranch("UNPIVOT", Color(NodeCategory.Table), PopChildren());
    }

    protected override void VisitValuesTableSource(ValuesTableSource source)
    {
        PaintTokens(source, Color(NodeCategory.Table));
        AddLeaf("VALUES", Truncate(LexemeText(source), 50), Color(NodeCategory.Table));
    }

    protected override void VisitRowsetFunctionReference(RowsetFunctionReference source)
    {
        PaintTokens(source, Color(NodeCategory.Function));
        PushChildren();
        Walk(source.FunctionCall);
        AddBranch("RowsetFunction", Color(NodeCategory.Function), PopChildren());
    }
}
