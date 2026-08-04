using System;
using System.Collections.Generic;
using TSQL.AST;

namespace TSQL
{
    /// <summary>
    /// Deep-clone support for syntax trees. The clone is a fully independent tree:
    /// fresh nodes and fresh tokens, sharing only immutable trivia instances.
    /// </summary>
    public static class SyntaxElementCloneExtensions
    {
        /// <summary>
        /// Returns a deep copy of this element. Mutating the copy (or the original)
        /// never affects the other.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the element type has no clone support.</exception>
        public static T Clone<T>(this T element) where T : class, ISyntaxElement
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            SyntaxElement clone = Cloner.CloneElement((SyntaxElement)(object)element);
            SyntaxElement.BuildTokenChain(clone);
            return (T)(object)clone;
        }
    }

    /// <summary>
    /// Structural deep clone over the AST. Every node is rebuilt with cloned tokens;
    /// construction goes through token-preserving constructors (the same ones the
    /// parser uses) so no formatting-fabrication side effects leak into clones.
    /// </summary>
    internal sealed class Cloner :
        Expr.Visitor<Expr>,
        Predicate.Visitor<Predicate>,
        TableSource.Visitor<TableSource>,
        Stmt.Visitor<Stmt>
    {
        private static readonly Cloner Instance = new Cloner();

        private Cloner() { }

        #region Entry Point and Dispatch

        internal static SyntaxElement CloneElement(SyntaxElement element)
        {
            switch (element)
            {
                case null: return null;
                case Expr expr: return CloneExpr(expr);
                case Predicate predicate: return ClonePredicate(predicate);
                case TableSource tableSource: return CloneTableSource(tableSource);
                case Stmt stmt: return CloneStmt(stmt);
                case QueryExpression query: return CloneQuery(query);
                case Script script: return CloneScript(script);
                case OrderByClause orderBy: return CloneOrderByClause(orderBy);
                case OrderByItem orderByItem: return CloneOrderByItem(orderByItem);
                case GroupByClause groupBy: return CloneGroupByClause(groupBy);
                case GroupByItem groupByItem: return CloneGroupByItem(groupByItem);
                case SelectColumn selectColumn: return CloneSelectColumn(selectColumn);
                case SuffixAlias suffixAlias: return (SyntaxElement)CloneAlias(suffixAlias);
                case PrefixAlias prefixAlias: return (SyntaxElement)CloneAlias(prefixAlias);
                case TopClause top: return CloneTopClause(top);
                case FromClause from: return CloneFromClause(from);
                case OptionClause option: return CloneOptionClause(option);
                case QueryHint queryHint: return CloneQueryHint(queryHint);
                case ForClause forClause: return CloneForClause(forClause);
                case Cte cte: return CloneCte(cte);
                case CteDefinition cteDefinition: return CloneCteDefinition(cteDefinition);
                case DataType dataType: return CloneDataType(dataType);
                case OverClause over: return CloneOverClause(over);
                case WindowFrame frame: return CloneWindowFrame(frame);
                case WindowFrameBound bound: return CloneWindowFrameBound(bound);
                case Expr.WithinGroupClause withinGroup: return CloneWithinGroup(withinGroup);
                case Expr.SimpleCaseWhen simpleWhen: return CloneSimpleCaseWhen(simpleWhen);
                case Expr.SearchedCaseWhen searchedWhen: return CloneSearchedCaseWhen(searchedWhen);
                case Predicate.FullTextColumns fullText: return CloneFullTextColumns(fullText);
                case ValuesRow valuesRow: return CloneValuesRow(valuesRow);
                case DerivedColumnAliases derivedAliases: return CloneDerivedColumnAliases(derivedAliases);
                case ForSystemTimeClause forSystemTime: return CloneForSystemTime(forSystemTime);
                case TablesampleClause tablesample: return CloneTablesample(tablesample);
                case TableHintClause tableHintClause: return CloneTableHintClause(tableHintClause);
                case TableHint tableHint: return CloneTableHint(tableHint);
                case ServerName serverName: return new ServerName(CloneToken(serverName.FirstToken()));
                case DatabaseName databaseName: return new DatabaseName(CloneToken(databaseName.FirstToken()));
                case SchemaName schemaName: return new SchemaName(CloneToken(schemaName.FirstToken()));
                case ObjectName objectName: return new ObjectName(CloneToken(objectName.FirstToken()));
                case ColumnName columnName: return new ColumnName(CloneToken(columnName.FirstToken()));
                case InsertColumnList insertColumns: return CloneInsertColumnList(insertColumns);
                case InsertSource insertSource: return CloneInsertSource(insertSource);
                case ExecuteArgument executeArgument: return CloneExecuteArgument(executeArgument);
                case VariableDeclaration variableDeclaration: return CloneVariableDeclaration(variableDeclaration);
                default:
                    throw new NotSupportedException($"Cloning is not supported for {element.GetType().Name}.");
            }
        }

        #endregion

        #region Tokens and Shared Helpers

        internal static Token CloneToken(Token token)
        {
            if (token == null)
            {
                return null;
            }

            ConcreteToken clone = new ConcreteToken(token.Type, token.Lexeme, token.Literal);
            clone.AddLeadingTrivia(token.LeadingTrivia);
            foreach (Trivia trivia in token.TrailingTrivia)
            {
                clone.AddTrailingTrivia(trivia);
            }
            return clone;
        }

        private static SyntaxElementList<TItem> CloneList<TItem>(SyntaxElementList<TItem> list, Func<TItem, TItem> cloneItem)
            where TItem : class, ISyntaxElement
        {
            if (list == null)
            {
                return null;
            }
            return list.CloneWith(cloneItem, CloneToken);
        }

        /// <summary>
        /// Re-applies the source part's leading trivia onto the cloned part's first token.
        /// Needed after ObjectIdentifier/ColumnIdentifier constructors, which clear the
        /// leading trivia of every part that follows a dot.
        /// </summary>
        private static void RestoreLeadingTrivia(SyntaxElement source, SyntaxElement clone)
        {
            if (source == null || clone == null)
            {
                return;
            }

            Token sourceToken = SyntaxElement.FirstTokenOf(source);
            Token cloneToken = SyntaxElement.FirstTokenOf(clone);
            if (sourceToken == null || cloneToken == null)
            {
                return;
            }

            cloneToken.ClearLeadingTrivia();
            cloneToken.AddLeadingTrivia(sourceToken.LeadingTrivia);
        }

        private static Token FirstTokenClone(SqlName name)
        {
            return CloneToken(SyntaxElement.FirstTokenOf(name));
        }

        #endregion

        #region Expressions

        internal static Expr CloneExpr(Expr expr)
        {
            if (expr == null)
            {
                return null;
            }
            return expr.Accept((Expr.Visitor<Expr>)Instance);
        }

        Expr Expr.Visitor<Expr>.VisitBinaryExpr(Expr.Binary expr)
        {
            Expr.Binary clone = new Expr.Binary(CloneToken(expr._operatorToken));
            clone.Left = CloneExpr(expr.Left);
            clone.Right = CloneExpr(expr.Right);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitStringLiteralExpr(Expr.StringLiteral expr)
        {
            return new Expr.StringLiteral(CloneToken(expr._token));
        }

        Expr Expr.Visitor<Expr>.VisitIntLiteralExpr(Expr.IntLiteral expr)
        {
            return new Expr.IntLiteral(CloneToken(expr._token));
        }

        Expr Expr.Visitor<Expr>.VisitDecimalLiteralExpr(Expr.DecimalLiteral expr)
        {
            return new Expr.DecimalLiteral(CloneToken(expr._token));
        }

        Expr Expr.Visitor<Expr>.VisitNullLiteralExpr(Expr.NullLiteral expr)
        {
            return new Expr.NullLiteral(CloneToken(expr._token));
        }

        Expr Expr.Visitor<Expr>.VisitColumnIdentifierExpr(Expr.ColumnIdentifier expr)
        {
            return CloneColumnIdentifier(expr);
        }

        private static Expr.ColumnIdentifier CloneColumnIdentifier(Expr.ColumnIdentifier expr)
        {
            DatabaseName database = expr.DatabaseName == null ? null : new DatabaseName(FirstTokenClone(expr.DatabaseName));
            SchemaName schema = expr.SchemaName == null ? null : new SchemaName(FirstTokenClone(expr.SchemaName));
            ObjectName obj = expr.ObjectName == null ? null : new ObjectName(FirstTokenClone(expr.ObjectName));
            ColumnName column = new ColumnName(FirstTokenClone(expr.ColumnName));

            Expr.ColumnIdentifier clone;
            if (database != null && schema != null && obj != null)
            {
                clone = new Expr.ColumnIdentifier(database, schema, obj, column);
            }
            else if (database != null && schema == null && obj != null)
            {
                clone = new Expr.ColumnIdentifier(database, obj, column);
            }
            else if (schema != null && obj != null)
            {
                clone = new Expr.ColumnIdentifier(schema, obj, column);
            }
            else if (obj != null)
            {
                clone = new Expr.ColumnIdentifier(obj, column);
            }
            else
            {
                clone = new Expr.ColumnIdentifier(column);
            }

            clone._databaseToSchemaDot = CloneToken(expr._databaseToSchemaDot) ?? clone._databaseToSchemaDot;
            clone._schemaToObjectDot = CloneToken(expr._schemaToObjectDot) ?? clone._schemaToObjectDot;
            clone._objectToColumnDot = CloneToken(expr._objectToColumnDot) ?? clone._objectToColumnDot;

            // The constructors clear leading trivia on parts after dots; put the source trivia back.
            RestoreLeadingTrivia(expr.SchemaName, schema);
            RestoreLeadingTrivia(expr.ObjectName, obj);
            RestoreLeadingTrivia(expr.ColumnName, column);

            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitObjectIdentifierExpr(Expr.ObjectIdentifier expr)
        {
            return CloneObjectIdentifier(expr);
        }

        private static Expr.ObjectIdentifier CloneObjectIdentifier(Expr.ObjectIdentifier expr)
        {
            if (expr == null)
            {
                return null;
            }

            ServerName server = expr.ServerName == null ? null : new ServerName(FirstTokenClone(expr.ServerName));
            DatabaseName database = expr.DatabaseName == null ? null : new DatabaseName(FirstTokenClone(expr.DatabaseName));
            SchemaName schema = expr.SchemaName == null ? null : new SchemaName(FirstTokenClone(expr.SchemaName));
            ObjectName obj = new ObjectName(FirstTokenClone(expr.ObjectName));

            Expr.ObjectIdentifier clone;
            if (server != null)
            {
                clone = new Expr.ObjectIdentifier(server, database, schema, obj);
            }
            else if (database != null && schema != null)
            {
                clone = new Expr.ObjectIdentifier(database, schema, obj);
            }
            else if (database != null)
            {
                clone = new Expr.ObjectIdentifier(database, obj);
            }
            else if (schema != null)
            {
                clone = new Expr.ObjectIdentifier(schema, obj);
            }
            else
            {
                clone = new Expr.ObjectIdentifier(obj);
            }

            clone._serverToDatabaseDot = CloneToken(expr._serverToDatabaseDot) ?? clone._serverToDatabaseDot;
            clone._databaseToSchemaDot = CloneToken(expr._databaseToSchemaDot) ?? clone._databaseToSchemaDot;
            clone._schemaToObjectDot = CloneToken(expr._schemaToObjectDot) ?? clone._schemaToObjectDot;

            RestoreLeadingTrivia(expr.DatabaseName, database);
            RestoreLeadingTrivia(expr.SchemaName, schema);
            RestoreLeadingTrivia(expr.ObjectName, obj);

            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitWildcardExpr(Expr.Wildcard expr)
        {
            return new Expr.Wildcard(CloneToken(expr._wildcardToken));
        }

        Expr Expr.Visitor<Expr>.VisitQualifiedWildcardExpr(Expr.QualifiedWildcard expr)
        {
            DatabaseName database = expr.DatabaseName == null ? null : new DatabaseName(FirstTokenClone(expr.DatabaseName));
            SchemaName schema = expr.SchemaName == null ? null : new SchemaName(FirstTokenClone(expr.SchemaName));
            ObjectName obj = new ObjectName(FirstTokenClone(expr.ObjectName));
            Token wildcard = CloneToken(expr._wildcardToken);

            Expr.QualifiedWildcard clone;
            if (database != null && schema != null)
            {
                clone = new Expr.QualifiedWildcard(database, schema, obj, wildcard);
            }
            else if (database != null)
            {
                clone = new Expr.QualifiedWildcard(database, obj, wildcard);
            }
            else if (schema != null)
            {
                clone = new Expr.QualifiedWildcard(schema, obj, wildcard);
            }
            else
            {
                clone = new Expr.QualifiedWildcard(obj, wildcard);
            }

            clone._databaseToSchemaDot = CloneToken(expr._databaseToSchemaDot);
            clone._schemaToObjectDot = CloneToken(expr._schemaToObjectDot);
            clone._objectToStarDot = CloneToken(expr._objectToStarDot);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitUnaryExpr(Expr.Unary expr)
        {
            return new Expr.Unary(CloneToken(expr._operatorToken), CloneExpr(expr.Right));
        }

        Expr Expr.Visitor<Expr>.VisitGroupingExpr(Expr.Grouping expr)
        {
            return new Expr.Grouping(CloneExpr(expr.Expression), CloneToken(expr._leftParen), CloneToken(expr._rightParen));
        }

        Expr Expr.Visitor<Expr>.VisitSubqueryExpr(Expr.Subquery expr)
        {
            return CloneSubquery(expr);
        }

        private static Expr.Subquery CloneSubquery(Expr.Subquery expr)
        {
            if (expr == null)
            {
                return null;
            }
            return new Expr.Subquery(CloneQuery(expr.Query), CloneToken(expr._leftParen), CloneToken(expr._rightParen));
        }

        Expr Expr.Visitor<Expr>.VisitFunctionCallExpr(Expr.FunctionCall expr)
        {
            return CloneFunctionCall(expr);
        }

        private static Expr.FunctionCall CloneFunctionCall(Expr.FunctionCall expr)
        {
            if (expr == null)
            {
                return null;
            }

            Expr.FunctionCall clone = new Expr.FunctionCall(CloneObjectIdentifier(expr.Callee), CloneList(expr.Arguments, CloneExpr));
            clone._leftParen = CloneToken(expr._leftParen);
            clone._quantifierKeyword = CloneToken(expr._quantifierKeyword);
            clone._rightParen = CloneToken(expr._rightParen);
            clone.Quantifier = expr.Quantifier;
            if (expr.WithinGroup != null)
            {
                clone.WithinGroup = CloneWithinGroup(expr.WithinGroup);
            }
            return clone;
        }

        private static Expr.WithinGroupClause CloneWithinGroup(Expr.WithinGroupClause clause)
        {
            Expr.WithinGroupClause clone = new Expr.WithinGroupClause(CloneList(clause.OrderBy, CloneOrderByItem));
            clone._withinKeyword = CloneToken(clause._withinKeyword);
            clone._groupKeyword = CloneToken(clause._groupKeyword);
            clone._leftParen = CloneToken(clause._leftParen);
            clone._orderKeyword = CloneToken(clause._orderKeyword);
            clone._byKeyword = CloneToken(clause._byKeyword);
            clone._rightParen = CloneToken(clause._rightParen);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitVariableExpr(Expr.Variable expr)
        {
            return new Expr.Variable(CloneToken(SyntaxElement.FirstTokenOf(expr)));
        }

        Expr Expr.Visitor<Expr>.VisitWindowFunctionExpr(Expr.WindowFunction expr)
        {
            return new Expr.WindowFunction(CloneFunctionCall(expr.Function), CloneOverClause(expr.Over));
        }

        private static OverClause CloneOverClause(OverClause over)
        {
            if (over == null)
            {
                return null;
            }

            OverClause clone = new OverClause
            {
                PartitionBy = CloneList(over.PartitionBy, CloneExpr),
                OrderBy = CloneList(over.OrderBy, CloneOrderByItem),
                Frame = CloneWindowFrame(over.Frame)
            };
            clone._overKeyword = CloneToken(over._overKeyword);
            clone._leftParen = CloneToken(over._leftParen);
            clone._rightParen = CloneToken(over._rightParen);
            clone._partitionKeyword = CloneToken(over._partitionKeyword);
            clone._partitionByKeyword = CloneToken(over._partitionByKeyword);
            clone._orderKeyword = CloneToken(over._orderKeyword);
            clone._orderByKeyword = CloneToken(over._orderByKeyword);
            return clone;
        }

        private static WindowFrame CloneWindowFrame(WindowFrame frame)
        {
            if (frame == null)
            {
                return null;
            }

            WindowFrame clone = new WindowFrame(frame.FrameType, CloneWindowFrameBound(frame.Start), CloneWindowFrameBound(frame.End));
            clone._rowsOrRangeToken = CloneToken(frame._rowsOrRangeToken);
            clone._betweenToken = CloneToken(frame._betweenToken);
            clone._andToken = CloneToken(frame._andToken);
            return clone;
        }

        private static WindowFrameBound CloneWindowFrameBound(WindowFrameBound bound)
        {
            if (bound == null)
            {
                return null;
            }

            WindowFrameBound clone = new WindowFrameBound(bound.BoundType, CloneExpr(bound.Offset));
            clone._unboundedToken = CloneToken(bound._unboundedToken);
            clone._currentToken = CloneToken(bound._currentToken);
            clone._rowToken = CloneToken(bound._rowToken);
            clone._precedingToken = CloneToken(bound._precedingToken);
            clone._followingToken = CloneToken(bound._followingToken);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitSimpleCaseExpr(Expr.SimpleCase expr)
        {
            List<Expr.SimpleCaseWhen> whens = new List<Expr.SimpleCaseWhen>(expr.WhenClauses.Count);
            foreach (Expr.SimpleCaseWhen when in expr.WhenClauses)
            {
                whens.Add(CloneSimpleCaseWhen(when));
            }

            Expr.SimpleCase clone = new Expr.SimpleCase(CloneExpr(expr.Operand), whens, CloneExpr(expr.ElseResult));
            clone._caseToken = CloneToken(expr._caseToken);
            clone._elseToken = CloneToken(expr._elseToken);
            clone._endToken = CloneToken(expr._endToken);
            return clone;
        }

        private static Expr.SimpleCaseWhen CloneSimpleCaseWhen(Expr.SimpleCaseWhen when)
        {
            Expr.SimpleCaseWhen clone = new Expr.SimpleCaseWhen(CloneExpr(when.Value), CloneExpr(when.Result));
            clone._whenToken = CloneToken(when._whenToken);
            clone._thenToken = CloneToken(when._thenToken);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitSearchedCaseExpr(Expr.SearchedCase expr)
        {
            List<Expr.SearchedCaseWhen> whens = new List<Expr.SearchedCaseWhen>(expr.WhenClauses.Count);
            foreach (Expr.SearchedCaseWhen when in expr.WhenClauses)
            {
                whens.Add(CloneSearchedCaseWhen(when));
            }

            Expr.SearchedCase clone = new Expr.SearchedCase(whens, CloneExpr(expr.ElseResult));
            clone._caseToken = CloneToken(expr._caseToken);
            clone._elseToken = CloneToken(expr._elseToken);
            clone._endToken = CloneToken(expr._endToken);
            return clone;
        }

        private static Expr.SearchedCaseWhen CloneSearchedCaseWhen(Expr.SearchedCaseWhen when)
        {
            Expr.SearchedCaseWhen clone = new Expr.SearchedCaseWhen(ClonePredicate(when.Condition), CloneExpr(when.Result));
            clone._whenToken = CloneToken(when._whenToken);
            clone._thenToken = CloneToken(when._thenToken);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitCastExpr(Expr.CastExpression expr)
        {
            Expr.CastExpression clone = new Expr.CastExpression(CloneExpr(expr.Expression), CloneDataType(expr.DataType), expr.Kind);
            clone._castKeyword = CloneToken(expr._castKeyword);
            clone._leftParen = CloneToken(expr._leftParen);
            clone._asToken = CloneToken(expr._asToken);
            clone._rightParen = CloneToken(expr._rightParen);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitConvertExpr(Expr.ConvertExpression expr)
        {
            Expr.ConvertExpression clone = new Expr.ConvertExpression(CloneDataType(expr.DataType), CloneExpr(expr.Expression), CloneExpr(expr.Style), expr.Kind);
            clone._convertKeyword = CloneToken(expr._convertKeyword);
            clone._leftParen = CloneToken(expr._leftParen);
            clone._commaAfterType = CloneToken(expr._commaAfterType);
            clone._commaAfterExpr = CloneToken(expr._commaAfterExpr);
            clone._rightParen = CloneToken(expr._rightParen);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitCollateExpr(Expr.Collate expr)
        {
            Expr.Collate clone = new Expr.Collate(CloneExpr(expr.Expression));
            clone._collateKeyword = CloneToken(expr._collateKeyword);
            clone._collationName = CloneToken(expr._collationName);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitIifExpr(Expr.Iif expr)
        {
            Expr.Iif clone = new Expr.Iif(ClonePredicate(expr.Condition), CloneExpr(expr.TrueValue), CloneExpr(expr.FalseValue));
            clone._iifKeyword = CloneToken(expr._iifKeyword);
            clone._leftParen = CloneToken(expr._leftParen);
            clone._firstComma = CloneToken(expr._firstComma);
            clone._secondComma = CloneToken(expr._secondComma);
            clone._rightParen = CloneToken(expr._rightParen);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitAtTimeZoneExpr(Expr.AtTimeZone expr)
        {
            Expr.AtTimeZone clone = new Expr.AtTimeZone(CloneExpr(expr.Expression), CloneExpr(expr.TimeZone));
            clone._atKeyword = CloneToken(expr._atKeyword);
            clone._timeKeyword = CloneToken(expr._timeKeyword);
            clone._zoneKeyword = CloneToken(expr._zoneKeyword);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitOpenXmlExpr(Expr.OpenXmlExpression expr)
        {
            Expr.FunctionCall functionClone = CloneFunctionCall(expr);
            Expr.OpenXmlExpression clone = new Expr.OpenXmlExpression(functionClone);
            clone._quantifierKeyword = functionClone._quantifierKeyword;
            clone.Quantifier = expr.Quantifier;
            clone.WithinGroup = functionClone.WithinGroup;
            clone.WithClause = CloneOpenXmlWithClause(expr.WithClause);
            return clone;
        }

        private static Expr.OpenXmlWithClause CloneOpenXmlWithClause(Expr.OpenXmlWithClause clause)
        {
            if (clause == null)
            {
                return null;
            }

            Expr.OpenXmlWithClause clone;
            if (clause is Expr.OpenXmlSchemaDeclaration schemaDeclaration)
            {
                clone = new Expr.OpenXmlSchemaDeclaration(CloneList(schemaDeclaration.Columns, CloneOpenXmlColumnDef));
            }
            else
            {
                Expr.OpenXmlTableName tableName = (Expr.OpenXmlTableName)clause;
                clone = new Expr.OpenXmlTableName(CloneObjectIdentifier(tableName.TableName));
            }

            clone._withKeyword = CloneToken(clause._withKeyword);
            clone._leftParen = CloneToken(clause._leftParen);
            clone._rightParen = CloneToken(clause._rightParen);
            return clone;
        }

        private static Expr.OpenXmlColumnDef CloneOpenXmlColumnDef(Expr.OpenXmlColumnDef columnDef)
        {
            Expr.OpenXmlColumnDef clone = new Expr.OpenXmlColumnDef(CloneDataType(columnDef.DataType));
            clone._name = CloneToken(columnDef._name);
            clone._colPatternToken = CloneToken(columnDef._colPatternToken);
            return clone;
        }

        Expr Expr.Visitor<Expr>.VisitMethodCallExpr(Expr.MethodCall expr)
        {
            Expr.MethodCall clone = new Expr.MethodCall(CloneExpr(expr.Object), CloneList(expr.Arguments, CloneExpr));
            clone._dot = CloneToken(expr._dot);
            clone._methodName = CloneToken(expr._methodName);
            clone._leftParen = CloneToken(expr._leftParen);
            clone._rightParen = CloneToken(expr._rightParen);
            return clone;
        }

        private static DataType CloneDataType(DataType dataType)
        {
            if (dataType == null)
            {
                return null;
            }

            DataType clone;
            if (dataType.Parameters != null)
            {
                clone = new DataType(CloneToken(dataType._typeNameToken), CloneList(dataType.Parameters, CloneExpr));
                clone._leftParen = CloneToken(dataType._leftParen);
                clone._rightParen = CloneToken(dataType._rightParen);
            }
            else
            {
                clone = new DataType(CloneToken(dataType._typeNameToken));
            }
            return clone;
        }

        #endregion

        #region Predicates

        internal static Predicate ClonePredicate(Predicate predicate)
        {
            if (predicate == null)
            {
                return null;
            }
            return predicate.Accept((Predicate.Visitor<Predicate>)Instance);
        }

        Predicate Predicate.Visitor<Predicate>.VisitComparisonPredicate(Predicate.Comparison predicate)
        {
            return new Predicate.Comparison(
                CloneExpr(predicate.Left),
                CloneToken(predicate._operatorToken),
                CloneToken(predicate._operatorToken2),
                predicate.Operator,
                CloneExpr(predicate.Right));
        }

        Predicate Predicate.Visitor<Predicate>.VisitLikePredicate(Predicate.Like predicate)
        {
            Predicate.Like clone = new Predicate.Like(CloneExpr(predicate.Left), CloneExpr(predicate.Pattern), CloneExpr(predicate.EscapeExpr), predicate.Negated);
            clone._notToken = CloneToken(predicate._notToken);
            clone._likeToken = CloneToken(predicate._likeToken);
            clone._escapeToken = CloneToken(predicate._escapeToken);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitBetweenPredicate(Predicate.Between predicate)
        {
            Predicate.Between clone = new Predicate.Between(CloneExpr(predicate.Expr), CloneExpr(predicate.LowRangeExpr), CloneExpr(predicate.HighRangeExpr), predicate.Negated);
            clone._notToken = CloneToken(predicate._notToken);
            clone._betweenToken = CloneToken(predicate._betweenToken);
            clone._andToken = CloneToken(predicate._andToken);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitNullPredicate(Predicate.Null predicate)
        {
            Predicate.Null clone = new Predicate.Null(CloneExpr(predicate.Expr), predicate.Negated);
            clone._isToken = CloneToken(predicate._isToken);
            clone._notToken = CloneToken(predicate._notToken);
            clone._nullToken = CloneToken(predicate._nullToken);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitContainsPredicate(Predicate.Contains predicate)
        {
            Predicate.Contains clone = new Predicate.Contains(CloneFullTextColumns(predicate.Columns), CloneExpr(predicate.SearchCondition));
            clone.Language = CloneExpr(predicate.Language);
            clone._containsToken = CloneToken(predicate._containsToken);
            clone._leftParen = CloneToken(predicate._leftParen);
            clone._comma = CloneToken(predicate._comma);
            clone._languageComma = CloneToken(predicate._languageComma);
            clone._languageKeyword = CloneToken(predicate._languageKeyword);
            clone._rightParen = CloneToken(predicate._rightParen);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitFreetextPredicate(Predicate.Freetext predicate)
        {
            Predicate.Freetext clone = new Predicate.Freetext(CloneFullTextColumns(predicate.Columns), CloneExpr(predicate.SearchCondition));
            clone.Language = CloneExpr(predicate.Language);
            clone._freetextToken = CloneToken(predicate._freetextToken);
            clone._leftParen = CloneToken(predicate._leftParen);
            clone._comma = CloneToken(predicate._comma);
            clone._languageComma = CloneToken(predicate._languageComma);
            clone._languageKeyword = CloneToken(predicate._languageKeyword);
            clone._rightParen = CloneToken(predicate._rightParen);
            return clone;
        }

        private static Predicate.FullTextColumns CloneFullTextColumns(Predicate.FullTextColumns columns)
        {
            if (columns == null)
            {
                return null;
            }

            if (columns is Predicate.FullTextAllColumns allColumns)
            {
                Predicate.FullTextAllColumns allClone = new Predicate.FullTextAllColumns();
                allClone._wildcardToken = CloneToken(allColumns._wildcardToken);
                return allClone;
            }

            Predicate.FullTextColumnNames names = (Predicate.FullTextColumnNames)columns;
            Predicate.FullTextColumnNames namesClone = new Predicate.FullTextColumnNames(CloneList(names.Columns, CloneColumnIdentifier));
            namesClone._leftParen = CloneToken(names._leftParen);
            namesClone._rightParen = CloneToken(names._rightParen);
            return namesClone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitInPredicate(Predicate.In predicate)
        {
            Predicate.In clone;
            if (predicate.Subquery != null)
            {
                clone = new Predicate.In(CloneExpr(predicate.Expr), predicate.Negated, CloneSubquery(predicate.Subquery));
            }
            else
            {
                clone = new Predicate.In(CloneExpr(predicate.Expr), predicate.Negated, CloneList(predicate.ValueList, CloneExpr));
            }
            clone._notToken = CloneToken(predicate._notToken);
            clone._inToken = CloneToken(predicate._inToken);
            clone._leftParen = CloneToken(predicate._leftParen);
            clone._rightParen = CloneToken(predicate._rightParen);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitQuantifierPredicate(Predicate.Quantifier predicate)
        {
            return new Predicate.Quantifier(
                CloneExpr(predicate.Left),
                CloneToken(predicate._operatorToken),
                CloneToken(predicate._operatorToken2),
                predicate.Operator,
                CloneToken(predicate._quantifierToken),
                CloneSubquery(predicate.Subquery));
        }

        Predicate Predicate.Visitor<Predicate>.VisitExistsPredicate(Predicate.Exists predicate)
        {
            Predicate.Exists clone = new Predicate.Exists(CloneSubquery(predicate.Subquery));
            clone._existsToken = CloneToken(predicate._existsToken);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitGroupingPredicate(Predicate.Grouping predicate)
        {
            Predicate.Grouping clone = new Predicate.Grouping(ClonePredicate(predicate.Predicate));
            clone._leftParen = CloneToken(predicate._leftParen);
            clone._rightParen = CloneToken(predicate._rightParen);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitAndPredicate(Predicate.And predicate)
        {
            Predicate.And clone = new Predicate.And(ClonePredicate(predicate.Left), ClonePredicate(predicate.Right));
            clone._andToken = CloneToken(predicate._andToken);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitOrPredicate(Predicate.Or predicate)
        {
            Predicate.Or clone = new Predicate.Or(ClonePredicate(predicate.Left), ClonePredicate(predicate.Right));
            clone._orToken = CloneToken(predicate._orToken);
            return clone;
        }

        Predicate Predicate.Visitor<Predicate>.VisitNotPredicate(Predicate.Not predicate)
        {
            Predicate.Not clone = new Predicate.Not(ClonePredicate(predicate.Predicate));
            clone._notToken = CloneToken(predicate._notToken);
            return clone;
        }

        #endregion

        #region Table Sources

        internal static TableSource CloneTableSource(TableSource source)
        {
            if (source == null)
            {
                return null;
            }
            return source.Accept((TableSource.Visitor<TableSource>)Instance);
        }

        private static Alias CloneAlias(Alias alias)
        {
            if (alias == null)
            {
                return null;
            }

            if (alias is SuffixAlias suffix)
            {
                SuffixAlias clone = new SuffixAlias(CloneToken(suffix._nameToken));
                clone._asKeyword = CloneToken(suffix._asKeyword);
                return clone;
            }

            PrefixAlias prefix = (PrefixAlias)alias;
            return new PrefixAlias(CloneToken(prefix._nameToken), CloneToken(prefix._equalsToken));
        }

        TableSource TableSource.Visitor<TableSource>.VisitTableReference(TableReference source)
        {
            TableReference clone = new TableReference(CloneObjectIdentifier(source.TableName));
            clone.ForSystemTime = CloneForSystemTime(source.ForSystemTime);
            clone.Alias = CloneAlias(source.Alias);
            clone.Tablesample = CloneTablesample(source.Tablesample);
            clone.TableHints = CloneTableHintClause(source.TableHints);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitSubqueryReference(SubqueryReference source)
        {
            SubqueryReference clone = new SubqueryReference(CloneSubquery(source.Subquery));
            clone.Alias = CloneAlias(source.Alias);
            clone.ColumnAliases = CloneDerivedColumnAliases(source.ColumnAliases);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitTableVariableReference(TableVariableReference source)
        {
            TableVariableReference clone = new TableVariableReference(CloneToken(source._variableToken));
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitQualifiedJoin(QualifiedJoin source)
        {
            QualifiedJoin clone = new QualifiedJoin(
                CloneTableSource(source.Left),
                CloneTableSource(source.Right),
                source.JoinType,
                ClonePredicate(source.OnCondition),
                source.JoinHint);
            clone._joinHintToken = CloneToken(source._joinHintToken);
            clone._joinTypeToken = CloneToken(source._joinTypeToken);
            clone._outerToken = CloneToken(source._outerToken);
            clone._joinToken = CloneToken(source._joinToken);
            clone._onToken = CloneToken(source._onToken);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitCrossJoin(CrossJoin source)
        {
            CrossJoin clone = new CrossJoin(CloneTableSource(source.Left), CloneTableSource(source.Right));
            clone._crossToken = CloneToken(source._crossToken);
            clone._joinToken = CloneToken(source._joinToken);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitApplyJoin(ApplyJoin source)
        {
            ApplyJoin clone = new ApplyJoin(CloneTableSource(source.Left), CloneTableSource(source.Right), source.ApplyType);
            clone._applyTypeToken = CloneToken(source._applyTypeToken);
            clone._applyToken = CloneToken(source._applyToken);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitParenthesizedTableSource(ParenthesizedTableSource source)
        {
            ParenthesizedTableSource clone = new ParenthesizedTableSource(CloneTableSource(source.Inner));
            clone._leftParen = CloneToken(source._leftParen);
            clone._rightParen = CloneToken(source._rightParen);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitPivotTableSource(PivotTableSource source)
        {
            PivotTableSource clone = new PivotTableSource(
                CloneTableSource(source.Source),
                CloneFunctionCall(source.AggregateFunction),
                CloneObjectIdentifier(source.PivotColumn),
                CloneList(source.ValueList, CloneColumnName));
            clone._pivotToken = CloneToken(source._pivotToken);
            clone._leftParen = CloneToken(source._leftParen);
            clone._forToken = CloneToken(source._forToken);
            clone._inToken = CloneToken(source._inToken);
            clone._inLeftParen = CloneToken(source._inLeftParen);
            clone._inRightParen = CloneToken(source._inRightParen);
            clone._rightParen = CloneToken(source._rightParen);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitUnpivotTableSource(UnpivotTableSource source)
        {
            UnpivotTableSource clone = new UnpivotTableSource(
                CloneTableSource(source.Source),
                CloneObjectIdentifier(source.ValueColumn),
                CloneObjectIdentifier(source.PivotColumn),
                CloneList(source.ColumnList, CloneColumnName));
            clone._unpivotToken = CloneToken(source._unpivotToken);
            clone._leftParen = CloneToken(source._leftParen);
            clone._forToken = CloneToken(source._forToken);
            clone._inToken = CloneToken(source._inToken);
            clone._inLeftParen = CloneToken(source._inLeftParen);
            clone._inRightParen = CloneToken(source._inRightParen);
            clone._rightParen = CloneToken(source._rightParen);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitValuesTableSource(ValuesTableSource source)
        {
            ValuesTableSource clone = new ValuesTableSource(CloneList(source.Rows, CloneValuesRow));
            clone._outerLeftParen = CloneToken(source._outerLeftParen);
            clone._valuesToken = CloneToken(source._valuesToken);
            clone._outerRightParen = CloneToken(source._outerRightParen);
            clone.Alias = CloneAlias(source.Alias);
            clone.ColumnAliases = CloneDerivedColumnAliases(source.ColumnAliases);
            return clone;
        }

        TableSource TableSource.Visitor<TableSource>.VisitRowsetFunctionReference(RowsetFunctionReference source)
        {
            RowsetFunctionReference clone = new RowsetFunctionReference(CloneFunctionCall(source.FunctionCall));
            clone.WithClause = CloneRowsetSchemaDeclaration(source.WithClause);
            clone.Alias = CloneAlias(source.Alias);
            return clone;
        }

        private static RowsetSchemaDeclaration CloneRowsetSchemaDeclaration(RowsetSchemaDeclaration declaration)
        {
            if (declaration == null)
            {
                return null;
            }

            RowsetSchemaDeclaration clone = new RowsetSchemaDeclaration(CloneList(declaration.Columns, CloneRowsetColumnDef));
            clone._withKeyword = CloneToken(declaration._withKeyword);
            clone._leftParen = CloneToken(declaration._leftParen);
            clone._rightParen = CloneToken(declaration._rightParen);
            return clone;
        }

        private static RowsetColumnDef CloneRowsetColumnDef(RowsetColumnDef columnDef)
        {
            RowsetColumnDef clone = new RowsetColumnDef(CloneDataType(columnDef.DataType));
            clone._name = CloneToken(columnDef._name);
            clone._columnPath = CloneToken(columnDef._columnPath);
            clone._asKeyword = CloneToken(columnDef._asKeyword);
            clone._jsonKeyword = CloneToken(columnDef._jsonKeyword);
            return clone;
        }

        private static ColumnName CloneColumnName(ColumnName columnName)
        {
            return new ColumnName(FirstTokenClone(columnName));
        }

        private static ValuesRow CloneValuesRow(ValuesRow row)
        {
            ValuesRow clone = new ValuesRow(CloneList(row.Values, CloneExpr));
            clone._leftParen = CloneToken(row._leftParen);
            clone._rightParen = CloneToken(row._rightParen);
            return clone;
        }

        private static DerivedColumnAliases CloneDerivedColumnAliases(DerivedColumnAliases aliases)
        {
            if (aliases == null)
            {
                return null;
            }

            DerivedColumnAliases clone = new DerivedColumnAliases(CloneList(aliases.ColumnNames, CloneColumnName));
            clone._leftParen = CloneToken(aliases._leftParen);
            clone._rightParen = CloneToken(aliases._rightParen);
            return clone;
        }

        private static ForSystemTimeClause CloneForSystemTime(ForSystemTimeClause clause)
        {
            if (clause == null)
            {
                return null;
            }

            ForSystemTimeClause clone = new ForSystemTimeClause(clause.TimeType, CloneExpr(clause.StartTime), CloneExpr(clause.EndTime));
            clone._forToken = CloneToken(clause._forToken);
            clone._systemTimeToken = CloneToken(clause._systemTimeToken);
            clone._typeKeyword1 = CloneToken(clause._typeKeyword1);
            clone._typeKeyword2 = CloneToken(clause._typeKeyword2);
            clone._leftParen = CloneToken(clause._leftParen);
            clone._comma = CloneToken(clause._comma);
            clone._rightParen = CloneToken(clause._rightParen);
            return clone;
        }

        private static TablesampleClause CloneTablesample(TablesampleClause clause)
        {
            if (clause == null)
            {
                return null;
            }

            TablesampleClause clone = new TablesampleClause(CloneExpr(clause.SampleSize), clause.Unit, CloneExpr(clause.RepeatSeed));
            clone._tablesampleToken = CloneToken(clause._tablesampleToken);
            clone._systemToken = CloneToken(clause._systemToken);
            clone._leftParen = CloneToken(clause._leftParen);
            clone._unitToken = CloneToken(clause._unitToken);
            clone._rightParen = CloneToken(clause._rightParen);
            clone._repeatableToken = CloneToken(clause._repeatableToken);
            clone._repeatLeftParen = CloneToken(clause._repeatLeftParen);
            clone._repeatRightParen = CloneToken(clause._repeatRightParen);
            return clone;
        }

        private static TableHintClause CloneTableHintClause(TableHintClause clause)
        {
            if (clause == null)
            {
                return null;
            }

            TableHintClause clone = new TableHintClause(CloneList(clause.Hints, CloneTableHint));
            clone._withToken = CloneToken(clause._withToken);
            clone._leftParen = CloneToken(clause._leftParen);
            clone._rightParen = CloneToken(clause._rightParen);
            return clone;
        }

        private static TableHint CloneTableHint(TableHint hint)
        {
            TableHint clone;
            if (hint.IndexValues != null)
            {
                clone = new TableHint(hint.HintType, CloneList(hint.IndexValues, CloneExpr));
            }
            else if (hint.ForceSeekIndexValue != null)
            {
                clone = new TableHint(hint.HintType, CloneExpr(hint.ForceSeekIndexValue), CloneList(hint.ForceSeekColumns, CloneColumnName));
            }
            else if (hint.SpatialMaxCellsValue != null)
            {
                clone = new TableHint(hint.HintType, CloneExpr(hint.SpatialMaxCellsValue));
            }
            else
            {
                clone = new TableHint(hint.HintType);
            }

            clone._hintToken = CloneToken(hint._hintToken);
            clone._equalsToken = CloneToken(hint._equalsToken);
            clone._leftParen = CloneToken(hint._leftParen);
            clone._rightParen = CloneToken(hint._rightParen);
            clone._innerLeftParen = CloneToken(hint._innerLeftParen);
            clone._innerRightParen = CloneToken(hint._innerRightParen);
            return clone;
        }

        #endregion

        #region Query Expressions and Clauses

        internal static QueryExpression CloneQuery(QueryExpression query)
        {
            if (query == null)
            {
                return null;
            }

            QueryExpression clone;
            if (query is SelectExpression select)
            {
                clone = CloneSelectExpression(select);
            }
            else if (query is SetOperation setOperation)
            {
                SetOperation setClone = new SetOperation(CloneQuery(setOperation.Left), CloneQuery(setOperation.Right), setOperation.OperationType);
                setClone._operatorToken = CloneToken(setOperation._operatorToken);
                setClone._allToken = CloneToken(setOperation._allToken);
                clone = setClone;
            }
            else
            {
                ParenthesizedQuery paren = (ParenthesizedQuery)query;
                ParenthesizedQuery parenClone = new ParenthesizedQuery(CloneQuery(paren.Inner));
                parenClone._leftParen = CloneToken(paren._leftParen);
                parenClone._rightParen = CloneToken(paren._rightParen);
                clone = parenClone;
            }

            clone.OrderBy = CloneOrderByClause(query.OrderBy);
            clone.For = CloneForClause(query.For);
            return clone;
        }

        private static SelectExpression CloneSelectExpression(SelectExpression select)
        {
            SelectExpression clone = new SelectExpression();
            clone._selectKeyword = CloneToken(select._selectKeyword);
            clone._quantifier = select._quantifier;
            clone._quantifierKeyword = CloneToken(select._quantifierKeyword);
            clone.Top = CloneTopClause(select.Top);
            clone._columns = CloneList(select._columns, CloneSelectItem);
            clone._into = CloneObjectIdentifier(select._into);
            clone._intoKeyword = CloneToken(select._intoKeyword);
            clone.From = CloneFromClause(select.From);
            if (select.Where != null)
            {
                clone.Where = ClonePredicate(select.Where);
                clone._whereKeyword = CloneToken(select._whereKeyword);
            }
            clone.GroupBy = CloneGroupByClause(select.GroupBy);
            if (select.Having != null)
            {
                clone.Having = ClonePredicate(select.Having);
                clone._havingKeyword = CloneToken(select._havingKeyword);
            }
            return clone;
        }

        private static SelectItem CloneSelectItem(SelectItem item)
        {
            if (item is SelectColumn column)
            {
                return CloneSelectColumn(column);
            }
            return (SelectItem)CloneExpr((Expr)item);
        }

        private static SelectColumn CloneSelectColumn(SelectColumn column)
        {
            return new SelectColumn(CloneExpr(column.Expression), CloneAlias(column.Alias));
        }

        private static TopClause CloneTopClause(TopClause top)
        {
            if (top == null)
            {
                return null;
            }

            TopClause clone = new TopClause(CloneExpr(top.Expression));
            clone.Modifiers = top.Modifiers;
            clone._topKeyword = CloneToken(top._topKeyword);
            clone._leftParen = CloneToken(top._leftParen);
            clone._rightParen = CloneToken(top._rightParen);
            clone._percentKeyword = CloneToken(top._percentKeyword);
            clone._withKeyword = CloneToken(top._withKeyword);
            clone._tiesKeyword = CloneToken(top._tiesKeyword);
            return clone;
        }

        private static FromClause CloneFromClause(FromClause from)
        {
            if (from == null)
            {
                return null;
            }

            FromClause clone = new FromClause(CloneToken(from._fromToken));
            clone.TableSources = CloneList(from.TableSources, CloneTableSource);
            return clone;
        }

        private static OrderByClause CloneOrderByClause(OrderByClause orderBy)
        {
            if (orderBy == null)
            {
                return null;
            }

            OrderByClause clone = new OrderByClause
            {
                Items = CloneList(orderBy.Items, CloneOrderByItem),
                OffsetCount = CloneExpr(orderBy.OffsetCount),
                FetchCount = CloneExpr(orderBy.FetchCount)
            };
            clone._orderKeyword = CloneToken(orderBy._orderKeyword);
            clone._orderByKeyword = CloneToken(orderBy._orderByKeyword);
            clone._offsetKeyword = CloneToken(orderBy._offsetKeyword);
            clone._offsetRowOrRows = CloneToken(orderBy._offsetRowOrRows);
            clone._fetchKeyword = CloneToken(orderBy._fetchKeyword);
            clone._firstOrNext = CloneToken(orderBy._firstOrNext);
            clone._fetchRowOrRows = CloneToken(orderBy._fetchRowOrRows);
            clone._onlyKeyword = CloneToken(orderBy._onlyKeyword);
            return clone;
        }

        internal static OrderByItem CloneOrderByItem(OrderByItem item)
        {
            OrderByItem clone = new OrderByItem();
            clone.Expression = CloneExpr(item.Expression);
            clone.Direction = item.Direction;
            clone._orderToken = CloneToken(item._orderToken);
            return clone;
        }

        private static GroupByClause CloneGroupByClause(GroupByClause groupBy)
        {
            if (groupBy == null)
            {
                return null;
            }

            return new GroupByClause(
                CloneToken(groupBy._groupKeyword),
                CloneToken(groupBy._byKeyword),
                CloneList(groupBy.Items, CloneGroupByItem));
        }

        private static GroupByItem CloneGroupByItem(GroupByItem item)
        {
            switch (item)
            {
                case GroupByExpression expression:
                    return new GroupByExpression(CloneExpr(expression.Expression));
                case GroupByGrandTotal grandTotal:
                    return new GroupByGrandTotal(CloneToken(grandTotal._leftParen), CloneToken(grandTotal._rightParen));
                case GroupByComposite composite:
                    return new GroupByComposite(CloneToken(composite._leftParen), CloneList(composite.Expressions, CloneExpr), CloneToken(composite._rightParen));
                case GroupByRollup rollup:
                    return new GroupByRollup(CloneToken(rollup._rollupKeyword), CloneToken(rollup._leftParen), CloneList(rollup.Items, CloneGroupByItem), CloneToken(rollup._rightParen));
                case GroupByCube cube:
                    return new GroupByCube(CloneToken(cube._cubeKeyword), CloneToken(cube._leftParen), CloneList(cube.Items, CloneGroupByItem), CloneToken(cube._rightParen));
                default:
                    GroupByGroupingSets sets = (GroupByGroupingSets)item;
                    return new GroupByGroupingSets(CloneToken(sets._groupingKeyword), CloneToken(sets._setsKeyword), CloneToken(sets._leftParen), CloneList(sets.Items, CloneGroupByItem), CloneToken(sets._rightParen));
            }
        }

        private static ForClause CloneForClause(ForClause forClause)
        {
            if (forClause == null)
            {
                return null;
            }

            ForClause clone;
            if (forClause is ForBrowseClause browse)
            {
                ForBrowseClause browseClone = new ForBrowseClause();
                browseClone._browseToken = CloneToken(browse._browseToken);
                clone = browseClone;
            }
            else if (forClause is ForXmlClause xml)
            {
                ForXmlClause xmlClone = new ForXmlClause(xml.Mode, CloneList(xml.Directives, CloneForDirective));
                xmlClone._xmlToken = CloneToken(xml._xmlToken);
                xmlClone._modeToken = CloneToken(xml._modeToken);
                xmlClone._modeLeftParen = CloneToken(xml._modeLeftParen);
                xmlClone._modeName = CloneToken(xml._modeName);
                xmlClone._modeRightParen = CloneToken(xml._modeRightParen);
                xmlClone._firstDirectiveComma = CloneToken(xml._firstDirectiveComma);
                clone = xmlClone;
            }
            else
            {
                ForJsonClause json = (ForJsonClause)forClause;
                ForJsonClause jsonClone = new ForJsonClause(json.Mode, CloneList(json.Directives, CloneForDirective));
                jsonClone._jsonToken = CloneToken(json._jsonToken);
                jsonClone._modeToken = CloneToken(json._modeToken);
                jsonClone._firstDirectiveComma = CloneToken(json._firstDirectiveComma);
                clone = jsonClone;
            }

            clone._forToken = CloneToken(forClause._forToken);
            return clone;
        }

        private static ForDirective CloneForDirective(ForDirective directive)
        {
            ForDirective clone = new ForDirective(directive.DirectiveType);
            clone._token1 = CloneToken(directive._token1);
            clone._token2 = CloneToken(directive._token2);
            clone._leftParen = CloneToken(directive._leftParen);
            clone._value = CloneToken(directive._value);
            clone._rightParen = CloneToken(directive._rightParen);
            return clone;
        }

        private static OptionClause CloneOptionClause(OptionClause option)
        {
            if (option == null)
            {
                return null;
            }

            OptionClause clone = new OptionClause(CloneList(option.Hints, CloneQueryHint));
            clone._optionToken = CloneToken(option._optionToken);
            clone._leftParen = CloneToken(option._leftParen);
            clone._rightParen = CloneToken(option._rightParen);
            return clone;
        }

        private static QueryHint CloneQueryHint(QueryHint hint)
        {
            QueryHint clone;
            switch (hint)
            {
                case SimpleQueryHint simple:
                    SimpleQueryHint simpleClone = new SimpleQueryHint(simple.HintType);
                    simpleClone._hintToken2 = CloneToken(simple._hintToken2);
                    clone = simpleClone;
                    break;
                case ValueQueryHint value:
                    ValueQueryHint valueClone = new ValueQueryHint(value.HintType, CloneExpr(value.Value));
                    valueClone._equalsToken = CloneToken(value._equalsToken);
                    clone = valueClone;
                    break;
                case ParameterizationQueryHint parameterization:
                    clone = new ParameterizationQueryHint(parameterization.ParameterizationMode, CloneToken(parameterization._modeToken));
                    break;
                case OptimizeForQueryHint optimizeFor:
                    OptimizeForQueryHint optimizeForClone = new OptimizeForQueryHint(CloneList(optimizeFor.OptimizeForVariables, CloneOptimizeForVariable));
                    optimizeForClone._forToken = CloneToken(optimizeFor._forToken);
                    optimizeForClone._leftParen = CloneToken(optimizeFor._leftParen);
                    optimizeForClone._rightParen = CloneToken(optimizeFor._rightParen);
                    clone = optimizeForClone;
                    break;
                case OptimizeForUnknownQueryHint optimizeForUnknown:
                    OptimizeForUnknownQueryHint unknownClone = new OptimizeForUnknownQueryHint();
                    unknownClone._forToken = CloneToken(optimizeForUnknown._forToken);
                    unknownClone._unknownToken = CloneToken(optimizeForUnknown._unknownToken);
                    clone = unknownClone;
                    break;
                case UseHintQueryHint useHint:
                    UseHintQueryHint useHintClone = new UseHintQueryHint(CloneList(useHint.UseHintNames, CloneExpr));
                    useHintClone._hintToken2 = CloneToken(useHint._hintToken2);
                    useHintClone._leftParen = CloneToken(useHint._leftParen);
                    useHintClone._rightParen = CloneToken(useHint._rightParen);
                    clone = useHintClone;
                    break;
                case UsePlanQueryHint usePlan:
                    UsePlanQueryHint usePlanClone = new UsePlanQueryHint(CloneExpr(usePlan.Value));
                    usePlanClone._planToken = CloneToken(usePlan._planToken);
                    clone = usePlanClone;
                    break;
                case TableHintQueryHint tableHint:
                    TableHintQueryHint tableHintClone = new TableHintQueryHint(CloneObjectIdentifier(tableHint.TableHintObjectName), CloneList(tableHint.TableHints, CloneTableHint));
                    tableHintClone._hintToken2 = CloneToken(tableHint._hintToken2);
                    tableHintClone._leftParen = CloneToken(tableHint._leftParen);
                    tableHintClone._rightParen = CloneToken(tableHint._rightParen);
                    tableHintClone._commaAfterObjectName = CloneToken(tableHint._commaAfterObjectName);
                    clone = tableHintClone;
                    break;
                default:
                    ForTimestampQueryHint forTimestamp = (ForTimestampQueryHint)hint;
                    ForTimestampQueryHint forTimestampClone = new ForTimestampQueryHint(CloneExpr(forTimestamp.Value));
                    forTimestampClone._timestampToken = CloneToken(forTimestamp._timestampToken);
                    forTimestampClone._asToken = CloneToken(forTimestamp._asToken);
                    forTimestampClone._ofToken = CloneToken(forTimestamp._ofToken);
                    clone = forTimestampClone;
                    break;
            }

            clone._hintToken = CloneToken(hint._hintToken);
            return clone;
        }

        private static OptimizeForVariable CloneOptimizeForVariable(OptimizeForVariable variable)
        {
            OptimizeForVariable clone;
            if (variable.LiteralValue != null)
            {
                clone = new OptimizeForVariable(CloneToken(variable._variableToken), CloneExpr(variable.LiteralValue));
                clone._equalsToken = CloneToken(variable._equalsToken);
            }
            else
            {
                clone = new OptimizeForVariable(CloneToken(variable._variableToken));
                clone._unknownToken = CloneToken(variable._unknownToken);
            }
            return clone;
        }

        private static Cte CloneCte(Cte cte)
        {
            if (cte == null)
            {
                return null;
            }

            Cte clone = new Cte(CloneToken(cte._withToken));
            clone.Ctes = CloneList(cte.Ctes, CloneCteDefinition);
            return clone;
        }

        private static CteDefinition CloneCteDefinition(CteDefinition definition)
        {
            CteDefinition clone = new CteDefinition();
            clone._nameToken = CloneToken(definition._nameToken);
            clone._asToken = CloneToken(definition._asToken);
            clone.ColumnNames = CloneCteColumnNames(definition.ColumnNames);
            clone.Query = CloneSubquery(definition.Query);
            return clone;
        }

        private static CteColumnNames CloneCteColumnNames(CteColumnNames columnNames)
        {
            if (columnNames == null)
            {
                return null;
            }

            CteColumnNames clone = new CteColumnNames();
            clone.ColumnNames = CloneList(columnNames.ColumnNames, CloneColumnName);
            clone._leftParen = CloneToken(columnNames._leftParen);
            clone._rightParen = CloneToken(columnNames._rightParen);
            return clone;
        }

        #endregion

        #region Statements

        internal static Stmt CloneStmt(Stmt stmt)
        {
            if (stmt == null)
            {
                return null;
            }
            return stmt.Accept((Stmt.Visitor<Stmt>)Instance);
        }

        private static Script CloneScript(Script script)
        {
            List<Stmt> statements = new List<Stmt>(script.Statements.Count);
            foreach (Stmt statement in script.Statements)
            {
                statements.Add(CloneStmt(statement));
            }

            List<Token> semicolons = new List<Token>(script._semicolons.Count);
            foreach (Token semicolon in script._semicolons)
            {
                semicolons.Add(CloneToken(semicolon));
            }

            return new Script(statements, semicolons);
        }

        Stmt Stmt.Visitor<Stmt>.VisitSelectStmt(Stmt.Select stmt)
        {
            Stmt.Select clone = new Stmt.Select(CloneQuery(stmt.Query));
            clone._cteStmt = CloneCte(stmt.CteStmt);
            clone.Option = CloneOptionClause(stmt.Option);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitInsertStmt(Stmt.Insert stmt)
        {
            Stmt.Insert clone = new Stmt.Insert(CloneExpr(stmt.Target), CloneInsertSource(stmt.Source));
            clone.CteStmt = CloneCte(stmt.CteStmt);
            clone.ColumnList = CloneInsertColumnList(stmt.ColumnList);
            clone._insertToken = CloneToken(stmt._insertToken);
            clone._intoToken = CloneToken(stmt._intoToken);
            return clone;
        }

        private static InsertColumnList CloneInsertColumnList(InsertColumnList columnList)
        {
            if (columnList == null)
            {
                return null;
            }

            InsertColumnList clone = new InsertColumnList(CloneList(columnList.Columns, CloneColumnName));
            clone._leftParen = CloneToken(columnList._leftParen);
            clone._rightParen = CloneToken(columnList._rightParen);
            return clone;
        }

        private static InsertSource CloneInsertSource(InsertSource source)
        {
            switch (source)
            {
                case SelectSource select:
                    SelectSource selectClone = new SelectSource(CloneQuery(select.Query));
                    selectClone.Option = CloneOptionClause(select.Option);
                    return selectClone;
                case ValuesSource values:
                    ValuesSource valuesClone = new ValuesSource(CloneList(values.Rows, CloneValuesRow));
                    valuesClone._valuesToken = CloneToken(values._valuesToken);
                    return valuesClone;
                case DefaultValuesSource defaults:
                    DefaultValuesSource defaultsClone = new DefaultValuesSource();
                    defaultsClone._defaultToken = CloneToken(defaults._defaultToken);
                    defaultsClone._valuesToken = CloneToken(defaults._valuesToken);
                    return defaultsClone;
                default:
                    ExecSource exec = (ExecSource)source;
                    ExecSource execClone = new ExecSource(CloneObjectIdentifier(exec.ProcedureName), CloneList(exec.Arguments, CloneExpr));
                    execClone._execToken = CloneToken(exec._execToken);
                    return execClone;
            }
        }

        Stmt Stmt.Visitor<Stmt>.VisitDropStmt(Stmt.Drop stmt)
        {
            Stmt.Drop clone = new Stmt.Drop(stmt.ObjectType, stmt.IfExists, CloneList(stmt.Targets, CloneObjectIdentifier));
            clone._dropToken = CloneToken(stmt._dropToken);
            clone._objectTypeToken = CloneToken(stmt._objectTypeToken);
            clone._ifToken = CloneToken(stmt._ifToken);
            clone._existsToken = CloneToken(stmt._existsToken);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitExecuteStmt(Stmt.Execute stmt)
        {
            Stmt.Execute clone = new Stmt.Execute(
                CloneToken(stmt._returnVariable),
                CloneToken(stmt._returnEqualsToken),
                CloneExpr(stmt.Target),
                CloneList(stmt.Arguments, CloneExecuteArgument));
            clone._execToken = CloneToken(stmt._execToken);
            clone.WithClause = CloneExecuteWithClause(stmt.WithClause);
            return clone;
        }

        private static ExecuteArgument CloneExecuteArgument(ExecuteArgument argument)
        {
            switch (argument)
            {
                case ValueArgument value:
                    return new ValueArgument(CloneToken(value._parameterNameToken), CloneToken(value._equalsToken), CloneExpr(value.Value));
                case DefaultArgument defaultArgument:
                    return new DefaultArgument(CloneToken(defaultArgument._parameterNameToken), CloneToken(defaultArgument._equalsToken), CloneToken(defaultArgument._defaultToken));
                default:
                    OutputArgument output = (OutputArgument)argument;
                    return new OutputArgument(CloneToken(output._parameterNameToken), CloneToken(output._equalsToken), CloneExpr(output.Value), CloneToken(output._outputToken));
            }
        }

        private static ExecuteWithClause CloneExecuteWithClause(ExecuteWithClause clause)
        {
            if (clause == null)
            {
                return null;
            }

            ExecuteWithClause clone = new ExecuteWithClause(clause.Recompile, CloneResultSetsSpec(clause.ResultSets));
            clone._withToken = CloneToken(clause._withToken);
            clone._recompileToken = CloneToken(clause._recompileToken);
            clone._commaToken = CloneToken(clause._commaToken);
            return clone;
        }

        private static ResultSetsSpec CloneResultSetsSpec(ResultSetsSpec spec)
        {
            switch (spec)
            {
                case null:
                    return null;
                case ResultSetsUndefined undefined:
                    ResultSetsUndefined undefinedClone = new ResultSetsUndefined();
                    undefinedClone._resultToken = CloneToken(undefined._resultToken);
                    undefinedClone._setsToken = CloneToken(undefined._setsToken);
                    undefinedClone._undefinedToken = CloneToken(undefined._undefinedToken);
                    return undefinedClone;
                case ResultSetsNone none:
                    ResultSetsNone noneClone = new ResultSetsNone();
                    noneClone._resultToken = CloneToken(none._resultToken);
                    noneClone._setsToken = CloneToken(none._setsToken);
                    noneClone._noneToken = CloneToken(none._noneToken);
                    return noneClone;
                default:
                    ResultSetsDefined defined = (ResultSetsDefined)spec;
                    ResultSetsDefined definedClone = new ResultSetsDefined(CloneList(defined.Definitions, CloneResultSetDefinition));
                    definedClone._resultToken = CloneToken(defined._resultToken);
                    definedClone._setsToken = CloneToken(defined._setsToken);
                    definedClone._outerLeftParen = CloneToken(defined._outerLeftParen);
                    definedClone._outerRightParen = CloneToken(defined._outerRightParen);
                    return definedClone;
            }
        }

        private static ResultSetDefinition CloneResultSetDefinition(ResultSetDefinition definition)
        {
            switch (definition)
            {
                case ColumnResultSet columns:
                    ColumnResultSet columnsClone = new ColumnResultSet(CloneList(columns.Columns, CloneResultSetColumn));
                    columnsClone._leftParen = CloneToken(columns._leftParen);
                    columnsClone._rightParen = CloneToken(columns._rightParen);
                    return columnsClone;
                case ObjectResultSet objectResult:
                    ObjectResultSet objectClone = new ObjectResultSet(CloneObjectIdentifier(objectResult.ObjectName));
                    objectClone._asToken = CloneToken(objectResult._asToken);
                    objectClone._objectToken = CloneToken(objectResult._objectToken);
                    return objectClone;
                case TypeResultSet typeResult:
                    TypeResultSet typeClone = new TypeResultSet(CloneObjectIdentifier(typeResult.TypeName));
                    typeClone._asToken = CloneToken(typeResult._asToken);
                    typeClone._typeToken = CloneToken(typeResult._typeToken);
                    return typeClone;
                default:
                    XmlResultSet xmlResult = (XmlResultSet)definition;
                    XmlResultSet xmlClone = new XmlResultSet();
                    xmlClone._asToken = CloneToken(xmlResult._asToken);
                    xmlClone._forToken = CloneToken(xmlResult._forToken);
                    xmlClone._xmlToken = CloneToken(xmlResult._xmlToken);
                    return xmlClone;
            }
        }

        private static ResultSetColumn CloneResultSetColumn(ResultSetColumn column)
        {
            ResultSetColumn clone = new ResultSetColumn(CloneToken(column._columnNameToken), CloneDataType(column.DataType));
            clone._collateToken = CloneToken(column._collateToken);
            clone._collationNameToken = CloneToken(column._collationNameToken);
            clone._notToken = CloneToken(column._notToken);
            clone._nullToken = CloneToken(column._nullToken);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitExecuteStringStmt(Stmt.ExecuteString stmt)
        {
            Stmt.ExecuteString clone = new Stmt.ExecuteString(CloneList(stmt.Expressions, CloneExpr));
            clone._execToken = CloneToken(stmt._execToken);
            clone._leftParen = CloneToken(stmt._leftParen);
            clone._rightParen = CloneToken(stmt._rightParen);
            clone.Context = CloneExecuteContext(stmt.Context);
            clone.AtClause = CloneExecuteAtClause(stmt.AtClause);
            return clone;
        }

        private static ExecuteContext CloneExecuteContext(ExecuteContext context)
        {
            if (context == null)
            {
                return null;
            }

            ExecuteContext clone = new ExecuteContext(context.ContextType);
            clone._asToken = CloneToken(context._asToken);
            clone._contextTypeToken = CloneToken(context._contextTypeToken);
            clone._equalsToken = CloneToken(context._equalsToken);
            clone._nameToken = CloneToken(context._nameToken);
            return clone;
        }

        private static ExecuteAtClause CloneExecuteAtClause(ExecuteAtClause atClause)
        {
            if (atClause == null)
            {
                return null;
            }

            ExecuteAtClause clone = new ExecuteAtClause(CloneToken(atClause._serverOrSourceNameToken), atClause.Target);
            clone._atToken = CloneToken(atClause._atToken);
            clone._dataSourceToken = CloneToken(atClause._dataSourceToken);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitDeclareStmt(Stmt.Declare stmt)
        {
            Stmt.Declare clone = new Stmt.Declare(CloneList(stmt.Declarations, CloneVariableDeclaration));
            clone._declareToken = CloneToken(stmt._declareToken);
            return clone;
        }

        private static VariableDeclaration CloneVariableDeclaration(VariableDeclaration declaration)
        {
            switch (declaration)
            {
                case ScalarVariableDeclaration scalar when scalar.Initializer != null:
                    return new ScalarVariableDeclaration(
                        CloneToken(scalar._variableToken),
                        CloneDataType(scalar.DataType),
                        CloneToken(scalar._equalsToken),
                        CloneExpr(scalar.Initializer));
                case ScalarVariableDeclaration scalar:
                    return new ScalarVariableDeclaration(CloneToken(scalar._variableToken), CloneDataType(scalar.DataType));
                case TableVariableDeclaration table:
                    return new TableVariableDeclaration(CloneToken(table._variableToken), CloneTableDefinition(table.TableDefinition));
                default:
                    return new VariableDeclaration(CloneToken(declaration._variableToken));
            }
        }

        private static TableDefinition CloneTableDefinition(TableDefinition definition)
        {
            return new TableDefinition(
                CloneToken(definition._tableToken),
                CloneToken(definition._leftParen),
                CloneList(definition.Columns, CloneTableColumnDefinition),
                CloneToken(definition._rightParen));
        }

        private static TableColumnDefinition CloneTableColumnDefinition(TableColumnDefinition column)
        {
            TableColumnDefinition clone = new TableColumnDefinition(CloneToken(column._columnNameToken), CloneDataType(column.DataType));
            clone._identityToken = CloneToken(column._identityToken);
            clone._identityLeftParen = CloneToken(column._identityLeftParen);
            clone._identitySeed = CloneToken(column._identitySeed);
            clone._identityComma = CloneToken(column._identityComma);
            clone._identityIncrement = CloneToken(column._identityIncrement);
            clone._identityRightParen = CloneToken(column._identityRightParen);
            clone._notToken = CloneToken(column._notToken);
            clone._nullToken = CloneToken(column._nullToken);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitSetStmt(Stmt.Set stmt)
        {
            Stmt.Set clone = new Stmt.Set(CloneToken(stmt._variableToken), CloneExpr(stmt.Value));
            clone._setToken = CloneToken(stmt._setToken);
            clone._equalsToken = CloneToken(stmt._equalsToken);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitIfStmt(Stmt.If stmt)
        {
            Stmt.If clone = new Stmt.If(ClonePredicate(stmt.Condition), CloneStmt(stmt.ThenBranch), CloneStmt(stmt.ElseBranch));
            clone._ifToken = CloneToken(stmt._ifToken);
            clone._elseToken = CloneToken(stmt._elseToken);
            return clone;
        }

        Stmt Stmt.Visitor<Stmt>.VisitBlockStmt(Stmt.Block stmt)
        {
            List<Stmt> statements = new List<Stmt>(stmt.Statements.Count);
            foreach (Stmt statement in stmt.Statements)
            {
                statements.Add(CloneStmt(statement));
            }

            List<Token> semicolons = new List<Token>(stmt._semicolons.Count);
            foreach (Token semicolon in stmt._semicolons)
            {
                semicolons.Add(CloneToken(semicolon));
            }

            Stmt.Block clone = new Stmt.Block(statements, semicolons);
            clone._beginToken = CloneToken(stmt._beginToken);
            clone._endToken = CloneToken(stmt._endToken);
            return clone;
        }

        #endregion
    }
}
