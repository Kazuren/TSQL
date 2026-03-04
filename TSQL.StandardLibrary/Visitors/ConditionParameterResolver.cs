using System;
using System.Collections.Generic;
using System.Reflection;
using TSQL.AST;

namespace TSQL.StandardLibrary.Visitors
{
    internal static class ConditionParameterResolver
    {
        public static (string Condition, IReadOnlyDictionary<string, object> Parameters)
            Resolve(Stmt stmt, string condition, IEnumerable<object> values)
        {
            HashSet<string> existingVars = CollectVariableNames(stmt);
            return ResolveCore(existingVars, condition, values);
        }

        public static (string Condition, IReadOnlyDictionary<string, object> Parameters)
            Resolve(IReadOnlyList<Stmt> stmts, string condition, IEnumerable<object> values)
        {
            HashSet<string> existingVars = CollectVariableNames(stmts);
            return ResolveCore(existingVars, condition, values);
        }

        private static HashSet<string> CollectVariableNames(Stmt stmt)
        {
            VariableNameCollector collector = new VariableNameCollector();
            collector.Walk(stmt);
            return collector.Names;
        }

        private static HashSet<string> CollectVariableNames(IReadOnlyList<Stmt> stmts)
        {
            VariableNameCollector collector = new VariableNameCollector();
            foreach (Stmt stmt in stmts)
            {
                collector.Walk(stmt);
            }
            return collector.Names;
        }

        private static (string Condition, IReadOnlyDictionary<string, object> Parameters)
            ResolveCore(HashSet<string> existingVars, string condition, IEnumerable<object> values)
        {
            // 1. Build name-value dictionary from values
            Dictionary<string, object> paramValues = BuildParamValues(values);

            // 2. Early exit: no values means nothing to resolve
            if (paramValues.Count == 0)
            {
                return (condition, new Dictionary<string, object>());
            }

            // 3. Parse condition into Predicate AST
            Predicate predicate = Predicate.ParsePredicate(condition);

            // 4. Collect variable names in the condition
            VariableNameCollector conditionCollector = new VariableNameCollector();
            conditionCollector.Walk(predicate);
            HashSet<string> conditionVars = conditionCollector.Names;

            // 5. Filter: keep only paramValues entries whose key appears in condition
            Dictionary<string, object> filteredParams =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> kvp in paramValues)
            {
                if (conditionVars.Contains(kvp.Key.ToUpperInvariant()))
                {
                    filteredParams[kvp.Key] = kvp.Value;
                }
            }

            // 6. Detect collisions and build rename map
            Dictionary<string, string> renameMap =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> allUsedNames = new HashSet<string>(existingVars);
            foreach (string name in conditionVars)
            {
                allUsedNames.Add(name);
            }

            foreach (string paramKey in filteredParams.Keys)
            {
                string upperKey = paramKey.ToUpperInvariant();
                if (existingVars.Contains(upperKey))
                {
                    // Collision — find a unique name
                    string baseName = paramKey;
                    int suffix = 1;
                    string newName;
                    do
                    {
                        newName = baseName + "_" + suffix;
                        suffix++;
                    } while (allUsedNames.Contains(newName.ToUpperInvariant()));

                    allUsedNames.Add(newName.ToUpperInvariant());
                    renameMap[paramKey] = newName;
                }
            }

            // 7. Rename variables in the predicate if needed
            if (renameMap.Count > 0)
            {
                VariableRenamer renamer = new VariableRenamer(renameMap);
                renamer.Walk(predicate);
            }

            // 8. Build final parameters dictionary with renamed keys
            Dictionary<string, object> finalParams =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> kvp in filteredParams)
            {
                if (renameMap.TryGetValue(kvp.Key, out string renamedKey))
                {
                    finalParams[renamedKey] = kvp.Value;
                }
                else
                {
                    finalParams[kvp.Key] = kvp.Value;
                }
            }

            return (predicate.ToSource(), finalParams);
        }

        private static Dictionary<string, object> BuildParamValues(IEnumerable<object> values)
        {
            Dictionary<string, object> result =
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (object element in values)
            {
                (string name, object value) = ExtractNameAndValue(element, index);
                if (result.ContainsKey(name))
                {
                    throw new ArgumentException(
                        $"Duplicate parameter name: {name}");
                }
                result[name] = value;
                index++;
            }
            return result;
        }

        private static (string Name, object Value) ExtractNameAndValue(object element, int index)
        {
            if (element == null)
            {
                return ("@P" + index, null);
            }

            Type type = element.GetType();

            // Check for ValueTuple<string, T> — fields Item1, Item2
            if (type.IsValueType && type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef.FullName != null && genericDef.FullName.StartsWith("System.ValueTuple`2"))
                {
                    FieldInfo item1Field = type.GetField("Item1");
                    FieldInfo item2Field = type.GetField("Item2");
                    if (item1Field != null && item1Field.FieldType == typeof(string))
                    {
                        string name = (string)item1Field.GetValue(element);
                        object value = item2Field.GetValue(element);
                        ValidateParameterName(name);
                        return (name, value);
                    }
                }
            }

            // Check for KeyValuePair<string, T> — properties Key, Value
            if (type.IsValueType && type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(KeyValuePair<,>))
                {
                    PropertyInfo keyProp = type.GetProperty("Key");
                    if (keyProp != null && keyProp.PropertyType == typeof(string))
                    {
                        string name = (string)keyProp.GetValue(element);
                        object value = type.GetProperty("Value").GetValue(element);
                        ValidateParameterName(name);
                        return (name, value);
                    }
                }
            }

            // Check for Tuple<string, T> — properties Item1, Item2
            if (!type.IsValueType && type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef.FullName != null && genericDef.FullName.StartsWith("System.Tuple`2"))
                {
                    PropertyInfo item1Prop = type.GetProperty("Item1");
                    if (item1Prop != null && item1Prop.PropertyType == typeof(string))
                    {
                        string name = (string)item1Prop.GetValue(element);
                        object value = type.GetProperty("Item2").GetValue(element);
                        ValidateParameterName(name);
                        return (name, value);
                    }
                }
            }

            // Unnamed value — auto-name as @P{index}
            return ("@P" + index, element);
        }

        private static void ValidateParameterName(string name)
        {
            if (name == null || !name.StartsWith("@"))
            {
                throw new ArgumentException(
                    $"Parameter name must start with '@'. Got: {(name ?? "null")}");
            }
        }

        private class VariableRenamer : SqlWalker
        {
            private readonly Dictionary<string, string> _renameMap;

            public VariableRenamer(Dictionary<string, string> renameMap)
            {
                _renameMap = renameMap;
            }

            private Expr TryRename(Expr expr)
            {
                if (expr is Expr.Variable variable
                    && _renameMap.TryGetValue(variable.Name, out string newName))
                {
                    return new Expr.Variable(newName);
                }
                else
                {
                    Walk(expr);
                    return expr;
                }
            }

            #region Expr Parents

            protected override void VisitBinary(Expr.Binary expr)
            {
                expr.Left = TryRename(expr.Left);
                expr.Right = TryRename(expr.Right);
            }

            protected override void VisitUnary(Expr.Unary expr)
            {
                expr.Right = TryRename(expr.Right);
            }

            protected override void VisitGrouping(Expr.Grouping expr)
            {
                expr.Expression = TryRename(expr.Expression);
            }

            protected override void VisitFunctionCall(Expr.FunctionCall expr)
            {
                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    expr.Arguments[i] = (Expr)TryRename(expr.Arguments[i]);
                }
            }

            protected override void VisitSimpleCase(Expr.SimpleCase expr)
            {
                expr.Operand = TryRename(expr.Operand);
                foreach (Expr.SimpleCaseWhen when in expr.WhenClauses)
                {
                    when.Value = TryRename(when.Value);
                    when.Result = TryRename(when.Result);
                }
                if (expr.ElseResult != null)
                {
                    expr.ElseResult = TryRename(expr.ElseResult);
                }
            }

            protected override void VisitSearchedCase(Expr.SearchedCase expr)
            {
                foreach (Expr.SearchedCaseWhen when in expr.WhenClauses)
                {
                    Walk(when.Condition);
                    when.Result = TryRename(when.Result);
                }
                if (expr.ElseResult != null)
                {
                    expr.ElseResult = TryRename(expr.ElseResult);
                }
            }

            protected override void VisitCast(Expr.CastExpression expr)
            {
                expr.Expression = TryRename(expr.Expression);
            }

            protected override void VisitConvert(Expr.ConvertExpression expr)
            {
                expr.Expression = TryRename(expr.Expression);
                if (expr.Style != null)
                {
                    expr.Style = TryRename(expr.Style);
                }
            }

            protected override void VisitCollate(Expr.Collate expr)
            {
                expr.Expression = TryRename(expr.Expression);
            }

            protected override void VisitIif(Expr.Iif expr)
            {
                Walk(expr.Condition);
                expr.TrueValue = TryRename(expr.TrueValue);
                expr.FalseValue = TryRename(expr.FalseValue);
            }

            protected override void VisitAtTimeZone(Expr.AtTimeZone expr)
            {
                expr.Expression = TryRename(expr.Expression);
                expr.TimeZone = TryRename(expr.TimeZone);
            }

            protected override void VisitOpenXml(Expr.OpenXmlExpression expr)
            {
                VisitFunctionCall(expr);
            }

            #endregion

            #region Predicate Parents

            protected override void VisitComparison(Predicate.Comparison pred)
            {
                pred.Left = TryRename(pred.Left);
                pred.Right = TryRename(pred.Right);
            }

            protected override void VisitLike(Predicate.Like pred)
            {
                pred.Left = TryRename(pred.Left);
                pred.Pattern = TryRename(pred.Pattern);
                if (pred.EscapeExpr != null)
                {
                    pred.EscapeExpr = TryRename(pred.EscapeExpr);
                }
            }

            protected override void VisitBetween(Predicate.Between pred)
            {
                pred.Expr = TryRename(pred.Expr);
                pred.LowRangeExpr = TryRename(pred.LowRangeExpr);
                pred.HighRangeExpr = TryRename(pred.HighRangeExpr);
            }

            protected override void VisitNull(Predicate.Null pred)
            {
                pred.Expr = TryRename(pred.Expr);
            }

            protected override void VisitContains(Predicate.Contains pred)
            {
                pred.SearchCondition = TryRename(pred.SearchCondition);
                if (pred.Language != null)
                {
                    pred.Language = TryRename(pred.Language);
                }
            }

            protected override void VisitFreetext(Predicate.Freetext pred)
            {
                pred.SearchCondition = TryRename(pred.SearchCondition);
                if (pred.Language != null)
                {
                    pred.Language = TryRename(pred.Language);
                }
            }

            protected override void VisitIn(Predicate.In pred)
            {
                pred.Expr = TryRename(pred.Expr);
                if (pred.Subquery != null)
                {
                    Walk(pred.Subquery);
                }
                else if (pred.ValueList != null)
                {
                    for (int i = 0; i < pred.ValueList.Count; i++)
                    {
                        pred.ValueList[i] = (Expr)TryRename(pred.ValueList[i]);
                    }
                }
            }

            protected override void VisitQuantifier(Predicate.Quantifier pred)
            {
                pred.Left = TryRename(pred.Left);
                Walk(pred.Subquery);
            }

            #endregion
        }
    }
}
