using System.Collections.Generic;

namespace TSQL
{
    #region Query Hint Types

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

    #endregion

    #region OPTION Clause

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
