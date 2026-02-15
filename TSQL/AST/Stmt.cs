using System.Collections.Generic;
using static TSQL.Expr;

namespace TSQL
{
    public abstract class Stmt : SyntaxElement
    {
        public static Stmt Parse(string sql)
        {
            Scanner scanner = new Scanner(sql);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.Parse();
        }

        public static Select ParseSelect(string sql)
        {
            Scanner scanner = new Scanner(sql);
            List<SourceToken> tokens = scanner.ScanTokens();
            Parser parser = new Parser(tokens);
            return parser.ParseSelect();
        }

        public abstract T Accept<T>(Visitor<T> visitor);
        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
        }

        public class Select : Stmt
        {
            public Cte CteStmt { get; set; }
            private QueryExpression _query;
            public QueryExpression Query
            {
                get => _query;
                set => SetWithTrivia(ref _query, value);
            }
            public OptionClause Option { get; set; }

            public Select(QueryExpression query)
            {
                _query = query;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitSelectStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (CteStmt != null)
                {
                    foreach (Token token in CteStmt.DescendantTokens())
                        yield return token;
                }

                foreach (Token token in Query.DescendantTokens())
                {
                    yield return token;
                }

                if (Option != null)
                {
                    foreach (Token token in Option.DescendantTokens())
                        yield return token;
                }
            }
        }
    }

    public class Cte : SyntaxElement
    {
        public SyntaxElementList<CteDefinition> Ctes { get; set; } = new SyntaxElementList<CteDefinition>();

        internal Token _withToken;
        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _withToken;

            foreach (Token token in Ctes.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class CteDefinition : SyntaxElement
    {
        public string Name
        {
            get => _nameToken.Lexeme;
            set { _nameToken = new ConcreteToken(TokenType.IDENTIFIER, value, null); }
        }
        public CteColumnNames ColumnNames { get; set; }
        public Expr.Subquery Query { get; set; }

        internal Token _nameToken;
        internal Token _asToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _nameToken;

            if (ColumnNames != null)
            {
                foreach (Token token in ColumnNames.DescendantTokens())
                    yield return token;
            }

            yield return _asToken;

            foreach (Token token in Query.DescendantTokens())
                yield return token;
        }
    }

    public class CteColumnNames : SyntaxElement
    {
        public SyntaxElementList<ColumnName> ColumnNames { get; set; }
        internal Token _leftParen;
        internal Token _rightParen;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;

            foreach (Token token in ColumnNames.DescendantTokens())
            {
                yield return token;
            }

            yield return _rightParen;
        }
    }

    public interface SelectItem : ISyntaxElement
    {
    }

    public class SelectColumn : SyntaxElement, SelectItem
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }

        private Alias _alias;
        public Alias Alias
        {
            get => _alias;
            set
            {
                if (_alias is SyntaxElement oldSe && value is SyntaxElement newSe)
                {
                    TransferLeadingTrivia(oldSe, newSe);
                }
                _alias = value;
            }
        }

        public SelectColumn(Expr expression, Alias alias)
        {
            _expression = expression;
            _alias = alias;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            if (Alias is PrefixAlias prefixAlias)
            {
                foreach (Token token in prefixAlias.DescendantTokens())
                {
                    yield return token;
                }

                foreach (Token token in Expression.DescendantTokens())
                {
                    yield return token;
                }
            }
            else
            {
                foreach (Token token in Expression.DescendantTokens())
                {
                    yield return token;
                }

                if (Alias is SuffixAlias suffixAlias)
                {
                    foreach (Token token in suffixAlias.DescendantTokens())
                    {
                        yield return token;
                    }
                }
            }
        }
    }

    public interface Alias : ISyntaxElement
    {
        string Name { get; }
    }

    public class SuffixAlias : SyntaxElement, Alias
    {
        public string Name { get => _nameToken.Lexeme; }
        internal Token _nameToken;
        internal Token _asKeyword;

        public SuffixAlias(string name, bool useAs = true)
        {
            _nameToken = new ConcreteToken(TokenType.IDENTIFIER, name, null);
            _nameToken.AddLeadingTrivia(new Whitespace(" "));
            if (useAs)
            {
                _asKeyword = new ConcreteToken(TokenType.AS, "AS", null);
                _asKeyword.AddLeadingTrivia(new Whitespace(" "));
            }
        }

        internal SuffixAlias(Token name)
        {
            _nameToken = name;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            if (_asKeyword != null)
            {
                yield return _asKeyword;
            }

            yield return _nameToken;
        }
    }

    public class PrefixAlias : SyntaxElement, Alias
    {
        public string Name { get => _nameToken.Lexeme; }
        internal Token _nameToken;
        internal Token _equalsToken;

        public PrefixAlias(string name)
        {
            _nameToken = new ConcreteToken(TokenType.IDENTIFIER, name, null);
            _equalsToken = new ConcreteToken(TokenType.EQUAL, "=", null);
            _equalsToken.AddLeadingTrivia(new Whitespace(" "));
        }

        internal PrefixAlias(Token name, Token equalsToken)
        {
            _nameToken = name;
            _equalsToken = equalsToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _nameToken;
            yield return _equalsToken;
        }
    }

    public class TopClause : SyntaxElement
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }
        public bool Percent { get; set; }
        public bool WithTies { get; set; }

        public TopClause(Expr expr)
        {
            _expression = expr;
        }

        internal Token _topKeyword;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _percentKeyword;
        internal Token _withKeyword;
        internal Token _tiesKeyword;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _topKeyword;
            if (_leftParen != null)
            {
                yield return _leftParen;
            }

            foreach (Token token in Expression.DescendantTokens())
            {
                yield return token;
            }

            if (_rightParen != null)
            {
                yield return _rightParen;
            }

            if (_percentKeyword != null)
            {
                yield return _percentKeyword;
            }

            if (_withKeyword != null)
            {
                yield return _withKeyword;
                yield return _tiesKeyword;
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
        public bool Descending { get; set; }


        // ASC, DESC
        internal Token _orderToken;

        public override IEnumerable<Token> DescendantTokens()
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
    }

    #region FROM Clause Enums

    public enum JoinType { Inner, LeftOuter, RightOuter, FullOuter }
    public enum JoinHint { Loop, Hash, Merge, Remote }
    public enum ApplyType { Cross, Outer }
    public enum SystemTimeType { AsOf, FromTo, BetweenAnd, ContainedIn, All }
    public enum TableSampleUnit { Percent, Rows }

    #endregion

    #region FROM Clause

    public class FromClause : SyntaxElement
    {
        public SyntaxElementList<TableSource> TableSources { get; set; } = new SyntaxElementList<TableSource>();

        internal Token _fromToken;

        public FromClause()
        {
            _fromToken = new ConcreteToken(TokenType.FROM, "FROM", null);
            _fromToken.AddLeadingTrivia(new Whitespace(" "));
        }

        internal FromClause(Token fromToken)
        {
            _fromToken = fromToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _fromToken;

            foreach (Token token in TableSources.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    #endregion

    #region Table Sources

    public abstract class TableSource : SyntaxElement
    {
        public Alias Alias { get; set; }

        public abstract T Accept<T>(Visitor<T> visitor);

        public interface Visitor<T>
        {
            T VisitTableReference(TableReference source);
            T VisitSubqueryReference(SubqueryReference source);
            T VisitTableVariableReference(TableVariableReference source);
            T VisitQualifiedJoin(QualifiedJoin source);
            T VisitCrossJoin(CrossJoin source);
            T VisitApplyJoin(ApplyJoin source);
            T VisitParenthesizedTableSource(ParenthesizedTableSource source);
            T VisitPivotTableSource(PivotTableSource source);
            T VisitUnpivotTableSource(UnpivotTableSource source);
            T VisitValuesTableSource(ValuesTableSource source);
            T VisitRowsetFunctionReference(RowsetFunctionReference source);
        }
    }

    public class TableReference : TableSource
    {
        private Expr.ObjectIdentifier _tableName;
        public Expr.ObjectIdentifier TableName
        {
            get => _tableName;
            set => SetWithTrivia(ref _tableName, value);
        }
        public ForSystemTimeClause ForSystemTime { get; set; }
        public TablesampleClause Tablesample { get; set; }
        public TableHintClause TableHints { get; set; }

        public TableReference(Expr.ObjectIdentifier tableName)
        {
            _tableName = tableName;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitTableReference(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in TableName.DescendantTokens())
                yield return token;
            if (ForSystemTime != null)
                foreach (Token token in ForSystemTime.DescendantTokens())
                    yield return token;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
            if (Tablesample != null)
                foreach (Token token in Tablesample.DescendantTokens())
                    yield return token;
            if (TableHints != null)
                foreach (Token token in TableHints.DescendantTokens())
                    yield return token;
        }
    }

    public class SubqueryReference : TableSource
    {
        private Expr.Subquery _subquery;
        public Expr.Subquery Subquery
        {
            get => _subquery;
            set => SetWithTrivia(ref _subquery, value);
        }
        public DerivedColumnAliases ColumnAliases { get; set; }

        public SubqueryReference(Expr.Subquery subquery)
        {
            _subquery = subquery;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitSubqueryReference(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Subquery.DescendantTokens())
                yield return token;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
            if (ColumnAliases != null)
                foreach (Token token in ColumnAliases.DescendantTokens())
                    yield return token;
        }
    }

    public class TableVariableReference : TableSource
    {
        public string VariableName
        {
            get => _variableToken.Lexeme;
            set { _variableToken = new ConcreteToken(TokenType.VARIABLE, value, null); }
        }

        internal Token _variableToken;

        public TableVariableReference(Token variableToken)
        {
            _variableToken = variableToken;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitTableVariableReference(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _variableToken;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
        }
    }

    public class QualifiedJoin : TableSource
    {
        private TableSource _left;
        public TableSource Left
        {
            get => _left;
            set => SetWithTrivia(ref _left, value);
        }
        private TableSource _right;
        public TableSource Right
        {
            get => _right;
            set => SetWithTrivia(ref _right, value);
        }
        public JoinType JoinType { get; set; }
        public JoinHint? JoinHint { get; set; }
        private AST.Predicate _onCondition;
        public AST.Predicate OnCondition
        {
            get => _onCondition;
            set => SetWithTrivia(ref _onCondition, value);
        }

        internal Token _joinHintToken;    // LOOP, HASH, MERGE, REMOTE (optional)
        internal Token _joinTypeToken;    // INNER, LEFT, RIGHT, FULL (optional for bare JOIN)
        internal Token _outerToken;       // OUTER (optional, for LEFT/RIGHT/FULL)
        internal Token _joinToken;        // JOIN
        internal Token _onToken;          // ON

        public QualifiedJoin(TableSource left, TableSource right, JoinType joinType, AST.Predicate onCondition, JoinHint? joinHint = null)
        {
            _left = left;
            _right = right;
            JoinType = joinType;
            _onCondition = onCondition;
            JoinHint = joinHint;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitQualifiedJoin(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Left.DescendantTokens())
                yield return token;
            if (_joinTypeToken != null)
                yield return _joinTypeToken;
            if (_outerToken != null)
                yield return _outerToken;
            if (_joinHintToken != null)
                yield return _joinHintToken;
            yield return _joinToken;
            foreach (Token token in Right.DescendantTokens())
                yield return token;
            yield return _onToken;
            foreach (Token token in OnCondition.DescendantTokens())
                yield return token;
        }
    }

    public class CrossJoin : TableSource
    {
        private TableSource _left;
        public TableSource Left
        {
            get => _left;
            set => SetWithTrivia(ref _left, value);
        }
        private TableSource _right;
        public TableSource Right
        {
            get => _right;
            set => SetWithTrivia(ref _right, value);
        }

        internal Token _crossToken;
        internal Token _joinToken;

        public CrossJoin(TableSource left, TableSource right)
        {
            _left = left;
            _right = right;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitCrossJoin(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Left.DescendantTokens())
                yield return token;
            yield return _crossToken;
            yield return _joinToken;
            foreach (Token token in Right.DescendantTokens())
                yield return token;
        }
    }

    public class ApplyJoin : TableSource
    {
        private TableSource _left;
        public TableSource Left
        {
            get => _left;
            set => SetWithTrivia(ref _left, value);
        }
        private TableSource _right;
        public TableSource Right
        {
            get => _right;
            set => SetWithTrivia(ref _right, value);
        }
        public ApplyType ApplyType { get; set; }

        internal Token _applyTypeToken;  // CROSS or OUTER
        internal Token _applyToken;      // APPLY

        public ApplyJoin(TableSource left, TableSource right, ApplyType applyType)
        {
            _left = left;
            _right = right;
            ApplyType = applyType;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitApplyJoin(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Left.DescendantTokens())
                yield return token;
            yield return _applyTypeToken;
            yield return _applyToken;
            foreach (Token token in Right.DescendantTokens())
                yield return token;
        }
    }

    public class ParenthesizedTableSource : TableSource
    {
        private TableSource _inner;
        public TableSource Inner
        {
            get => _inner;
            set => SetWithTrivia(ref _inner, value);
        }

        internal Token _leftParen;
        internal Token _rightParen;

        public ParenthesizedTableSource(TableSource inner)
        {
            _inner = inner;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitParenthesizedTableSource(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            foreach (Token token in Inner.DescendantTokens())
                yield return token;
            yield return _rightParen;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
        }
    }

    public class PivotTableSource : TableSource
    {
        private TableSource _source;
        public TableSource Source
        {
            get => _source;
            set => SetWithTrivia(ref _source, value);
        }
        private Expr.FunctionCall _aggregateFunction;
        public Expr.FunctionCall AggregateFunction
        {
            get => _aggregateFunction;
            set => SetWithTrivia(ref _aggregateFunction, value);
        }

        private Expr.ObjectIdentifier _pivotColumn;
        public Expr.ObjectIdentifier PivotColumn
        {
            get => _pivotColumn;
            set => SetWithTrivia(ref _pivotColumn, value);
        }

        private SyntaxElementList<ColumnName> _valueList;
        public SyntaxElementList<ColumnName> ValueList
        {
            get => _valueList;
            set => SetWithTrivia(ref _valueList, value);
        }

        internal Token _pivotToken;
        internal Token _leftParen;
        internal Token _forToken;
        internal Token _inToken;
        internal Token _inLeftParen;
        internal Token _inRightParen;
        internal Token _rightParen;

        public PivotTableSource(TableSource source, Expr.FunctionCall aggregateFunction, Expr.ObjectIdentifier pivotColumn, SyntaxElementList<ColumnName> valueList)
        {
            _source = source;
            _aggregateFunction = aggregateFunction;
            _pivotColumn = pivotColumn;
            _valueList = valueList;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitPivotTableSource(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Source.DescendantTokens())
                yield return token;
            yield return _pivotToken;
            yield return _leftParen;
            foreach (Token token in AggregateFunction.DescendantTokens())
                yield return token;
            yield return _forToken;
            foreach (Token token in PivotColumn.DescendantTokens())
                yield return token;
            yield return _inToken;
            yield return _inLeftParen;
            foreach (Token token in ValueList.DescendantTokens())
                yield return token;
            yield return _inRightParen;
            yield return _rightParen;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
        }
    }

    public class UnpivotTableSource : TableSource
    {
        private TableSource _source;
        public TableSource Source
        {
            get => _source;
            set => SetWithTrivia(ref _source, value);
        }
        private Expr.ObjectIdentifier _valueColumn;
        public Expr.ObjectIdentifier ValueColumn
        {
            get => _valueColumn;
            set => SetWithTrivia(ref _valueColumn, value);
        }

        private Expr.ObjectIdentifier _pivotColumn;
        public Expr.ObjectIdentifier PivotColumn
        {
            get => _pivotColumn;
            set => SetWithTrivia(ref _pivotColumn, value);
        }

        private SyntaxElementList<ColumnName> _columnList;
        public SyntaxElementList<ColumnName> ColumnList
        {
            get => _columnList;
            set => SetWithTrivia(ref _columnList, value);
        }

        internal Token _unpivotToken;
        internal Token _leftParen;
        internal Token _forToken;
        internal Token _inToken;
        internal Token _inLeftParen;
        internal Token _inRightParen;
        internal Token _rightParen;

        public UnpivotTableSource(TableSource source, Expr.ObjectIdentifier valueColumn, Expr.ObjectIdentifier pivotColumn, SyntaxElementList<ColumnName> columnList)
        {
            _source = source;
            _valueColumn = valueColumn;
            _pivotColumn = pivotColumn;
            _columnList = columnList;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitUnpivotTableSource(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Source.DescendantTokens())
                yield return token;
            yield return _unpivotToken;
            yield return _leftParen;
            foreach (Token token in ValueColumn.DescendantTokens())
                yield return token;
            yield return _forToken;
            foreach (Token token in PivotColumn.DescendantTokens())
                yield return token;
            yield return _inToken;
            yield return _inLeftParen;
            foreach (Token token in ColumnList.DescendantTokens())
                yield return token;
            yield return _inRightParen;
            yield return _rightParen;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
        }
    }

    public class ValuesTableSource : TableSource
    {
        public SyntaxElementList<ValuesRow> Rows { get; }
        public DerivedColumnAliases ColumnAliases { get; set; }

        internal Token _outerLeftParen;
        internal Token _valuesToken;
        internal Token _outerRightParen;

        public ValuesTableSource(SyntaxElementList<ValuesRow> rows)
        {
            Rows = rows;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitValuesTableSource(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _outerLeftParen;
            yield return _valuesToken;
            foreach (Token token in Rows.DescendantTokens())
                yield return token;
            yield return _outerRightParen;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
            if (ColumnAliases != null)
                foreach (Token token in ColumnAliases.DescendantTokens())
                    yield return token;
        }
    }

    public class RowsetFunctionReference : TableSource
    {
        public Expr.FunctionCall FunctionCall { get; }

        public RowsetFunctionReference(Expr.FunctionCall functionCall)
        {
            FunctionCall = functionCall;
        }

        public override T Accept<T>(Visitor<T> visitor)
        {
            return visitor.VisitRowsetFunctionReference(this);
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in FunctionCall.DescendantTokens())
                yield return token;
            if (Alias != null)
                foreach (Token token in Alias.DescendantTokens())
                    yield return token;
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
            _groupKeyword = new ConcreteToken(TokenType.GROUP, "GROUP", null);
            _groupKeyword.AddLeadingTrivia(new Whitespace(" "));
            _byKeyword = new ConcreteToken(TokenType.BY, "BY", null);
            _byKeyword.AddLeadingTrivia(new Whitespace(" "));
            Items = items;
        }

        internal GroupByClause(Token groupKeyword, Token byKeyword, SyntaxElementList<GroupByItem> items)
        {
            _groupKeyword = groupKeyword;
            _byKeyword = byKeyword;
            Items = items;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _groupKeyword;
            yield return _byKeyword;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
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

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Expression.DescendantTokens())
                yield return token;
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

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            yield return _rightParen;
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

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            foreach (Token token in Expressions.DescendantTokens())
                yield return token;
            yield return _rightParen;
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
            _rollupKeyword = new ConcreteToken(TokenType.ROLLUP, "ROLLUP", null);
            _rollupKeyword.AddLeadingTrivia(new Whitespace(" "));
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

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _rollupKeyword;
            yield return _leftParen;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
            yield return _rightParen;
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
            _cubeKeyword = new ConcreteToken(TokenType.CUBE, "CUBE", null);
            _cubeKeyword.AddLeadingTrivia(new Whitespace(" "));
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

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _cubeKeyword;
            yield return _leftParen;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
            yield return _rightParen;
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
            _groupingKeyword = new ConcreteToken(TokenType.GROUPING, "GROUPING", null);
            _groupingKeyword.AddLeadingTrivia(new Whitespace(" "));
            _setsKeyword = new ConcreteToken(TokenType.SETS, "SETS", null);
            _setsKeyword.AddLeadingTrivia(new Whitespace(" "));
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

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _groupingKeyword;
            yield return _setsKeyword;
            yield return _leftParen;
            foreach (Token token in Items.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }
    }

    #endregion

    #region Supporting Clause Types

    public class ValuesRow : SyntaxElement
    {
        public SyntaxElementList<Expr> Values { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public ValuesRow(SyntaxElementList<Expr> values)
        {
            Values = values;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            foreach (Token token in Values.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }
    }

    public class DerivedColumnAliases : SyntaxElement
    {
        public SyntaxElementList<ColumnName> ColumnNames { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public DerivedColumnAliases(SyntaxElementList<ColumnName> columnNames)
        {
            ColumnNames = columnNames;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;
            foreach (Token token in ColumnNames.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }
    }

    public class ForSystemTimeClause : SyntaxElement
    {
        public SystemTimeType TimeType { get; }
        public Expr StartTime { get; }
        public Expr EndTime { get; }

        internal Token _forToken;
        internal Token _systemTimeToken;  // SYSTEM_TIME identifier
        internal Token _typeKeyword1;     // AS, FROM, BETWEEN, CONTAINED, ALL
        internal Token _typeKeyword2;     // OF, TO, AND, IN (depends on type)
        internal Token _leftParen;        // CONTAINED IN ( ... )
        internal Token _comma;            // CONTAINED IN (start, end)
        internal Token _rightParen;       // CONTAINED IN ( ... )

        public ForSystemTimeClause(SystemTimeType timeType, Expr startTime = null, Expr endTime = null)
        {
            TimeType = timeType;
            StartTime = startTime;
            EndTime = endTime;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _forToken;
            yield return _systemTimeToken;

            switch (TimeType)
            {
                case SystemTimeType.AsOf:
                    yield return _typeKeyword1; // AS
                    yield return _typeKeyword2; // OF
                    foreach (Token token in StartTime.DescendantTokens())
                        yield return token;
                    break;
                case SystemTimeType.FromTo:
                    yield return _typeKeyword1; // FROM
                    foreach (Token token in StartTime.DescendantTokens())
                        yield return token;
                    yield return _typeKeyword2; // TO
                    foreach (Token token in EndTime.DescendantTokens())
                        yield return token;
                    break;
                case SystemTimeType.BetweenAnd:
                    yield return _typeKeyword1; // BETWEEN
                    foreach (Token token in StartTime.DescendantTokens())
                        yield return token;
                    yield return _typeKeyword2; // AND
                    foreach (Token token in EndTime.DescendantTokens())
                        yield return token;
                    break;
                case SystemTimeType.ContainedIn:
                    yield return _typeKeyword1; // CONTAINED
                    yield return _typeKeyword2; // IN
                    yield return _leftParen;
                    foreach (Token token in StartTime.DescendantTokens())
                        yield return token;
                    yield return _comma;
                    foreach (Token token in EndTime.DescendantTokens())
                        yield return token;
                    yield return _rightParen;
                    break;
                case SystemTimeType.All:
                    yield return _typeKeyword1; // ALL
                    break;
            }
        }
    }

    public class TablesampleClause : SyntaxElement
    {
        public Expr SampleSize { get; }
        public TableSampleUnit Unit { get; }
        public Expr RepeatSeed { get; }

        internal Token _tablesampleToken;
        internal Token _systemToken;      // SYSTEM (optional)
        internal Token _leftParen;
        internal Token _unitToken;        // PERCENT or ROWS
        internal Token _rightParen;
        internal Token _repeatableToken;  // REPEATABLE (optional)
        internal Token _repeatLeftParen;
        internal Token _repeatRightParen;

        public TablesampleClause(Expr sampleSize, TableSampleUnit unit, Expr repeatSeed = null)
        {
            SampleSize = sampleSize;
            Unit = unit;
            RepeatSeed = repeatSeed;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _tablesampleToken;
            if (_systemToken != null)
                yield return _systemToken;
            yield return _leftParen;
            foreach (Token token in SampleSize.DescendantTokens())
                yield return token;
            yield return _unitToken;
            yield return _rightParen;
            if (RepeatSeed != null)
            {
                yield return _repeatableToken;
                yield return _repeatLeftParen;
                foreach (Token token in RepeatSeed.DescendantTokens())
                    yield return token;
                yield return _repeatRightParen;
            }
        }
    }

    public enum TableHintType
    {
        NoExpand, Index, ForceSeek, ForceScan, HoldLock, NoLock, NoWait,
        PageLock, ReadCommitted, ReadCommittedLock, ReadPast, ReadUncommitted,
        RepeatableRead, RowLock, Serializable, Snapshot, SpatialWindowMaxCells,
        TabLock, TabLockX, UpdLock, XLock
    }

    public class TableHintClause : SyntaxElement
    {
        public SyntaxElementList<TableHint> Hints { get; }

        internal Token _withToken;
        internal Token _leftParen;
        internal Token _rightParen;

        public TableHintClause(SyntaxElementList<TableHint> hints)
        {
            Hints = hints;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _withToken;
            yield return _leftParen;
            foreach (Token token in Hints.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }
    }

    public class TableHint : SyntaxElement
    {
        public TableHintType HintType { get; }
        public SyntaxElementList<Expr> IndexValues { get; }
        public Expr ForceSeekIndexValue { get; }
        public SyntaxElementList<ColumnName> ForceSeekColumns { get; }
        public Expr SpatialMaxCellsValue { get; }

        internal Token _hintToken;
        internal Token _equalsToken;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _innerLeftParen;
        internal Token _innerRightParen;

        public TableHint(TableHintType hintType)
        {
            HintType = hintType;
        }

        public TableHint(TableHintType hintType, SyntaxElementList<Expr> indexValues)
        {
            HintType = hintType;
            IndexValues = indexValues;
        }

        public TableHint(TableHintType hintType, Expr forceSeekIndexValue, SyntaxElementList<ColumnName> forceSeekColumns)
        {
            HintType = hintType;
            ForceSeekIndexValue = forceSeekIndexValue;
            ForceSeekColumns = forceSeekColumns;
        }

        public TableHint(TableHintType hintType, Expr spatialMaxCellsValue)
        {
            HintType = hintType;
            SpatialMaxCellsValue = spatialMaxCellsValue;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;

            switch (HintType)
            {
                case TableHintType.Index:
                    if (_equalsToken != null)
                    {
                        yield return _equalsToken;
                        foreach (Token token in IndexValues.DescendantTokens())
                            yield return token;
                    }
                    else
                    {
                        yield return _leftParen;
                        foreach (Token token in IndexValues.DescendantTokens())
                            yield return token;
                        yield return _rightParen;
                    }
                    break;
                case TableHintType.ForceSeek:
                    if (ForceSeekIndexValue != null)
                    {
                        yield return _leftParen;
                        foreach (Token token in ForceSeekIndexValue.DescendantTokens())
                            yield return token;
                        yield return _innerLeftParen;
                        foreach (Token token in ForceSeekColumns.DescendantTokens())
                            yield return token;
                        yield return _innerRightParen;
                        yield return _rightParen;
                    }
                    break;
                case TableHintType.SpatialWindowMaxCells:
                    yield return _equalsToken;
                    foreach (Token token in SpatialMaxCellsValue.DescendantTokens())
                        yield return token;
                    break;
            }
        }
    }

    #endregion

    #region Query Hints

    public enum ParameterizationMode { Simple, Forced }

    public enum QueryHintType
    {
        // Simple keyword hints (1-2 tokens)
        HashGroup, OrderGroup,
        ConcatUnion, HashUnion, MergeUnion,
        LoopJoin, MergeJoin, HashJoin,
        Recompile, ExpandViews, ForceOrder,
        KeepPlan, KeepfixedPlan, RobustPlan,
        NoPerformanceSpool, OptimizeForUnknown,
        IgnoreNonclusteredColumnstoreIndex, DisableOptimizedPlanForcing,
        ForceExternalPushdown, DisableExternalPushdown,
        ForceScaleoutExecution, DisableScaleoutExecution,
        // Value hints (keyword + value)
        Fast, Maxdop, Maxrecursion, QueryTraceOn,
        MaxGrantPercent, MinGrantPercent,
        // Parameterized
        Parameterization, Label,
        // Complex
        OptimizeFor, UseHint, UsePlan, QueryTableHint, ForTimestamp,
    }

    /// <summary>
    /// OPTIMIZE FOR (@var { UNKNOWN | = literal } [, ...n])
    /// </summary>
    public class OptimizeForVariable : SyntaxElement
    {
        public string VariableName { get => _variableToken.Lexeme; }
        public Expr LiteralValue { get; }

        internal Token _variableToken;
        internal Token _unknownToken;
        internal Token _equalsToken;

        internal OptimizeForVariable(Token variable, Expr literalValue)
        {
            _variableToken = variable;
            LiteralValue = literalValue;
        }

        internal OptimizeForVariable(Token variable)
        {
            _variableToken = variable;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _variableToken;
            if (LiteralValue != null)
            {
                yield return _equalsToken;
                foreach (Token token in LiteralValue.DescendantTokens())
                    yield return token;
            }
            else
            {
                yield return _unknownToken;
            }
        }
    }

    public abstract class QueryHint : SyntaxElement
    {
        public QueryHintType HintType { get; }

        internal Token _hintToken;

        protected QueryHint(QueryHintType hintType)
        {
            HintType = hintType;
        }
    }

    /// <summary>
    /// Simple keyword hints: RECOMPILE, HASH GROUP, KEEP PLAN, FORCE ORDER, etc.
    /// One or two keyword tokens with no additional data.
    /// </summary>
    public class SimpleQueryHint : QueryHint
    {
        internal Token _hintToken2;

        public SimpleQueryHint(QueryHintType hintType) : base(hintType) { }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            if (_hintToken2 != null)
            {
                yield return _hintToken2;
            }
        }
    }

    /// <summary>
    /// Value hints: FAST N, MAXDOP N, MAX_GRANT_PERCENT = N, LABEL = 'name', etc.
    /// A keyword followed by an optional equals sign and a value expression.
    /// </summary>
    public class ValueQueryHint : QueryHint
    {
        public Expr Value { get; }
        internal Token _equalsToken;

        public ValueQueryHint(QueryHintType hintType, Expr value) : base(hintType)
        {
            Value = value;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            if (_equalsToken != null)
            {
                yield return _equalsToken;
            }
            foreach (Token token in Value.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    /// <summary>
    /// PARAMETERIZATION { SIMPLE | FORCED }
    /// </summary>
    public class ParameterizationQueryHint : QueryHint
    {
        public ParameterizationMode ParameterizationMode { get; }
        internal Token _modeToken;

        internal ParameterizationQueryHint(ParameterizationMode mode, Token modeToken) : base(QueryHintType.Parameterization)
        {
            ParameterizationMode = mode;
            _modeToken = modeToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _modeToken;
        }
    }

    /// <summary>
    /// OPTIMIZE FOR ( @var { UNKNOWN | = literal } [, ...n] )
    /// </summary>
    public class OptimizeForQueryHint : QueryHint
    {
        public SyntaxElementList<OptimizeForVariable> OptimizeForVariables { get; }
        internal Token _forToken;
        internal Token _leftParen;
        internal Token _rightParen;

        public OptimizeForQueryHint(SyntaxElementList<OptimizeForVariable> optimizeForVariables) : base(QueryHintType.OptimizeFor)
        {
            OptimizeForVariables = optimizeForVariables;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _forToken;
            yield return _leftParen;
            foreach (Token token in OptimizeForVariables.DescendantTokens())
            {
                yield return token;
            }
            yield return _rightParen;
        }
    }

    /// <summary>
    /// OPTIMIZE FOR UNKNOWN
    /// </summary>
    public class OptimizeForUnknownQueryHint : QueryHint
    {
        internal Token _forToken;
        internal Token _unknownToken;

        public OptimizeForUnknownQueryHint() : base(QueryHintType.OptimizeForUnknown) { }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _forToken;
            yield return _unknownToken;
        }
    }

    /// <summary>
    /// USE HINT ( 'hint_name' [, ...n] )
    /// </summary>
    public class UseHintQueryHint : QueryHint
    {
        public SyntaxElementList<Expr> UseHintNames { get; }
        internal Token _hintToken2;
        internal Token _leftParen;
        internal Token _rightParen;

        public UseHintQueryHint(SyntaxElementList<Expr> useHintNames) : base(QueryHintType.UseHint)
        {
            UseHintNames = useHintNames;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _hintToken2;
            yield return _leftParen;
            foreach (Token token in UseHintNames.DescendantTokens())
            {
                yield return token;
            }
            yield return _rightParen;
        }
    }

    /// <summary>
    /// USE PLAN N'xml_plan'
    /// </summary>
    public class UsePlanQueryHint : QueryHint
    {
        public Expr Value { get; }
        internal Token _planToken;

        public UsePlanQueryHint(Expr value) : base(QueryHintType.UsePlan)
        {
            Value = value;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _planToken;
            foreach (Token token in Value.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    /// <summary>
    /// TABLE HINT ( exposed_object_name [, table_hint [, ...n]] )
    /// </summary>
    public class TableHintQueryHint : QueryHint
    {
        public Expr.ObjectIdentifier TableHintObjectName { get; }
        public SyntaxElementList<TableHint> TableHints { get; }
        internal Token _hintToken2;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _commaAfterObjectName;

        public TableHintQueryHint(Expr.ObjectIdentifier tableHintObjectName, SyntaxElementList<TableHint> tableHints) : base(QueryHintType.QueryTableHint)
        {
            TableHintObjectName = tableHintObjectName;
            TableHints = tableHints;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _hintToken2;
            yield return _leftParen;
            foreach (Token token in TableHintObjectName.DescendantTokens())
            {
                yield return token;
            }
            if (TableHints != null && TableHints.Count > 0)
            {
                yield return _commaAfterObjectName;
                foreach (Token token in TableHints.DescendantTokens())
                {
                    yield return token;
                }
            }
            yield return _rightParen;
        }
    }

    /// <summary>
    /// FOR TIMESTAMP AS OF 'time'
    /// </summary>
    public class ForTimestampQueryHint : QueryHint
    {
        public Expr Value { get; }
        internal Token _timestampToken;
        internal Token _asToken;
        internal Token _ofToken;

        public ForTimestampQueryHint(Expr value) : base(QueryHintType.ForTimestamp)
        {
            Value = value;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;
            yield return _timestampToken;
            yield return _asToken;
            yield return _ofToken;
            foreach (Token token in Value.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    /// <summary>
    /// OPTION ( query_hint [, ...n] )
    /// </summary>
    public class OptionClause : SyntaxElement
    {
        public SyntaxElementList<QueryHint> Hints { get; }

        internal Token _optionToken;
        internal Token _leftParen;
        internal Token _rightParen;

        public OptionClause(SyntaxElementList<QueryHint> hints)
        {
            Hints = hints;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _optionToken;
            yield return _leftParen;
            foreach (Token token in Hints.DescendantTokens())
                yield return token;
            yield return _rightParen;
        }
    }

    #endregion
}
