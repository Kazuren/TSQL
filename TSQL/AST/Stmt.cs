using System.Collections.Generic;
using static TSQL.Expr;

namespace TSQL
{
    #region Statement Base and Select
    public abstract class Stmt : SyntaxElement
    {
        /// <summary>Parses a SQL statement from the given string.</summary>
        /// <param name="sql">The SQL text to parse.</param>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Stmt Parse(string sql)
        {
            Stmt result = Parser.CreateParser(sql).Parse();
            BuildTokenChain(result);
            return result;
        }

        /// <summary>Parses a SQL SELECT statement from the given string.</summary>
        /// <param name="sql">The SQL text to parse. Must be a SELECT statement.</param>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Select ParseSelect(string sql)
        {
            Select result = Parser.CreateParser(sql).ParseSelect();
            BuildTokenChain(result);
            return result;
        }

        /// <summary>Parses a SQL INSERT statement from the given string.</summary>
        /// <param name="sql">The SQL text to parse. Must be an INSERT statement.</param>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Insert ParseInsert(string sql)
        {
            Insert result = Parser.CreateParser(sql).ParseInsert();
            BuildTokenChain(result);
            return result;
        }

        /// <summary>Parses a SQL DROP statement from the given string.</summary>
        /// <param name="sql">The SQL text to parse. Must be a DROP statement.</param>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Drop ParseDrop(string sql)
        {
            Drop result = Parser.CreateParser(sql).ParseDrop();
            BuildTokenChain(result);
            return result;
        }

        /// <summary>Parses a SQL EXECUTE/EXEC statement from the given string.</summary>
        /// <param name="sql">The SQL text to parse. Must be an EXECUTE statement.</param>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Stmt ParseExecute(string sql)
        {
            Stmt result = Parser.CreateParser(sql).ParseExecute();
            BuildTokenChain(result);
            return result;
        }

        public abstract T Accept<T>(Visitor<T> visitor);
        public interface Visitor<T>
        {
            T VisitSelectStmt(Stmt.Select stmt);
            T VisitInsertStmt(Stmt.Insert stmt);
            T VisitDropStmt(Stmt.Drop stmt);
            T VisitExecuteStmt(Stmt.Execute stmt);
            T VisitExecuteStringStmt(Stmt.ExecuteString stmt);
            T VisitDeclareStmt(Stmt.Declare stmt);
            T VisitSetStmt(Stmt.Set stmt);
            T VisitIfStmt(Stmt.If stmt);
            T VisitBlockStmt(Stmt.Block stmt);
        }

        public class Select : Stmt
        {
            private Cte _cteStmt;
            public Cte CteStmt
            {
                get => _cteStmt;
                set
                {
                    _cteStmt = value;

                    // When a CTE is attached programmatically, the query's SELECT keyword
                    // needs a leading space to separate from the CTE's closing paren.
                    // Without this: "WITH cte AS (SELECT 1)SELECT * FROM cte"
                    // With this:    "WITH cte AS (SELECT 1) SELECT * FROM cte"
                    // Parsed tokens already carry whitespace from source, so the
                    // LeadingTrivia check avoids double-adding in the parser path.
                    if (value != null && _query != null)
                    {
                        Token queryFirst = FirstTokenOf(_query);
                        if (queryFirst != null && queryFirst.LeadingTrivia.Count == 0)
                        {
                            queryFirst.AddLeadingTrivia(Whitespace.Space);
                        }
                    }
                }
            }
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

        public class Insert : Stmt
        {
            public Cte CteStmt { get; set; }
            public Expr Target { get; }
            public InsertColumnList ColumnList { get; set; }
            public InsertSource Source { get; }

            internal Token _insertToken;
            internal Token _intoToken;

            public Insert(Expr target, InsertSource source)
            {
                Target = target;
                Source = source;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitInsertStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                if (CteStmt != null)
                {
                    foreach (Token token in CteStmt.DescendantTokens())
                        yield return token;
                }

                yield return _insertToken;

                if (_intoToken != null)
                {
                    yield return _intoToken;
                }

                foreach (Token token in Target.DescendantTokens())
                {
                    yield return token;
                }

                if (ColumnList != null)
                {
                    foreach (Token token in ColumnList.DescendantTokens())
                        yield return token;
                }

                foreach (Token token in Source.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        public class Drop : Stmt
        {
            public ObjectType ObjectType { get; }
            public bool IfExists { get; }
            public SyntaxElementList<Expr.ObjectIdentifier> Targets { get; }

            internal Token _dropToken;
            internal Token _objectTypeToken;
            internal Token _ifToken;
            internal Token _existsToken;

            public Drop(ObjectType objectType, bool ifExists, SyntaxElementList<Expr.ObjectIdentifier> targets)
            {
                ObjectType = objectType;
                IfExists = ifExists;
                Targets = targets;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitDropStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _dropToken;
                yield return _objectTypeToken;

                if (_ifToken != null)
                {
                    yield return _ifToken;
                    yield return _existsToken;
                }

                foreach (Token token in Targets.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        public class Execute : Stmt
        {
            public Token ReturnVariable { get; }
            public Expr Target { get; }
            public SyntaxElementList<ExecuteArgument> Arguments { get; }
            public ExecuteWithClause WithClause { get; set; }

            internal Token _execToken;
            internal Token _returnEqualsToken;

            public Execute(Expr target, SyntaxElementList<ExecuteArgument> arguments)
            {
                Target = target;
                Arguments = arguments;
            }

            public Execute(Token returnVariable, Token returnEquals, Expr target, SyntaxElementList<ExecuteArgument> arguments)
            {
                ReturnVariable = returnVariable;
                _returnEqualsToken = returnEquals;
                Target = target;
                Arguments = arguments;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitExecuteStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _execToken;

                if (ReturnVariable != null)
                {
                    yield return ReturnVariable;
                    yield return _returnEqualsToken;
                }

                foreach (Token token in Target.DescendantTokens())
                {
                    yield return token;
                }

                if (Arguments.Count > 0)
                {
                    foreach (Token token in Arguments.DescendantTokens())
                    {
                        yield return token;
                    }
                }

                if (WithClause != null)
                {
                    foreach (Token token in WithClause.DescendantTokens())
                    {
                        yield return token;
                    }
                }
            }
        }

        public class ExecuteString : Stmt
        {
            public SyntaxElementList<Expr> Expressions { get; }
            public ExecuteContext Context { get; set; }
            public ExecuteAtClause AtClause { get; set; }

            internal Token _execToken;
            internal Token _leftParen;
            internal Token _rightParen;

            public ExecuteString(SyntaxElementList<Expr> expressions)
            {
                Expressions = expressions;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitExecuteStringStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _execToken;
                yield return _leftParen;

                foreach (Token token in Expressions.DescendantTokens())
                {
                    yield return token;
                }

                yield return _rightParen;

                if (Context != null)
                {
                    foreach (Token token in Context.DescendantTokens())
                    {
                        yield return token;
                    }
                }

                if (AtClause != null)
                {
                    foreach (Token token in AtClause.DescendantTokens())
                    {
                        yield return token;
                    }
                }
            }
        }

        public class Declare : Stmt
        {
            public SyntaxElementList<VariableDeclaration> Declarations { get; }

            internal Token _declareToken;

            public Declare(SyntaxElementList<VariableDeclaration> declarations)
            {
                Declarations = declarations;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitDeclareStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _declareToken;

                foreach (Token token in Declarations.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        public class Set : Stmt
        {
            public Token Variable { get; }
            public Expr Value { get; }

            internal Token _setToken;
            internal Token _equalsToken;

            public Set(Token variable, Expr value)
            {
                Variable = variable;
                Value = value;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitSetStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _setToken;
                yield return Variable;
                yield return _equalsToken;

                foreach (Token token in Value.DescendantTokens())
                {
                    yield return token;
                }
            }
        }

        public class If : Stmt
        {
            public AST.Predicate Condition { get; }
            public Stmt ThenBranch { get; }
            public Stmt ElseBranch { get; }

            internal Token _ifToken;
            internal Token _elseToken;

            public If(AST.Predicate condition, Stmt thenBranch, Stmt elseBranch)
            {
                Condition = condition;
                ThenBranch = thenBranch;
                ElseBranch = elseBranch;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitIfStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _ifToken;

                foreach (Token token in Condition.DescendantTokens())
                {
                    yield return token;
                }

                foreach (Token token in ThenBranch.DescendantTokens())
                {
                    yield return token;
                }

                if (_elseToken != null)
                {
                    yield return _elseToken;

                    foreach (Token token in ElseBranch.DescendantTokens())
                    {
                        yield return token;
                    }
                }
            }
        }

        public class Block : Stmt
        {
            public IReadOnlyList<Stmt> Statements { get; }
            internal readonly List<Token> _semicolons;

            internal Token _beginToken;
            internal Token _endToken;

            public Block(IReadOnlyList<Stmt> statements)
            {
                Statements = statements;
                _semicolons = new List<Token>();
                for (int i = 0; i < statements.Count; i++)
                {
                    if (i < statements.Count - 1)
                    {
                        _semicolons.Add(new ConcreteToken(TokenType.SEMICOLON, ";", null));
                    }
                    else
                    {
                        _semicolons.Add(null);
                    }
                }
                _beginToken = new ConcreteToken(TokenType.BEGIN, "BEGIN", null);
                _endToken = ConcreteToken.WithLeadingSpace(TokenType.END, "END");
            }

            internal Block(IReadOnlyList<Stmt> statements, List<Token> semicolons)
            {
                Statements = statements;
                _semicolons = semicolons;
            }

            public override T Accept<T>(Visitor<T> visitor)
            {
                return visitor.VisitBlockStmt(this);
            }

            public override IEnumerable<Token> DescendantTokens()
            {
                yield return _beginToken;

                for (int i = 0; i < Statements.Count; i++)
                {
                    foreach (Token token in Statements[i].DescendantTokens())
                    {
                        yield return token;
                    }

                    if (_semicolons[i] != null)
                    {
                        yield return _semicolons[i];
                    }
                }

                yield return _endToken;
            }
        }
    }
    #endregion

    #region DECLARE Supporting Types

    public class VariableDeclaration : SyntaxElement
    {
        public Token Variable { get; }

        internal VariableDeclaration(Token variable)
        {
            Variable = variable;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Variable;
        }
    }

    public class ScalarVariableDeclaration : VariableDeclaration
    {
        public DataType DataType { get; }
        public Expr Initializer { get; }

        internal Token _equalsToken;

        internal ScalarVariableDeclaration(Token variable, DataType dataType)
            : base(variable)
        {
            DataType = dataType;
        }

        internal ScalarVariableDeclaration(Token variable, DataType dataType, Token equalsToken, Expr initializer)
            : base(variable)
        {
            DataType = dataType;
            _equalsToken = equalsToken;
            Initializer = initializer;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Variable;

            foreach (Token token in DataType.DescendantTokens())
            {
                yield return token;
            }

            if (_equalsToken != null)
            {
                yield return _equalsToken;

                foreach (Token token in Initializer.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }

    public class TableVariableDeclaration : VariableDeclaration
    {
        public TableDefinition TableDefinition { get; }

        internal TableVariableDeclaration(Token variable, TableDefinition tableDefinition)
            : base(variable)
        {
            TableDefinition = tableDefinition;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return Variable;

            foreach (Token token in TableDefinition.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class TableColumnDefinition : SyntaxElement
    {
        internal Token _columnNameToken;
        public DataType DataType { get; }
        internal Token _identityToken;
        internal Token _identityLeftParen;
        internal Token _identitySeed;
        internal Token _identityComma;
        internal Token _identityIncrement;
        internal Token _identityRightParen;
        internal Token _notToken;
        internal Token _nullToken;

        internal TableColumnDefinition(Token columnName, DataType dataType)
        {
            _columnNameToken = columnName;
            DataType = dataType;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _columnNameToken;

            foreach (Token token in DataType.DescendantTokens())
            {
                yield return token;
            }

            if (_identityToken != null)
            {
                yield return _identityToken;

                if (_identityLeftParen != null)
                {
                    yield return _identityLeftParen;
                    yield return _identitySeed;
                    yield return _identityComma;
                    yield return _identityIncrement;
                    yield return _identityRightParen;
                }
            }

            if (_notToken != null)
            {
                yield return _notToken;
            }

            if (_nullToken != null)
            {
                yield return _nullToken;
            }
        }
    }

    public class TableDefinition : SyntaxElement
    {
        internal Token _tableToken;
        public SyntaxElementList<TableColumnDefinition> Columns { get; }
        internal Token _leftParen;
        internal Token _rightParen;

        internal TableDefinition(Token tableToken, Token leftParen, SyntaxElementList<TableColumnDefinition> columns, Token rightParen)
        {
            _tableToken = tableToken;
            _leftParen = leftParen;
            Columns = columns;
            _rightParen = rightParen;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _tableToken;
            yield return _leftParen;

            foreach (Token token in Columns.DescendantTokens())
            {
                yield return token;
            }

            yield return _rightParen;
        }
    }

    #endregion

    #region DROP Object Types

    public enum ObjectType { Table }

    #endregion

    #region EXECUTE Supporting Types

    public abstract class ExecuteArgument : SyntaxElement
    {
        public Token ParameterName { get; }
        internal Token _equalsToken;

        protected ExecuteArgument() { }

        protected ExecuteArgument(Token parameterName, Token equalsToken)
        {
            ParameterName = parameterName;
            _equalsToken = equalsToken;
        }

        protected IEnumerable<Token> ParameterTokens()
        {
            if (ParameterName != null)
            {
                yield return ParameterName;
                yield return _equalsToken;
            }
        }
    }

    public class ValueArgument : ExecuteArgument
    {
        public Expr Value { get; }

        public ValueArgument(Expr value)
        {
            Value = value;
        }

        public ValueArgument(Token parameterName, Token equalsToken, Expr value)
            : base(parameterName, equalsToken)
        {
            Value = value;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in ParameterTokens())
            {
                yield return token;
            }

            foreach (Token token in Value.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class DefaultArgument : ExecuteArgument
    {
        internal Token _defaultToken;

        public DefaultArgument(Token defaultToken)
        {
            _defaultToken = defaultToken;
        }

        public DefaultArgument(Token parameterName, Token equalsToken, Token defaultToken)
            : base(parameterName, equalsToken)
        {
            _defaultToken = defaultToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in ParameterTokens())
            {
                yield return token;
            }

            yield return _defaultToken;
        }
    }

    public class OutputArgument : ExecuteArgument
    {
        public Expr Value { get; }
        internal Token _outputToken;

        public OutputArgument(Expr value, Token outputToken)
        {
            Value = value;
            _outputToken = outputToken;
        }

        public OutputArgument(Token parameterName, Token equalsToken, Expr value, Token outputToken)
            : base(parameterName, equalsToken)
        {
            Value = value;
            _outputToken = outputToken;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in ParameterTokens())
            {
                yield return token;
            }

            foreach (Token token in Value.DescendantTokens())
            {
                yield return token;
            }

            yield return _outputToken;
        }
    }

    public enum ExecuteContextType { Login, User }

    public class ExecuteContext : SyntaxElement
    {
        public ExecuteContextType ContextType { get; }

        internal Token _asToken;
        internal Token _contextTypeToken;
        internal Token _equalsToken;
        internal Token _nameToken;

        public ExecuteContext(ExecuteContextType contextType)
        {
            ContextType = contextType;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _asToken;
            yield return _contextTypeToken;
            yield return _equalsToken;
            yield return _nameToken;
        }
    }

    public enum ExecuteAtTarget { LinkedServer, DataSource }

    public class ExecuteAtClause : SyntaxElement
    {
        public ExecuteAtTarget Target { get; }
        public Token ServerOrSourceName { get; }

        internal Token _atToken;
        internal Token _dataSourceToken;

        public ExecuteAtClause(Token serverOrSourceName, ExecuteAtTarget target)
        {
            ServerOrSourceName = serverOrSourceName;
            Target = target;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _atToken;

            if (_dataSourceToken != null)
            {
                yield return _dataSourceToken;
            }

            yield return ServerOrSourceName;
        }
    }

    public class ExecuteWithClause : SyntaxElement
    {
        public bool Recompile { get; }
        public ResultSetsSpec ResultSets { get; }

        internal Token _withToken;
        internal Token _recompileToken;
        internal Token _commaToken;

        public ExecuteWithClause(bool recompile, ResultSetsSpec resultSets)
        {
            Recompile = recompile;
            ResultSets = resultSets;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _withToken;

            if (_recompileToken != null)
            {
                yield return _recompileToken;
            }

            if (_commaToken != null)
            {
                yield return _commaToken;
            }

            if (ResultSets != null)
            {
                foreach (Token token in ResultSets.DescendantTokens())
                {
                    yield return token;
                }
            }
        }
    }

    public abstract class ResultSetsSpec : SyntaxElement { }

    public class ResultSetsUndefined : ResultSetsSpec
    {
        internal Token _resultToken;
        internal Token _setsToken;
        internal Token _undefinedToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _resultToken;
            yield return _setsToken;
            yield return _undefinedToken;
        }
    }

    public class ResultSetsNone : ResultSetsSpec
    {
        internal Token _resultToken;
        internal Token _setsToken;
        internal Token _noneToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _resultToken;
            yield return _setsToken;
            yield return _noneToken;
        }
    }

    public class ResultSetsDefined : ResultSetsSpec
    {
        public SyntaxElementList<ResultSetDefinition> Definitions { get; }

        internal Token _resultToken;
        internal Token _setsToken;
        internal Token _outerLeftParen;
        internal Token _outerRightParen;

        public ResultSetsDefined(SyntaxElementList<ResultSetDefinition> definitions)
        {
            Definitions = definitions;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _resultToken;
            yield return _setsToken;
            yield return _outerLeftParen;

            foreach (Token token in Definitions.DescendantTokens())
            {
                yield return token;
            }

            yield return _outerRightParen;
        }
    }

    public abstract class ResultSetDefinition : SyntaxElement { }

    public class ColumnResultSet : ResultSetDefinition
    {
        public SyntaxElementList<ResultSetColumn> Columns { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public ColumnResultSet(SyntaxElementList<ResultSetColumn> columns)
        {
            Columns = columns;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;

            foreach (Token token in Columns.DescendantTokens())
            {
                yield return token;
            }

            yield return _rightParen;
        }
    }

    public class ResultSetColumn : SyntaxElement
    {
        public DataType DataType { get; }

        internal Token _columnNameToken;
        internal Token _collateToken;
        internal Token _collationNameToken;
        internal Token _notToken;
        internal Token _nullToken;

        public ResultSetColumn(Token columnName, DataType dataType)
        {
            _columnNameToken = columnName;
            DataType = dataType;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _columnNameToken;

            foreach (Token token in DataType.DescendantTokens())
            {
                yield return token;
            }

            if (_collateToken != null)
            {
                yield return _collateToken;
                yield return _collationNameToken;
            }

            if (_notToken != null)
            {
                yield return _notToken;
            }

            if (_nullToken != null)
            {
                yield return _nullToken;
            }
        }
    }

    public class ObjectResultSet : ResultSetDefinition
    {
        public Expr.ObjectIdentifier ObjectName { get; }

        internal Token _asToken;
        internal Token _objectToken;

        public ObjectResultSet(Expr.ObjectIdentifier objectName)
        {
            ObjectName = objectName;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _asToken;
            yield return _objectToken;

            foreach (Token token in ObjectName.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class TypeResultSet : ResultSetDefinition
    {
        public Expr.ObjectIdentifier TypeName { get; }

        internal Token _asToken;
        internal Token _typeToken;

        public TypeResultSet(Expr.ObjectIdentifier typeName)
        {
            TypeName = typeName;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _asToken;
            yield return _typeToken;

            foreach (Token token in TypeName.DescendantTokens())
            {
                yield return token;
            }
        }
    }

    public class XmlResultSet : ResultSetDefinition
    {
        internal Token _asToken;
        internal Token _forToken;
        internal Token _xmlToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _asToken;
            yield return _forToken;
            yield return _xmlToken;
        }
    }

    #endregion

    #region INSERT Source Types

    public abstract class InsertSource : SyntaxElement { }

    public class SelectSource : InsertSource
    {
        public QueryExpression Query { get; }
        public OptionClause Option { get; set; }

        public SelectSource(QueryExpression query)
        {
            Query = query;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            foreach (Token token in Query.DescendantTokens())
                yield return token;

            if (Option != null)
            {
                foreach (Token token in Option.DescendantTokens())
                    yield return token;
            }
        }
    }

    public class ValuesSource : InsertSource
    {
        public SyntaxElementList<ValuesRow> Rows { get; }

        internal Token _valuesToken;

        public ValuesSource(SyntaxElementList<ValuesRow> rows)
        {
            Rows = rows;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _valuesToken;
            foreach (Token token in Rows.DescendantTokens())
                yield return token;
        }
    }

    public class DefaultValuesSource : InsertSource
    {
        internal Token _defaultToken;
        internal Token _valuesToken;

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _defaultToken;
            yield return _valuesToken;
        }
    }

    public class ExecSource : InsertSource
    {
        public Expr.ObjectIdentifier ProcedureName { get; }
        public SyntaxElementList<Expr> Arguments { get; }

        internal Token _execToken;

        public ExecSource(Expr.ObjectIdentifier procedureName, SyntaxElementList<Expr> arguments)
        {
            ProcedureName = procedureName;
            Arguments = arguments;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _execToken;
            foreach (Token token in ProcedureName.DescendantTokens())
                yield return token;

            if (Arguments.Count > 0)
            {
                foreach (Token token in Arguments.DescendantTokens())
                    yield return token;
            }
        }
    }

    public class InsertColumnList : SyntaxElement
    {
        public SyntaxElementList<ColumnName> Columns { get; }

        internal Token _leftParen;
        internal Token _rightParen;

        public InsertColumnList(SyntaxElementList<ColumnName> columns)
        {
            Columns = columns;
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            yield return _leftParen;

            foreach (Token token in Columns.DescendantTokens())
            {
                yield return token;
            }

            yield return _rightParen;
        }
    }

    #endregion

    #region Script

    public class Script : SyntaxElement
    {
        public IReadOnlyList<Stmt> Statements { get; }
        internal readonly List<Token> _semicolons;

        /// <summary>Parses a SQL script containing one or more statements.</summary>
        /// <param name="sql">The SQL text to parse. May contain multiple semicolon-separated statements.</param>
        /// <exception cref="ParseError">Thrown when the SQL is not valid.</exception>
        public static Script Parse(string sql)
        {
            Script result = Parser.CreateParser(sql).ParseScript();
            BuildTokenChain(result);
            return result;
        }

        internal Script(IReadOnlyList<Stmt> statements, List<Token> semicolons)
        {
            Statements = statements;
            _semicolons = semicolons;
        }

        public Script(IReadOnlyList<Stmt> statements)
        {
            Statements = statements;
            _semicolons = new List<Token>();
            for (int i = 0; i < statements.Count; i++)
            {
                if (i < statements.Count - 1)
                {
                    _semicolons.Add(new ConcreteToken(TokenType.SEMICOLON, ";", null));
                }
                else
                {
                    _semicolons.Add(null);
                }

                // Add newline before each statement after the first
                if (i > 0)
                {
                    Token first = FirstTokenOf(statements[i]);
                    if (first != null)
                    {
                        first.ClearLeadingTrivia();
                        first.AddLeadingTrivia(new Whitespace("\n"));
                    }
                }
            }
        }

        public override IEnumerable<Token> DescendantTokens()
        {
            for (int i = 0; i < Statements.Count; i++)
            {
                foreach (Token token in Statements[i].DescendantTokens())
                {
                    yield return token;
                }

                if (_semicolons[i] != null)
                {
                    yield return _semicolons[i];
                }
            }
        }
    }

    #endregion

    #region Common Table Expressions
    public class Cte : SyntaxElement
    {
        public SyntaxElementList<CteDefinition> Ctes { get; set; } = new SyntaxElementList<CteDefinition>();

        internal Token _withToken;

        public Cte()
        {
            _withToken = new ConcreteToken(TokenType.WITH, "WITH", null);
        }

        internal Cte(Token withToken)
        {
            _withToken = withToken;
        }

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
            get => _nameToken.IdentifierName;
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
    #endregion

    #region SELECT Column Types
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
        string Lexeme { get; }
    }

    public class SuffixAlias : SyntaxElement, Alias
    {
        public string Name { get => _nameToken.IdentifierName; }
        public string Lexeme { get => _nameToken.Lexeme; }
        internal Token _nameToken;
        internal Token _asKeyword;

        public SuffixAlias(string name, bool useAs = true)
        {
            _nameToken = ConcreteToken.WithLeadingSpace(TokenType.IDENTIFIER, name);
            if (useAs)
            {
                _asKeyword = ConcreteToken.WithLeadingSpace(TokenType.AS, "AS");
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
        public string Name { get => _nameToken.IdentifierName; }
        public string Lexeme { get => _nameToken.Lexeme; }
        internal Token _nameToken;
        internal Token _equalsToken;

        public PrefixAlias(string name)
        {
            _nameToken = new ConcreteToken(TokenType.IDENTIFIER, name, null);
            _equalsToken = ConcreteToken.WithLeadingSpace(TokenType.EQUAL, "=");
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
    #endregion

    #region TOP Clause

    [System.Flags]
    public enum TopModifier { None = 0, Percent = 1, WithTies = 2 }

    public class TopClause : SyntaxElement
    {
        private Expr _expression;
        public Expr Expression
        {
            get => _expression;
            set => SetWithTrivia(ref _expression, value);
        }
        public TopModifier Modifiers { get; set; }

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
    #endregion
}
