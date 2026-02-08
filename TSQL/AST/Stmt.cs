using System.Collections.Generic;
using static TSQL.Expr;

namespace TSQL
{
    public abstract class Stmt : SyntaxElement
    {
        public abstract T Accept<T>(Visitor<T> visitor);
        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
        }

        public class Select : Stmt
        {
            public Cte CteStmt { get; set; }
            private SelectExpression _selectExpression;
            public SelectExpression SelectExpression
            {
                get => _selectExpression;
                set => SetWithTrivia(ref _selectExpression, value);
            }

            public Select(SelectExpression selectExpression)
            {
                _selectExpression = selectExpression;
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

                foreach (Token token in SelectExpression.DescendantTokens())
                {
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
        public Token Name { get; set; }
        public CteColumnNames ColumnNames { get; set; }
        public Expr.Subquery Query { get; set; }

        internal Token _asToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Name;

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
        public Expr Expression { get; }
        public Alias Alias { get; }

        public SelectColumn(Expr expression, Alias alias)
        {
            Expression = expression;
            Alias = alias;
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
        Token Name { get; }
    }

    internal class SuffixAlias : SyntaxElement, Alias
    {
        public Token Name { get; }
        internal Token _asKeyword;
        public SuffixAlias(Token name)
        {
            Name = name;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            if (_asKeyword != null)
            {
                yield return _asKeyword;
            }

            yield return Name;
        }
    }

    internal class PrefixAlias : SyntaxElement, Alias
    {
        public Token Name { get; }
        internal Token _equalsToken;
        public PrefixAlias(Token name, Token equalsToken)
        {
            Name = name;
            _equalsToken = equalsToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Name;
            yield return _equalsToken;
        }
    }

    public class TopClause : SyntaxElement
    {
        public Expr Expression { get; }

        public TopClause(Expr expr)
        {
            Expression = expr;
        }

        internal Token _topKeyword;
        internal Token _leftParen;
        internal Token _rightParen;

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
        public Token _orderToken;

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
        public Expr.Subquery Subquery { get; }
        public DerivedColumnAliases ColumnAliases { get; set; }

        public SubqueryReference(Expr.Subquery subquery)
        {
            Subquery = subquery;
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
        public string VariableName => _variableToken.Lexeme;

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
        public Expr.FunctionCall AggregateFunction { get; }
        public Expr.ObjectIdentifier PivotColumn { get; }
        public SyntaxElementList<ColumnName> ValueList { get; }

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
            AggregateFunction = aggregateFunction;
            PivotColumn = pivotColumn;
            ValueList = valueList;
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
        public Expr.ObjectIdentifier ValueColumn { get; }
        public Expr.ObjectIdentifier PivotColumn { get; }
        public SyntaxElementList<ColumnName> ColumnList { get; }

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
            ValueColumn = valueColumn;
            PivotColumn = pivotColumn;
            ColumnList = columnList;
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

        internal GroupByExpression(Expr expression)
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
        public Token Variable { get; }
        public Expr LiteralValue { get; }

        internal Token _unknownToken;
        internal Token _equalsToken;

        public OptimizeForVariable(Token variable, Expr literalValue)
        {
            Variable = variable;
            LiteralValue = literalValue;
        }

        public OptimizeForVariable(Token variable)
        {
            Variable = variable;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Variable;
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

    public class QueryHint : SyntaxElement
    {
        public QueryHintType HintType { get; }

        public Expr Value { get; }
        public Token ParameterizationMode { get; }
        public SyntaxElementList<OptimizeForVariable> OptimizeForVariables { get; }
        public SyntaxElementList<Expr> UseHintNames { get; }
        public Expr.ObjectIdentifier TableHintObjectName { get; }
        public SyntaxElementList<TableHint> TableHints { get; }

        internal Token _hintToken;
        internal Token _hintToken2;
        internal Token _unknownToken;
        internal Token _equalsToken;
        internal Token _leftParen;
        internal Token _rightParen;
        internal Token _commaAfterObjectName;
        internal Token _timestampToken;
        internal Token _asToken;
        internal Token _ofToken;

        public QueryHint(QueryHintType hintType)
        {
            HintType = hintType;
        }

        public QueryHint(QueryHintType hintType, Expr value)
        {
            HintType = hintType;
            Value = value;
        }

        public QueryHint(QueryHintType hintType, Token parameterizationMode)
        {
            HintType = hintType;
            ParameterizationMode = parameterizationMode;
        }

        public QueryHint(QueryHintType hintType, SyntaxElementList<OptimizeForVariable> optimizeForVariables)
        {
            HintType = hintType;
            OptimizeForVariables = optimizeForVariables;
        }

        public QueryHint(QueryHintType hintType, SyntaxElementList<Expr> useHintNames)
        {
            HintType = hintType;
            UseHintNames = useHintNames;
        }

        public QueryHint(QueryHintType hintType, Expr.ObjectIdentifier tableHintObjectName, SyntaxElementList<TableHint> tableHints)
        {
            HintType = hintType;
            TableHintObjectName = tableHintObjectName;
            TableHints = tableHints;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _hintToken;

            switch (HintType)
            {
                // Two-token simple hints
                case QueryHintType.HashGroup:
                case QueryHintType.OrderGroup:
                case QueryHintType.ConcatUnion:
                case QueryHintType.HashUnion:
                case QueryHintType.MergeUnion:
                case QueryHintType.LoopJoin:
                case QueryHintType.MergeJoin:
                case QueryHintType.HashJoin:
                case QueryHintType.ExpandViews:
                case QueryHintType.ForceOrder:
                case QueryHintType.KeepPlan:
                case QueryHintType.KeepfixedPlan:
                case QueryHintType.RobustPlan:
                case QueryHintType.ForceExternalPushdown:
                case QueryHintType.DisableExternalPushdown:
                case QueryHintType.ForceScaleoutExecution:
                case QueryHintType.DisableScaleoutExecution:
                    yield return _hintToken2;
                    break;

                // Single-token simple hints
                case QueryHintType.Recompile:
                case QueryHintType.NoPerformanceSpool:
                case QueryHintType.IgnoreNonclusteredColumnstoreIndex:
                case QueryHintType.DisableOptimizedPlanForcing:
                    break;

                // OPTIMIZE FOR UNKNOWN (three tokens)
                case QueryHintType.OptimizeForUnknown:
                    yield return _hintToken2;
                    yield return _unknownToken;
                    break;

                // Value hints without =
                case QueryHintType.Fast:
                case QueryHintType.Maxdop:
                case QueryHintType.Maxrecursion:
                case QueryHintType.QueryTraceOn:
                    foreach (Token token in Value.DescendantTokens())
                        yield return token;
                    break;

                // Value hints with =
                case QueryHintType.MaxGrantPercent:
                case QueryHintType.MinGrantPercent:
                case QueryHintType.Label:
                    yield return _equalsToken;
                    foreach (Token token in Value.DescendantTokens())
                        yield return token;
                    break;

                // PARAMETERIZATION { SIMPLE | FORCED }
                case QueryHintType.Parameterization:
                    yield return ParameterizationMode;
                    break;

                // OPTIMIZE FOR ( @var ... )
                case QueryHintType.OptimizeFor:
                    yield return _hintToken2;
                    yield return _leftParen;
                    foreach (Token token in OptimizeForVariables.DescendantTokens())
                        yield return token;
                    yield return _rightParen;
                    break;

                // USE HINT ( 'name' ... )
                case QueryHintType.UseHint:
                    yield return _hintToken2;
                    yield return _leftParen;
                    foreach (Token token in UseHintNames.DescendantTokens())
                        yield return token;
                    yield return _rightParen;
                    break;

                // USE PLAN N'xml'
                case QueryHintType.UsePlan:
                    yield return _hintToken2;
                    foreach (Token token in Value.DescendantTokens())
                        yield return token;
                    break;

                // TABLE HINT ( name [, table_hint ...] )
                case QueryHintType.QueryTableHint:
                    yield return _hintToken2;
                    yield return _leftParen;
                    foreach (Token token in TableHintObjectName.DescendantTokens())
                        yield return token;
                    if (TableHints != null && TableHints.Count > 0)
                    {
                        yield return _commaAfterObjectName;
                        foreach (Token token in TableHints.DescendantTokens())
                            yield return token;
                    }
                    yield return _rightParen;
                    break;

                // FOR TIMESTAMP AS OF 'time'
                case QueryHintType.ForTimestamp:
                    yield return _timestampToken;
                    yield return _asToken;
                    yield return _ofToken;
                    foreach (Token token in Value.DescendantTokens())
                        yield return token;
                    break;
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
