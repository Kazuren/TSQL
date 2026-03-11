using System.Collections.Generic;
using System.Text;

namespace TSQL
{
    #region Window Function Support

    /// <summary>
    /// Frame type for window functions: ROWS or RANGE
    /// </summary>
    public enum WindowFrameType
    {
        Rows,
        Range
    }

    /// <summary>
    /// Bound type for window frame specifications
    /// </summary>
    public enum WindowFrameBoundType
    {
        UnboundedPreceding,
        UnboundedFollowing,
        CurrentRow,
        Preceding,   // N PRECEDING
        Following    // N FOLLOWING
    }

    /// <summary>
    /// Represents a single frame boundary (e.g., UNBOUNDED PRECEDING, CURRENT ROW, 3 PRECEDING)
    /// </summary>
    public class WindowFrameBound : SyntaxElement
    {
        public WindowFrameBoundType BoundType { get; }
        public Expr Offset { get; }  // For N PRECEDING/FOLLOWING (null otherwise)

        internal Token _unboundedToken;   // UNBOUNDED (if applicable)
        internal Token _currentToken;     // CURRENT (if applicable)
        internal Token _rowToken;         // ROW in CURRENT ROW (if applicable)
        internal Token _precedingToken;   // PRECEDING (if applicable)
        internal Token _followingToken;   // FOLLOWING (if applicable)

        public WindowFrameBound(WindowFrameBoundType boundType, Expr offset = null)
        {
            BoundType = boundType;
            Offset = offset;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            switch (BoundType)
            {
                case WindowFrameBoundType.UnboundedPreceding:
                    yield return _unboundedToken;
                    yield return _precedingToken;
                    break;
                case WindowFrameBoundType.UnboundedFollowing:
                    yield return _unboundedToken;
                    yield return _followingToken;
                    break;
                case WindowFrameBoundType.CurrentRow:
                    yield return _currentToken;
                    yield return _rowToken;
                    break;
                case WindowFrameBoundType.Preceding:
                    foreach (Token token in Offset.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _precedingToken;
                    break;
                case WindowFrameBoundType.Following:
                    foreach (Token token in Offset.DescendantTokens())
                    {
                        yield return token;
                    }
                    yield return _followingToken;
                    break;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            switch (BoundType)
            {
                case WindowFrameBoundType.UnboundedPreceding:
                    _unboundedToken.AppendTo(sb);
                    _precedingToken.AppendTo(sb);
                    break;
                case WindowFrameBoundType.UnboundedFollowing:
                    _unboundedToken.AppendTo(sb);
                    _followingToken.AppendTo(sb);
                    break;
                case WindowFrameBoundType.CurrentRow:
                    _currentToken.AppendTo(sb);
                    _rowToken.AppendTo(sb);
                    break;
                case WindowFrameBoundType.Preceding:
                    Offset.WriteTo(sb);
                    _precedingToken.AppendTo(sb);
                    break;
                case WindowFrameBoundType.Following:
                    Offset.WriteTo(sb);
                    _followingToken.AppendTo(sb);
                    break;
            }
        }
    }

    /// <summary>
    /// Represents a window frame clause: ROWS/RANGE [BETWEEN bound AND bound | bound]
    /// </summary>
    public class WindowFrame : SyntaxElement
    {
        public WindowFrameType FrameType { get; }
        public WindowFrameBound Start { get; }
        public WindowFrameBound End { get; }  // null if short syntax (not BETWEEN)

        internal Token _rowsOrRangeToken;
        internal Token _betweenToken;  // null if short syntax
        internal Token _andToken;      // null if short syntax

        public WindowFrame(WindowFrameType frameType, WindowFrameBound start, WindowFrameBound end = null)
        {
            FrameType = frameType;
            Start = start;
            End = end;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _rowsOrRangeToken;

            if (_betweenToken != null)
            {
                yield return _betweenToken;
                foreach (Token token in Start.DescendantTokens())
                {
                    yield return token;
                }
                yield return _andToken;
                foreach (Token token in End.DescendantTokens())
                {
                    yield return token;
                }
            }
            else
            {
                foreach (Token token in Start.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _rowsOrRangeToken.AppendTo(sb);

            if (_betweenToken != null)
            {
                _betweenToken.AppendTo(sb);
                Start.WriteTo(sb);
                _andToken.AppendTo(sb);
                End.WriteTo(sb);
            }
            else
            {
                Start.WriteTo(sb);
            }
        }
    }

    /// <summary>
    /// Represents the OVER clause: OVER (PARTITION BY ... ORDER BY ... ROWS/RANGE ...)
    /// </summary>
    public class OverClause : SyntaxElement
    {
        public SyntaxElementList<Expr> PartitionBy { get; set; }
        public SyntaxElementList<OrderByItem> OrderBy { get; set; }
        public WindowFrame Frame { get; set; }

        internal Token _overKeyword;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _partitionKeyword;
        internal Token _partitionByKeyword;
        internal Token _orderKeyword;
        internal Token _orderByKeyword;

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _overKeyword;
            yield return _leftParen;

            if (PartitionBy != null && PartitionBy.Count > 0)
            {
                yield return _partitionKeyword;
                yield return _partitionByKeyword;
                foreach (Token token in PartitionBy.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (OrderBy != null && OrderBy.Count > 0)
            {
                yield return _orderKeyword;
                yield return _orderByKeyword;
                foreach (Token token in OrderBy.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (Frame != null)
            {
                foreach (Token token in Frame.DescendantTokens())
                {
                    yield return token;
                }
            }

            yield return _rightParen;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _overKeyword.AppendTo(sb);
            _leftParen.AppendTo(sb);

            if (PartitionBy != null && PartitionBy.Count > 0)
            {
                _partitionKeyword.AppendTo(sb);
                _partitionByKeyword.AppendTo(sb);
                PartitionBy.WriteTo(sb);
            }

            if (OrderBy != null && OrderBy.Count > 0)
            {
                _orderKeyword.AppendTo(sb);
                _orderByKeyword.AppendTo(sb);
                OrderBy.WriteTo(sb);
            }

            if (Frame != null)
            {
                Frame.WriteTo(sb);
            }

            _rightParen.AppendTo(sb);
        }
    }

    #endregion

    #region DataType

    /// <summary>
    /// Represents a SQL data type: INT, VARCHAR(50), DECIMAL(10, 2), etc.
    /// </summary>
    public class DataType : SyntaxElement
    {
        public string TypeName
        {
            get => _typeNameToken.Lexeme;
            set { _typeNameToken = new ConcreteToken(_typeNameToken.Type, value, null); }
        }
        public SyntaxElementList<Expr> Parameters { get; }

        internal Token _typeNameToken;
        internal Token _leftParen;
        internal Token _rightParen;

        internal DataType(Token typeName, SyntaxElementList<Expr> parameters)
        {
            _typeNameToken = typeName;
            Parameters = parameters;
        }

        internal DataType(Token typeName)
        {
            _typeNameToken = typeName;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _typeNameToken;
            if (Parameters != null)
            {
                yield return _leftParen;
                foreach (Token token in Parameters.DescendantTokens())
                    yield return token;
                yield return _rightParen;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _typeNameToken.AppendTo(sb);
            if (Parameters != null)
            {
                _leftParen.AppendTo(sb);
                Parameters.WriteTo(sb);
                _rightParen.AppendTo(sb);
            }
        }
    }

    #endregion

    #region ORDER BY

    public enum SortDirection { Ascending, Descending }

    public class OrderByClause : SyntaxElement
    {
        public SyntaxElementList<OrderByItem> Items { get; set; } = new SyntaxElementList<OrderByItem>();
        public Expr OffsetCount { get; set; }
        public Expr FetchCount { get; set; }

        internal Token _orderKeyword;
        internal Token _orderByKeyword;
        internal Token _offsetKeyword;
        internal Token _offsetRowOrRows;
        internal Token _fetchKeyword;
        internal Token _firstOrNext;
        internal Token _fetchRowOrRows;
        internal Token _onlyKeyword;

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _orderKeyword;
            yield return _orderByKeyword;
            foreach (Token token in Items.DescendantTokens())
                yield return token;

            if (OffsetCount != null)
            {
                yield return _offsetKeyword;
                foreach (Token token in OffsetCount.DescendantTokens())
                    yield return token;
                yield return _offsetRowOrRows;

                if (FetchCount != null)
                {
                    yield return _fetchKeyword;
                    yield return _firstOrNext;
                    foreach (Token token in FetchCount.DescendantTokens())
                        yield return token;
                    yield return _fetchRowOrRows;
                    yield return _onlyKeyword;
                }
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _orderKeyword.AppendTo(sb);
            _orderByKeyword.AppendTo(sb);
            Items.WriteTo(sb);

            if (OffsetCount != null)
            {
                _offsetKeyword.AppendTo(sb);
                OffsetCount.WriteTo(sb);
                _offsetRowOrRows.AppendTo(sb);

                if (FetchCount != null)
                {
                    _fetchKeyword.AppendTo(sb);
                    _firstOrNext.AppendTo(sb);
                    FetchCount.WriteTo(sb);
                    _fetchRowOrRows.AppendTo(sb);
                    _onlyKeyword.AppendTo(sb);
                }
            }
        }
    }

    public class OrderByItem : SyntaxElement
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }
        public SortDirection Direction { get; set; }


        // ASC, DESC
        internal Token _orderToken;

        internal override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Expression.DescendantTokens())
            {
                yield return token;
            }

            if (_orderToken != null)
            {
                yield return _orderToken;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            Expression.WriteTo(sb);

            if (_orderToken != null)
            {
                _orderToken.AppendTo(sb);
            }
        }
    }

    #endregion

    #region GROUP BY Clause

    public class GroupByClause : SyntaxElement
    {
        public SyntaxElementList<GroupByItem> Items { get; }

        internal Token _groupKeyword;
        internal Token _byKeyword;

        public GroupByClause(SyntaxElementList<GroupByItem> items)
        {
            _groupKeyword = ConcreteToken.WithLeadingSpace(TokenType.GROUP, "GROUP");
            _byKeyword = ConcreteToken.WithLeadingSpace(TokenType.BY, "BY");
            Items = items;
        }

        internal GroupByClause(Token groupKeyword, Token byKeyword, SyntaxElementList<GroupByItem> items)
        {
            _groupKeyword = groupKeyword;
            _byKeyword = byKeyword;
            Items = items;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _groupKeyword;
            yield return _byKeyword;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _groupKeyword.AppendTo(sb);
            _byKeyword.AppendTo(sb);
            Items.WriteTo(sb);
        }
    }

    public abstract class GroupByItem : SyntaxElement { }

    public class GroupByExpression : GroupByItem
    {
        public Expr Expression { get; }

        public GroupByExpression(Expr expression)
        {
            Expression = expression;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Expression.DescendantTokens())
                yield return token;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            Expression.WriteTo(sb);
        }
    }

    public class GroupByGrandTotal : GroupByItem
    {
        internal Token _leftParen;
        internal Token _rightParen;

        public GroupByGrandTotal()
        {
            _leftParen = new ConcreteToken(TokenType.LEFT_PAREN, "(", null);
            _rightParen = new ConcreteToken(TokenType.RIGHT_PAREN, ")", null);
        }

        internal GroupByGrandTotal(Token leftParen, Token rightParen)
        {
            _leftParen = leftParen;
            _rightParen = rightParen;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            yield return _rightParen;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _leftParen.AppendTo(sb);
            _rightParen.AppendTo(sb);
        }
    }

    public class GroupByComposite : GroupByItem
    {
        public SyntaxElementList<Expr> Expressions { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public GroupByComposite(SyntaxElementList<Expr> expressions)
        {
            _leftParen = new ConcreteToken(TokenType.LEFT_PAREN, "(", null);
            Expressions = expressions;
            _rightParen = new ConcreteToken(TokenType.RIGHT_PAREN, ")", null);
        }

        internal GroupByComposite(Token leftParen, SyntaxElementList<Expr> expressions, Token rightParen)
        {
            _leftParen = leftParen;
            Expressions = expressions;
            _rightParen = rightParen;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            foreach (Token token in Expressions.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _leftParen.AppendTo(sb);
            Expressions.WriteTo(sb);
            _rightParen.AppendTo(sb);
        }
    }

    public class GroupByRollup : GroupByItem
    {
        public SyntaxElementList<GroupByItem> Items { get; }

        internal Token _rollupKeyword;
        internal Token _leftParen;
        internal Token _rightParen;

        public GroupByRollup(SyntaxElementList<GroupByItem> items)
        {
            _rollupKeyword = ConcreteToken.WithLeadingSpace(TokenType.ROLLUP, "ROLLUP");
            _leftParen = new ConcreteToken(TokenType.LEFT_PAREN, "(", null);
            Items = items;
            _rightParen = new ConcreteToken(TokenType.RIGHT_PAREN, ")", null);
        }

        internal GroupByRollup(Token rollupKeyword, Token leftParen, SyntaxElementList<GroupByItem> items, Token rightParen)
        {
            _rollupKeyword = rollupKeyword;
            _leftParen = leftParen;
            Items = items;
            _rightParen = rightParen;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _rollupKeyword;
            yield return _leftParen;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _rollupKeyword.AppendTo(sb);
            _leftParen.AppendTo(sb);
            Items.WriteTo(sb);
            _rightParen.AppendTo(sb);
        }
    }

    public class GroupByCube : GroupByItem
    {
        public SyntaxElementList<GroupByItem> Items { get; }

        internal Token _cubeKeyword;
        internal Token _leftParen;
        internal Token _rightParen;

        public GroupByCube(SyntaxElementList<GroupByItem> items)
        {
            _cubeKeyword = ConcreteToken.WithLeadingSpace(TokenType.CUBE, "CUBE");
            _leftParen = new ConcreteToken(TokenType.LEFT_PAREN, "(", null);
            Items = items;
            _rightParen = new ConcreteToken(TokenType.RIGHT_PAREN, ")", null);
        }

        internal GroupByCube(Token cubeKeyword, Token leftParen, SyntaxElementList<GroupByItem> items, Token rightParen)
        {
            _cubeKeyword = cubeKeyword;
            _leftParen = leftParen;
            Items = items;
            _rightParen = rightParen;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _cubeKeyword;
            yield return _leftParen;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _cubeKeyword.AppendTo(sb);
            _leftParen.AppendTo(sb);
            Items.WriteTo(sb);
            _rightParen.AppendTo(sb);
        }
    }

    public class GroupByGroupingSets : GroupByItem
    {
        public SyntaxElementList<GroupByItem> Items { get; }

        internal Token _groupingKeyword;
        internal Token _setsKeyword;
        internal Token _leftParen;
        internal Token _rightParen;

        public GroupByGroupingSets(SyntaxElementList<GroupByItem> items)
        {
            _groupingKeyword = ConcreteToken.WithLeadingSpace(TokenType.GROUPING, "GROUPING");
            _setsKeyword = ConcreteToken.WithLeadingSpace(TokenType.SETS, "SETS");
            _leftParen = new ConcreteToken(TokenType.LEFT_PAREN, "(", null);
            Items = items;
            _rightParen = new ConcreteToken(TokenType.RIGHT_PAREN, ")", null);
        }

        internal GroupByGroupingSets(Token groupingKeyword, Token setsKeyword, Token leftParen, SyntaxElementList<GroupByItem> items, Token rightParen)
        {
            _groupingKeyword = groupingKeyword;
            _setsKeyword = setsKeyword;
            _leftParen = leftParen;
            Items = items;
            _rightParen = rightParen;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _groupingKeyword;
            yield return _setsKeyword;
            yield return _leftParen;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _groupingKeyword.AppendTo(sb);
            _setsKeyword.AppendTo(sb);
            _leftParen.AppendTo(sb);
            Items.WriteTo(sb);
            _rightParen.AppendTo(sb);
        }
    }

    #endregion

    #region FOR Clause

    public enum ForXmlMode { Raw, Auto, Explicit, Path }
    public enum ForJsonMode { Auto, Path }

    public enum ForDirectiveType
    {
        BinaryBase64, Type, Root, XmlData, XmlSchema,
        Elements, ElementsXsiNil, ElementsAbsent,
        IncludeNullValues, WithoutArrayWrapper
    }

    public abstract class ForClause : SyntaxElement
    {
        internal Token _forToken;
    }

    public class ForBrowseClause : ForClause
    {
        internal Token _browseToken;

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _forToken;
            yield return _browseToken;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _forToken.AppendTo(sb);
            _browseToken.AppendTo(sb);
        }
    }

    public class ForXmlClause : ForClause
    {
        public ForXmlMode Mode { get; }
        public SyntaxElementList<ForDirective> Directives { get; }

        internal Token _xmlToken;
        internal Token _modeToken;
        internal Token _modeLeftParen;
        internal Token _modeName;
        internal Token _modeRightParen;
        internal Token _firstDirectiveComma;

        public ForXmlClause(ForXmlMode mode, SyntaxElementList<ForDirective> directives)
        {
            Mode = mode;
            Directives = directives;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _forToken;
            yield return _xmlToken;
            yield return _modeToken;
            if (_modeLeftParen != null)
            {
                yield return _modeLeftParen;
                yield return _modeName;
                yield return _modeRightParen;
            }
            if (Directives.Count > 0)
            {
                yield return _firstDirectiveComma;
                foreach (Token token in Directives.DescendantTokens())
                    yield return token;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _forToken.AppendTo(sb);
            _xmlToken.AppendTo(sb);
            _modeToken.AppendTo(sb);
            if (_modeLeftParen != null)
            {
                _modeLeftParen.AppendTo(sb);
                _modeName.AppendTo(sb);
                _modeRightParen.AppendTo(sb);
            }
            if (Directives.Count > 0)
            {
                _firstDirectiveComma.AppendTo(sb);
                Directives.WriteTo(sb);
            }
        }
    }

    public class ForJsonClause : ForClause
    {
        public ForJsonMode Mode { get; }
        public SyntaxElementList<ForDirective> Directives { get; }

        internal Token _jsonToken;
        internal Token _modeToken;
        internal Token _firstDirectiveComma;

        public ForJsonClause(ForJsonMode mode, SyntaxElementList<ForDirective> directives)
        {
            Mode = mode;
            Directives = directives;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _forToken;
            yield return _jsonToken;
            yield return _modeToken;
            if (Directives.Count > 0)
            {
                yield return _firstDirectiveComma;
                foreach (Token token in Directives.DescendantTokens())
                    yield return token;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _forToken.AppendTo(sb);
            _jsonToken.AppendTo(sb);
            _modeToken.AppendTo(sb);
            if (Directives.Count > 0)
            {
                _firstDirectiveComma.AppendTo(sb);
                Directives.WriteTo(sb);
            }
        }
    }

    public class ForDirective : SyntaxElement
    {
        public ForDirectiveType DirectiveType { get; }

        internal Token _token1;
        internal Token _token2;
        internal Token _leftParen;
        internal Token _value;
        internal Token _rightParen;

        internal ForDirective(ForDirectiveType type)
        {
            DirectiveType = type;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _token1;
            if (_token2 != null)
                yield return _token2;
            if (_leftParen != null)
            {
                yield return _leftParen;
                yield return _value;
                yield return _rightParen;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _token1.AppendTo(sb);
            if (_token2 != null)
                _token2.AppendTo(sb);
            if (_leftParen != null)
            {
                _leftParen.AppendTo(sb);
                _value.AppendTo(sb);
                _rightParen.AppendTo(sb);
            }
        }
    }

    #endregion

    #region Query Expressions

    public enum SetOperationType { Union, UnionAll, Intersect, Except }
    public enum SetQuantifier { All, Distinct }

    public abstract class QueryExpression : SyntaxElement
    {
        public OrderByClause OrderBy { get; set; }
        public ForClause For { get; set; }
    }

    public class SelectExpression : QueryExpression
    {
        public SetQuantifier Quantifier { get; set; }
        public TopClause Top { get; set; }
        public SyntaxElementList<SelectItem> Columns { get; set; }
        private Expr.ObjectIdentifier _into;
        public Expr.ObjectIdentifier Into
        {
            get => _into;
            set
            {
                _into = value;
                if (value != null && _intoKeyword == null)
                {
                    _intoKeyword = ConcreteToken.WithLeadingSpace(TokenType.INTO, "INTO");
                }
            }
        }
        internal Token _intoKeyword;
        public FromClause From { get; set; }
        private AST.Predicate _where;
        public AST.Predicate Where
        {
            get => _where;
            set => SetWithTrivia(ref _where, value);
        }
        public GroupByClause GroupBy { get; set; }
        private AST.Predicate _having;
        public AST.Predicate Having
        {
            get => _having;
            set => SetWithTrivia(ref _having, value);
        }

        // Original tokens
        internal Token _selectKeyword;

        public SelectExpression()
        {
            _selectKeyword = new ConcreteToken(TokenType.SELECT, "SELECT", null);
            Columns = new SyntaxElementList<SelectItem>();
        }
        internal Token _quantifierKeyword;
        internal Token _whereKeyword;
        internal Token _havingKeyword;

        public void AddWhere(AST.Predicate condition)
        {
            if (Where == null)
            {
                _whereKeyword = ConcreteToken.WithLeadingSpace(TokenType.WHERE, "WHERE");

                Token conditionFirst = FirstTokenOf(condition);
                conditionFirst.ClearLeadingTrivia();
                conditionFirst.AddLeadingTrivia(Whitespace.Space);

                _where = condition;
            }
            else
            {
                AST.Predicate existing = _where;

                if (existing is AST.Predicate.Or)
                {
                    existing = WrapInGrouping(existing);
                }

                if (condition is AST.Predicate.Or)
                {
                    condition = WrapInGrouping(condition);
                }

                Token conditionFirst = FirstTokenOf(condition);
                conditionFirst.ClearLeadingTrivia();
                conditionFirst.AddLeadingTrivia(Whitespace.Space);

                var andToken = ConcreteToken.WithLeadingSpace(TokenType.AND, "AND");

                var andPredicate = new AST.Predicate.And(existing, condition);
                andPredicate._andToken = andToken;

                // Assign directly to avoid SetWithTrivia trivia transfer,
                // since we manage trivia explicitly above.
                _where = andPredicate;
            }
        }

        private static AST.Predicate WrapInGrouping(AST.Predicate predicate)
        {
            var leftParen = new ConcreteToken(TokenType.LEFT_PAREN, "(", null);
            var rightParen = new ConcreteToken(TokenType.RIGHT_PAREN, ")", null);

            Token predicateFirst = FirstTokenOf(predicate);
            if (predicateFirst != null)
            {
                leftParen.AddLeadingTrivia(predicateFirst.LeadingTrivia);
                predicateFirst.ClearLeadingTrivia();
            }

            var grouping = new AST.Predicate.Grouping(predicate);
            grouping._leftParen = leftParen;
            grouping._rightParen = rightParen;
            return grouping;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _selectKeyword;

            if (_quantifierKeyword != null)
            {
                yield return _quantifierKeyword;
            }

            if (Top != null)
            {
                foreach (Token token in Top.DescendantTokens())
                {
                    yield return token;
                }
            }

            foreach (Token token in Columns.DescendantTokens())
            {
                yield return token;
            }

            if (Into != null)
            {
                yield return _intoKeyword;
                foreach (Token token in Into.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (From != null)
            {
                foreach (Token token in From.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (Where != null)
            {
                yield return _whereKeyword;
                foreach (Token token in Where.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (GroupBy != null)
            {
                foreach (Token token in GroupBy.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (Having != null)
            {
                yield return _havingKeyword;
                foreach (Token token in Having.DescendantTokens())
                {
                    yield return token;
                }
            }

            if (OrderBy != null)
            {
                foreach (Token token in OrderBy.DescendantTokens())
                    yield return token;
            }

            if (For != null)
            {
                foreach (Token token in For.DescendantTokens())
                    yield return token;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _selectKeyword.AppendTo(sb);

            if (_quantifierKeyword != null)
            {
                _quantifierKeyword.AppendTo(sb);
            }

            if (Top != null)
            {
                Top.WriteTo(sb);
            }

            Columns.WriteTo(sb);

            if (Into != null)
            {
                _intoKeyword.AppendTo(sb);
                Into.WriteTo(sb);
            }

            if (From != null)
            {
                From.WriteTo(sb);
            }

            if (Where != null)
            {
                _whereKeyword.AppendTo(sb);
                Where.WriteTo(sb);
            }

            if (GroupBy != null)
            {
                GroupBy.WriteTo(sb);
            }

            if (Having != null)
            {
                _havingKeyword.AppendTo(sb);
                Having.WriteTo(sb);
            }

            if (OrderBy != null)
            {
                OrderBy.WriteTo(sb);
            }

            if (For != null)
            {
                For.WriteTo(sb);
            }
        }
    }

    public class SetOperation : QueryExpression
    {
        private QueryExpression _left;
        public QueryExpression Left
        {
            get => _left;
            set => SetWithTrivia(ref _left, value);
        }
        private QueryExpression _right;
        public QueryExpression Right
        {
            get => _right;
            set => SetWithTrivia(ref _right, value);
        }
        public SetOperationType OperationType { get; }

        internal Token _operatorToken;
        internal Token _allToken;

        public SetOperation(QueryExpression left, QueryExpression right, SetOperationType operationType)
        {
            _left = left;
            _right = right;
            OperationType = operationType;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Left.DescendantTokens())
                yield return token;
            yield return _operatorToken;
            if (_allToken != null)
                yield return _allToken;
            foreach (Token token in Right.DescendantTokens())
                yield return token;

            if (OrderBy != null)
            {
                foreach (Token token in OrderBy.DescendantTokens())
                    yield return token;
            }

            if (For != null)
            {
                foreach (Token token in For.DescendantTokens())
                    yield return token;
            }
        }

        internal override void WriteTo(StringBuilder sb)
        {
            Left.WriteTo(sb);
            _operatorToken.AppendTo(sb);
            if (_allToken != null)
                _allToken.AppendTo(sb);
            Right.WriteTo(sb);

            if (OrderBy != null)
            {
                OrderBy.WriteTo(sb);
            }

            if (For != null)
            {
                For.WriteTo(sb);
            }
        }
    }

    #endregion

    #region SQL Names

    public abstract class SqlName : SyntaxElement
    {
        public string Name { get; }
        public string Lexeme { get => _token.Lexeme; }
        private readonly Token _token;

        protected SqlName(string name)
        {
            Name = name;
            _token = ConcreteToken.WithLeadingSpace(TokenType.IDENTIFIER, name);
        }

        internal SqlName(Token token)
        {
            Name = token.IdentifierName;
            _token = token;
        }

        internal override IEnumerable<Token> DescendantTokens()
        {
            yield return _token;
        }

        internal override void WriteTo(StringBuilder sb)
        {
            _token.AppendTo(sb);
        }
    }

    public class ServerName : SqlName
    {
        public ServerName(string name) : base(name) { }
        internal ServerName(Token token) : base(token) { }
    }

    public class DatabaseName : SqlName
    {
        public DatabaseName(string name) : base(name) { }
        internal DatabaseName(Token token) : base(token) { }
    }

    public class SchemaName : SqlName
    {
        public SchemaName(string name) : base(name) { }
        internal SchemaName(Token token) : base(token) { }
    }

    public class ObjectName : SqlName
    {
        public ObjectName(string name) : base(name) { }
        internal ObjectName(Token token) : base(token) { }

        public ObjectName Rename(string newName)
        {
            bool bracketed = Lexeme.StartsWith("[", System.StringComparison.Ordinal);
            string lexeme = bracketed ? "[" + newName + "]" : newName;
            return new ObjectName(
                ConcreteToken.WithLeadingSpace(TokenType.IDENTIFIER, lexeme, newName));
        }
    }

    public class ColumnName : SqlName
    {
        public ColumnName(string name) : base(name) { }
        internal ColumnName(Token token) : base(token) { }
    }

    #endregion
}
