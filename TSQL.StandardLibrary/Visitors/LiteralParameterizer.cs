using System.Collections.Generic;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    /// <summary>
    /// Converts literal values in a SQL statement into parameters (@P0, @P1, etc.)
    /// and produces a parameter dictionary mapping names to original values.
    /// NULL literals are not parameterized since IS NULL semantics differ from = @param.
    /// </summary>
    internal static class LiteralParameterizer
    {
        /// <summary>
        /// Parameterizes all non-NULL literals in the statement in place.
        /// Returns a dictionary mapping parameter names to their original values.
        /// </summary>
        public static IReadOnlyDictionary<string, object> Parameterize(Stmt stmt)
        {
            // Phase 1: Collect existing variable names
            VariableNameCollector variableCollector = new VariableNameCollector();
            variableCollector.Walk(stmt);

            // Phase 2: Walk the tree and replace literals with variables
            LiteralReplacer replacer = new LiteralReplacer(variableCollector.Names);
            replacer.Walk(stmt);

            return replacer.Parameters;
        }

        /// <summary>
        /// Parameterizes all non-NULL literals across multiple statements in place.
        /// A single parameter counter is shared across all statements, producing
        /// unique names (@P0, @P1, ...) across the whole set.
        /// </summary>
        public static IReadOnlyDictionary<string, object> Parameterize(IReadOnlyList<Stmt> stmts)
        {
            // Phase 1: Collect existing variable names from ALL statements
            VariableNameCollector variableCollector = new VariableNameCollector();
            foreach (Stmt stmt in stmts)
            {
                variableCollector.Walk(stmt);
            }

            // Phase 2: Walk each statement sequentially with a shared replacer
            LiteralReplacer replacer = new LiteralReplacer(variableCollector.Names);
            foreach (Stmt stmt in stmts)
            {
                replacer.Walk(stmt);
            }

            return replacer.Parameters;
        }

        private class LiteralReplacer : SqlWalker
        {
            private readonly HashSet<string> _existingNames;
            private int _paramIndex;

            public Dictionary<string, object> Parameters { get; } = new Dictionary<string, object>();

            public LiteralReplacer(HashSet<string> existingNames)
            {
                _existingNames = existingNames;
            }

            private Expr TryReplace(Expr expr)
            {
                object value;
                if (expr is Expr.StringLiteral stringLit)
                {
                    value = stringLit.Value;
                }
                else if (expr is Expr.IntLiteral intLit)
                {
                    value = intLit.Value;
                }
                else if (expr is Expr.DecimalLiteral decLit)
                {
                    value = decLit.Value;
                }
                else
                {
                    // NullLiteral and all non-literal exprs: walk into children, don't parameterize
                    Walk(expr);
                    return expr;
                }

                string paramName;
                do
                {
                    paramName = "@P" + _paramIndex;
                    _paramIndex++;
                } while (_existingNames.Contains(paramName.ToUpperInvariant()));

                Parameters[paramName] = value;
                return new Expr.Variable(paramName);
            }

            #region Expr Parents

            protected override void VisitBinary(Expr.Binary expr)
            {
                expr.Left = TryReplace(expr.Left);
                expr.Right = TryReplace(expr.Right);
            }

            protected override void VisitUnary(Expr.Unary expr)
            {
                expr.Right = TryReplace(expr.Right);
            }

            protected override void VisitGrouping(Expr.Grouping expr)
            {
                expr.Expression = TryReplace(expr.Expression);
            }

            protected override void VisitFunctionCall(Expr.FunctionCall expr)
            {
                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    expr.Arguments[i] = (Expr)TryReplace(expr.Arguments[i]);
                }
            }

            protected override void VisitSimpleCase(Expr.SimpleCase expr)
            {
                expr.Operand = TryReplace(expr.Operand);
                foreach (Expr.SimpleCaseWhen when in expr.WhenClauses)
                {
                    when.Value = TryReplace(when.Value);
                    when.Result = TryReplace(when.Result);
                }
                if (expr.ElseResult != null)
                {
                    expr.ElseResult = TryReplace(expr.ElseResult);
                }
            }

            protected override void VisitSearchedCase(Expr.SearchedCase expr)
            {
                foreach (Expr.SearchedCaseWhen when in expr.WhenClauses)
                {
                    Walk(when.Condition);
                    when.Result = TryReplace(when.Result);
                }
                if (expr.ElseResult != null)
                {
                    expr.ElseResult = TryReplace(expr.ElseResult);
                }
            }

            protected override void VisitCast(Expr.CastExpression expr)
            {
                expr.Expression = TryReplace(expr.Expression);
            }

            protected override void VisitConvert(Expr.ConvertExpression expr)
            {
                expr.Expression = TryReplace(expr.Expression);
                if (expr.Style != null)
                {
                    expr.Style = TryReplace(expr.Style);
                }
            }

            protected override void VisitCollate(Expr.Collate expr)
            {
                expr.Expression = TryReplace(expr.Expression);
            }

            protected override void VisitIif(Expr.Iif expr)
            {
                Walk(expr.Condition);
                expr.TrueValue = TryReplace(expr.TrueValue);
                expr.FalseValue = TryReplace(expr.FalseValue);
            }

            protected override void VisitAtTimeZone(Expr.AtTimeZone expr)
            {
                expr.Expression = TryReplace(expr.Expression);
                expr.TimeZone = TryReplace(expr.TimeZone);
            }

            protected override void VisitOpenXml(Expr.OpenXmlExpression expr)
            {
                VisitFunctionCall(expr);
            }

            #endregion

            #region Predicate Parents

            protected override void VisitComparison(Predicate.Comparison pred)
            {
                pred.Left = TryReplace(pred.Left);
                pred.Right = TryReplace(pred.Right);
            }

            protected override void VisitLike(Predicate.Like pred)
            {
                pred.Left = TryReplace(pred.Left);
                pred.Pattern = TryReplace(pred.Pattern);
                if (pred.EscapeExpr != null)
                {
                    pred.EscapeExpr = TryReplace(pred.EscapeExpr);
                }
            }

            protected override void VisitBetween(Predicate.Between pred)
            {
                pred.Expr = TryReplace(pred.Expr);
                pred.LowRangeExpr = TryReplace(pred.LowRangeExpr);
                pred.HighRangeExpr = TryReplace(pred.HighRangeExpr);
            }

            protected override void VisitNull(Predicate.Null pred)
            {
                pred.Expr = TryReplace(pred.Expr);
            }

            protected override void VisitContains(Predicate.Contains pred)
            {
                pred.SearchCondition = TryReplace(pred.SearchCondition);
                if (pred.Language != null)
                {
                    pred.Language = TryReplace(pred.Language);
                }
            }

            protected override void VisitFreetext(Predicate.Freetext pred)
            {
                pred.SearchCondition = TryReplace(pred.SearchCondition);
                if (pred.Language != null)
                {
                    pred.Language = TryReplace(pred.Language);
                }
            }

            protected override void VisitIn(Predicate.In pred)
            {
                pred.Expr = TryReplace(pred.Expr);
                if (pred.Subquery != null)
                {
                    Walk(pred.Subquery);
                }
                else if (pred.ValueList != null)
                {
                    for (int i = 0; i < pred.ValueList.Count; i++)
                    {
                        pred.ValueList[i] = (Expr)TryReplace(pred.ValueList[i]);
                    }
                }
            }

            protected override void VisitQuantifier(Predicate.Quantifier pred)
            {
                pred.Left = TryReplace(pred.Left);
                Walk(pred.Subquery);
            }

            #endregion
        }
    }
}
