using System.Collections.Generic;
using System.Text;

namespace TSQL
{
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
            _fromToken = ConcreteToken.WithLeadingSpace(TokenType.FROM, "FROM");
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

        public override void WriteTo(StringBuilder sb)
        {
            _fromToken.AppendTo(sb);
            TableSources.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            TableName.WriteTo(sb);
            if (ForSystemTime != null)
                ForSystemTime.WriteTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
            if (Tablesample != null)
                Tablesample.WriteTo(sb);
            if (TableHints != null)
                TableHints.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            Subquery.WriteTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
            if (ColumnAliases != null)
                ColumnAliases.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _variableToken.AppendTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            Left.WriteTo(sb);
            if (_joinTypeToken != null)
                _joinTypeToken.AppendTo(sb);
            if (_outerToken != null)
                _outerToken.AppendTo(sb);
            if (_joinHintToken != null)
                _joinHintToken.AppendTo(sb);
            _joinToken.AppendTo(sb);
            Right.WriteTo(sb);
            _onToken.AppendTo(sb);
            OnCondition.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            Left.WriteTo(sb);
            _crossToken.AppendTo(sb);
            _joinToken.AppendTo(sb);
            Right.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            Left.WriteTo(sb);
            _applyTypeToken.AppendTo(sb);
            _applyToken.AppendTo(sb);
            Right.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _leftParen.AppendTo(sb);
            Inner.WriteTo(sb);
            _rightParen.AppendTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            Source.WriteTo(sb);
            _pivotToken.AppendTo(sb);
            _leftParen.AppendTo(sb);
            AggregateFunction.WriteTo(sb);
            _forToken.AppendTo(sb);
            PivotColumn.WriteTo(sb);
            _inToken.AppendTo(sb);
            _inLeftParen.AppendTo(sb);
            ValueList.WriteTo(sb);
            _inRightParen.AppendTo(sb);
            _rightParen.AppendTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            Source.WriteTo(sb);
            _unpivotToken.AppendTo(sb);
            _leftParen.AppendTo(sb);
            ValueColumn.WriteTo(sb);
            _forToken.AppendTo(sb);
            PivotColumn.WriteTo(sb);
            _inToken.AppendTo(sb);
            _inLeftParen.AppendTo(sb);
            ColumnList.WriteTo(sb);
            _inRightParen.AppendTo(sb);
            _rightParen.AppendTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _outerLeftParen.AppendTo(sb);
            _valuesToken.AppendTo(sb);
            Rows.WriteTo(sb);
            _outerRightParen.AppendTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
            if (ColumnAliases != null)
                ColumnAliases.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            FunctionCall.WriteTo(sb);
            if (Alias != null)
                Alias.WriteTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _leftParen.AppendTo(sb);
            Values.WriteTo(sb);
            _rightParen.AppendTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _leftParen.AppendTo(sb);
            ColumnNames.WriteTo(sb);
            _rightParen.AppendTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _forToken.AppendTo(sb);
            _systemTimeToken.AppendTo(sb);

            switch (TimeType)
            {
                case SystemTimeType.AsOf:
                    _typeKeyword1.AppendTo(sb);
                    _typeKeyword2.AppendTo(sb);
                    StartTime.WriteTo(sb);
                    break;
                case SystemTimeType.FromTo:
                    _typeKeyword1.AppendTo(sb);
                    StartTime.WriteTo(sb);
                    _typeKeyword2.AppendTo(sb);
                    EndTime.WriteTo(sb);
                    break;
                case SystemTimeType.BetweenAnd:
                    _typeKeyword1.AppendTo(sb);
                    StartTime.WriteTo(sb);
                    _typeKeyword2.AppendTo(sb);
                    EndTime.WriteTo(sb);
                    break;
                case SystemTimeType.ContainedIn:
                    _typeKeyword1.AppendTo(sb);
                    _typeKeyword2.AppendTo(sb);
                    _leftParen.AppendTo(sb);
                    StartTime.WriteTo(sb);
                    _comma.AppendTo(sb);
                    EndTime.WriteTo(sb);
                    _rightParen.AppendTo(sb);
                    break;
                case SystemTimeType.All:
                    _typeKeyword1.AppendTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _tablesampleToken.AppendTo(sb);
            if (_systemToken != null)
                _systemToken.AppendTo(sb);
            _leftParen.AppendTo(sb);
            SampleSize.WriteTo(sb);
            _unitToken.AppendTo(sb);
            _rightParen.AppendTo(sb);
            if (RepeatSeed != null)
            {
                _repeatableToken.AppendTo(sb);
                _repeatLeftParen.AppendTo(sb);
                RepeatSeed.WriteTo(sb);
                _repeatRightParen.AppendTo(sb);
            }
        }
    }

    #endregion

    #region Table Hints

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

        public override void WriteTo(StringBuilder sb)
        {
            _withToken.AppendTo(sb);
            _leftParen.AppendTo(sb);
            Hints.WriteTo(sb);
            _rightParen.AppendTo(sb);
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

        public override void WriteTo(StringBuilder sb)
        {
            _hintToken.AppendTo(sb);

            switch (HintType)
            {
                case TableHintType.Index:
                    if (_equalsToken != null)
                    {
                        _equalsToken.AppendTo(sb);
                        IndexValues.WriteTo(sb);
                    }
                    else
                    {
                        _leftParen.AppendTo(sb);
                        IndexValues.WriteTo(sb);
                        _rightParen.AppendTo(sb);
                    }
                    break;
                case TableHintType.ForceSeek:
                    if (ForceSeekIndexValue != null)
                    {
                        _leftParen.AppendTo(sb);
                        ForceSeekIndexValue.WriteTo(sb);
                        _innerLeftParen.AppendTo(sb);
                        ForceSeekColumns.WriteTo(sb);
                        _innerRightParen.AppendTo(sb);
                        _rightParen.AppendTo(sb);
                    }
                    break;
                case TableHintType.SpatialWindowMaxCells:
                    _equalsToken.AppendTo(sb);
                    SpatialMaxCellsValue.WriteTo(sb);
                    break;
            }
        }
    }

    #endregion
}
