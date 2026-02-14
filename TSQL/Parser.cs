using System;
using System.Collections.Generic;
using System.Linq;
using TSQL.AST;
using static TSQL.Expr;

namespace TSQL
{
    // UNSUPPORTED:
    // ## @variable.function_call
    //  - reason: @variable must be a CLR user-defined type, something that's rare and not worth implementing unless needed

    // dollar sign ($) columns need no unique handling, column identifier already handles them just fine
    /*
    Legend:
    ? -> the group before it can appear zero or one time but not more.
    * -> the group before it can appear zero or more times.
    + -> the group before it can appear one or more times.
    | -> one of the following (OR) e.g. a | b = a OR b
    () -> grouping

    Grammar:
        Statements:
            Statement -> ("WITH" cte_list)? select_expression
            cte_list -> cte_definition ("," cte_definition)*
            cte_definition -> IDENTIFIER ("(" IDENTIFIER ("," IDENTIFIER)* ")")? "AS" "(" select_expression ")"
           
        Expressions:
            expression -> term
            term -> factor ( ("-" | "+" | "&" | "^" | "|") factor )*
            factor -> unary ( ( "/" | "*" | "%") unary )*
            unary -> ("-" | "~") postfix | postfix
            postfix -> scalar_subquery ("COLLATE" IDENTIFIER)?
            scalar_subquery -> ( "(" select_expression ")" ) | primary
            primary ->
                "NULL" | WHOLE_NUMBER | DECIMAL | STRING | VARIABLE
                | case_expression | iif_expression | cast_expression | convert_expression
                | column_expression | ( "(" expression ")" )
                | scalar_function | window_function
            case_expression -> "CASE" (expression WHEN_clause_simple+ | WHEN_clause_searched+) ("ELSE" expression)? "END"
            iif_expression -> "IIF" "(" search_condition "," expression "," expression ")"
            cast_expression -> ("CAST" | "TRY_CAST") "(" expression "AS" data_type ")"
            convert_expression -> ("CONVERT" | "TRY_CONVERT") "(" data_type "," expression ("," expression)? ")"
            data_type -> IDENTIFIER ("(" expression ("," expression)* ")")?
        

        Syntax nodes:
            column_expression -> fully_qualified_identifier
            fully_qualified_identifier -> (IDENTIFIER ".")? (IDENTIFIER ".")? (IDENTIFIER ".")? IDENTIFIER

            scalar_function -> function_call
            function_call -> fully_qualified_identifier "(" (expression_list)? ")" 
            expression_list -> expression ("," expression)*

            select_expression -> "SELECT" ("DISTINCT")? ("TOP" (WHOLE_NUMBER | "(" expression ")") ("PERCENT")? ("WITH TIES")? )? select_list (from_clause)? (where_clause)? (group_by_clause)? (having_clause)? (order_by_clause)? (option_clause)?
            option_clause -> "OPTION" "(" query_hint ("," query_hint)* ")"
            parenthesized_expression -> ( "(" select_expression | expression ")" ) 
            wildcard -> STAR
            qualified_wildcard -> (IDENTIFIER ".")? (IDENTIFIER ".")? (IDENTIFIER ".") STAR
            select_item -> wildcard | qualified_wildcard | expression (("AS")? IDENTIFIER)?
            select_list -> select_item ("," select_item)*

            where_clause -> "WHERE search_condition
            group_by_clause -> "GROUP" "BY" group_by_item ("," group_by_item)*
            group_by_item -> expression | rollup | cube | grouping_sets | "()" | "(" expression ("," expression)+ ")"
            rollup -> "ROLLUP" "(" group_by_expression ("," group_by_expression)* ")"
            cube -> "CUBE" "(" group_by_expression ("," group_by_expression)* ")"
            grouping_sets -> "GROUPING" "SETS" "(" grouping_set ("," grouping_set)* ")"
            group_by_expression -> expression | "(" expression ("," expression)+ ")"
            grouping_set -> "()" | group_by_item
            having_clause -> "HAVING" search_condition
            order_by_clause -> "ORDER" "BY" order_by_item ("," order_by_item)*
            order_by_item -> expression ("ASC" | "DESC")?
            comparison_operator = ("=" | "!=" | "<>" | ">" | ">=" | "<" | "<=" | "!>" | "!<" )

            ---------------- WHERE ---------------
            comparison_predicate -> expression comparison_operator expression 
            like_predicate -> expression ("NOT")? "LIKE" expression ("ESCAPE" STRING)?
            between_predicate -> expression ("NOT")? "BETWEEN" expression "AND" expression
            null_predicate -> expression IS ("NOT")? "NULL"
            full_text_columns -> "*" | column_identifier | "(" column_identifier ("," column_identifier)* ")"
            contains_predicate -> "CONTAINS" "(" full_text_columns "," expression ("," "LANGUAGE" expression)? ")"
            freetext_predicate -> "FREETEXT" "(" full_text_columns "," expression ("," "LANGUAGE" expression)? ")"
            in_predicate -> expression ("NOT")? IN "(" ( select_expression | ( expression ("," expression)* ) ) ")"
            quantifier_predicate -> expression comparison_operator ("ALL" | "SOME" | "ANY" ) "(" select_expression ")"
            exists_predicate -> "EXISTS" "(" select_expression ")"


            search_condition -> predicate
            
            predicate -> or_predicate
            or_predicate ->  and_predicate ("OR" and_predicate)*
            and_predicate -> unary_predicate ("AND" unary_predicate)*
            unary_predicate -> ("NOT")? unary_predicate | primary_predicate
            primary_predicate ->
                comparison_predicate | like_comparison | between_predicate |
                null_predicate | contains_predicate | freetext_predicate | in_predicate |
                quantifier_predicate | exists_predicate | "(" predicate ")"
            ---------------- WHERE ---------------
    
            ---------------- FROM ---------------
            from_clause -> "FROM" table_source_list
            table_source_list -> table_source_item ("," table_source_item)*
            table_source_item -> table_source_primary (join_part)*

            table_source_primary ->
                named_table_source
                | subquery_table_source
                | variable_table_source
                | values_table_source
                | rowset_function_source
                | "(" table_source_item ")"

            named_table_source -> fully_qualified_identifier (for_system_time)? (("AS")? IDENTIFIER)? (tablesample_clause)? (with_hints)?
            subquery_table_source -> "(" select_expression ")" (("AS")? IDENTIFIER)? ( "(" IDENTIFIER ("," IDENTIFIER)* ")" )?
            variable_table_source -> VARIABLE (("AS")? IDENTIFIER)?
            values_table_source -> "(" "VALUES" values_row ("," values_row)* ")" (("AS")? IDENTIFIER)? ( "(" IDENTIFIER ("," IDENTIFIER)* ")" )?
            values_row -> "(" expression ("," expression)* ")"
            rowset_function_source -> ("OPENROWSET" | "OPENQUERY" | "OPENDATASOURCE") "(" expression_list ")" (("AS")? IDENTIFIER)?

            join_part -> qualified_join | cross_join | apply_join | pivot_clause | unpivot_clause
            qualified_join -> ( ("INNER" | "LEFT" ("OUTER")? | "RIGHT" ("OUTER")? | "FULL" ("OUTER")?) (join_hint)? )? "JOIN" table_source_primary "ON" search_condition
            cross_join -> "CROSS" "JOIN" table_source_primary
            apply_join -> ("CROSS" | "OUTER") "APPLY" table_source_primary
            join_hint -> "LOOP" | "HASH" | "MERGE" | "REMOTE"

            pivot_clause -> "PIVOT" "(" function_call "FOR" fully_qualified_identifier "IN" "(" expression_list ")" ")" (("AS")? IDENTIFIER)?
            unpivot_clause -> "UNPIVOT" "(" fully_qualified_identifier "FOR" fully_qualified_identifier "IN" "(" IDENTIFIER ("," IDENTIFIER)* ")" ")" (("AS")? IDENTIFIER)?

            for_system_time -> "FOR" "SYSTEM_TIME" system_time
            system_time ->
                "AS" "OF" date_time
                | "FROM" date_time "TO" date_time
                | "BETWEEN" date_time "AND" date_time
                | "CONTAINED" "IN" "(" date_time "," date_time ")"
                | "ALL"
            date_time -> STRING | VARIABLE

            tablesample_clause -> "TABLESAMPLE" ("SYSTEM")? "(" expression ("PERCENT" | "ROWS") ")" ( "REPEATABLE" "(" expression ")" )?

            with_hints -> "WITH" "(" table_hint ("," table_hint)* ")"
            table_hint -> "NOEXPAND"
                | "INDEX" "(" index_value ("," index_value)* ")"
                | "INDEX" "=" index_value
                | "FORCESEEK" ( "(" index_value "(" IDENTIFIER ("," IDENTIFIER)* ")" ")" )?
                | "FORCESCAN"
                | "HOLDLOCK"
                | "NOLOCK"
                | "NOWAIT"
                | "PAGLOCK"
                | "READCOMMITTED"
                | "READCOMMITTEDLOCK"
                | "READPAST"
                | "READUNCOMMITTED"
                | "REPEATABLEREAD"
                | "ROWLOCK"
                | "SERIALIZABLE"
                | "SNAPSHOT"
                | "SPATIAL_WINDOW_MAX_CELLS" "=" WHOLE_NUMBER
                | "TABLOCK"
                | "TABLOCKX"
                | "UPDLOCK"
                | "XLOCK"
            index_value -> WHOLE_NUMBER | IDENTIFIER
            ---------------- FROM ---------------

            ---------------- WINDOW FUNCTIONS ---------------
            window_function -> function_call over_clause
            over_clause -> "OVER" "(" (partition_by_clause)? (order_by_clause)? (frame_clause)? ")"
            partition_by_clause -> "PARTITION" "BY" expression ("," expression)*
            order_by_clause -> "ORDER" "BY" order_by_item ("," order_by_item)*
            order_by_item -> expression ("ASC" | "DESC")?
            frame_clause -> ("ROWS" | "RANGE") frame_extent
            frame_extent -> frame_bound | "BETWEEN" frame_bound "AND" frame_bound
            frame_bound -> "UNBOUNDED" "PRECEDING" 
                         | "UNBOUNDED" "FOLLOWING"
                         | "CURRENT" "ROW"
                         | WHOLE_NUMBER "PRECEDING"
                         | WHOLE_NUMBER "FOLLOWING"
            ---------------- WINDOW FUNCTIONS ---------------
    */

    public class Parser
    {
        public class ParseError : Exception
        {
            public ParseError(string message) : base(message)
            {

            }
        }

        private readonly List<Token> _tokens;
        private int _current = 0;

        /// <summary>
        /// Non-reserved keywords that can be used as identifiers.
        /// These are contextual keywords - they act as keywords only in specific contexts.
        /// </summary>
        private static readonly HashSet<TokenType> ContextualKeywords = new HashSet<TokenType>
        {
            TokenType.ROWS,
            TokenType.RANGE,
            TokenType.PARTITION,
            TokenType.UNBOUNDED,
            TokenType.PRECEDING,
            TokenType.FOLLOWING,
            TokenType.ROW,
            TokenType.ROW_NUMBER,
            TokenType.RANK,
            TokenType.DENSE_RANK,
            TokenType.NTILE,
            TokenType.APPLY,
            TokenType.LOOP,
            TokenType.HASH,
            TokenType.REMOTE,
            TokenType.SYSTEM,
            TokenType.CONTAINED,
            TokenType.REPEATABLE,
            TokenType.ROLLUP,
            TokenType.CUBE,
            TokenType.GROUPING,
            TokenType.SETS,
            TokenType.LANGUAGE,
            TokenType.IIF,
            TokenType.TIES
        };

        public Parser(IEnumerable<Token> tokens)
        {
            _tokens = tokens.ToList();
        }

        private void Reset()
        {
            _current = 0;
        }

        public Stmt Parse()
        {
            Reset();
            return ParseSelect();
        }

        public Stmt.Select ParseSelect()
        {
            Reset();
            Stmt.Select selectStmt = SelectStatement();

            if (!IsAtEnd())
            {
                throw Error(Peek(), "Expected valid token.");
            }

            return selectStmt;
        }

        /// <summary>
        /// (WITH cte_list)? select_expression
        /// </summary>
        private Stmt.Select SelectStatement()
        {
            Cte cte = null;
            if (Check(TokenType.WITH))
            {
                cte = ParseCte();
            }

            Stmt.Select selectStmt = new Stmt.Select(SelectExpression());
            selectStmt.CteStmt = cte;
            return selectStmt;
        }

        private Expr.Subquery Subquery()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
            SelectExpression subquery = SelectExpression();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            return new Expr.Subquery(subquery, leftParen, rightParen);
        }

        private SelectExpression SelectExpression()
        {
            SelectExpression selectExpr = new SelectExpression();
            selectExpr._selectKeyword = Consume(TokenType.SELECT, "Expected SELECT");

            if (Match(TokenType.DISTINCT))
            {
                selectExpr.Distinct = true;
                selectExpr._distinctKeyword = Previous();
            }

            if (Match(TokenType.TOP, out Token topKeyword))
            {
                selectExpr.Top = ParseTopClause(topKeyword);
            }

            selectExpr.Columns.Add(SelectItem());

            while (Match(TokenType.COMMA, out Token comma))
            {
                selectExpr.Columns.Add(SelectItem(), comma);
            }

            selectExpr.From = FromClause();

            if (Match(TokenType.WHERE, out Token whereToken))
            {
                selectExpr._whereKeyword = whereToken;
                selectExpr.Where = SearchCondition();
            }

            if (Check(TokenType.GROUP))
            {
                selectExpr.GroupBy = GroupByClause();
            }

            // HAVING search_condition
            if (Match(TokenType.HAVING, out Token havingToken))
            {
                selectExpr._havingKeyword = havingToken;
                selectExpr.Having = SearchCondition();
            }

            // ORDER BY expr [ASC|DESC], ...
            if (Match(TokenType.ORDER, out Token orderToken))
            {
                selectExpr._orderKeyword = orderToken;
                selectExpr._orderByKeyword = Consume(TokenType.BY, "Expected BY after ORDER");
                selectExpr.OrderBy = ParseOrderByList();
            }

            // OPTION (query_hint [, ...n])
            if (Check(TokenType.OPTION))
            {
                selectExpr.Option = ParseOptionClause();
            }

            return selectExpr;
        }


        /// <summary>
        /// TOP WHOLE_NUMBER | TOP (expression) — bare TOP only accepts integer literals.
        /// Rejects NULL, DECIMAL, and STRING literals.
        /// </summary>
        private TopClause ParseTopClause(Token topKeyword)
        {
            TopClause top;

            if (Check(TokenType.LEFT_PAREN))
            {
                // TOP (expression) — full expression inside parens
                Token leftParen = Advance();
                Expr expr = Expression();
                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after TOP expression");
                top = new TopClause(expr);
                top._leftParen = leftParen;
                top._rightParen = rightParen;
            }
            else if (Check(TokenType.WHOLE_NUMBER))
            {
                // TOP N — bare integer literal only
                Expr expr = new Expr.Literal(Advance());
                top = new TopClause(expr);
            }
            else
            {
                throw Error(Peek(), "Expected integer literal or parenthesized expression after TOP");
            }

            top._topKeyword = topKeyword;

            if (Match(TokenType.PERCENT, out Token percentToken))
            {
                top.Percent = true;
                top._percentKeyword = percentToken;
            }

            if (Check(TokenType.WITH) && CheckNext(TokenType.TIES))
            {
                top.WithTies = true;
                top._withKeyword = Advance();
                top._tiesKeyword = Advance();
            }

            return top;
        }

        private SelectItem SelectItem()
        {
            if (Match(TokenType.STAR, out Token wildcardToken))
            {
                return new Wildcard(wildcardToken);
            }

            // Check for alternate alias syntax: alias = expression
            // Contextual keywords can also be used as aliases
            Alias alias = null;
            if (IsIdentifierOrContextualKeyword() && CheckNext(TokenType.EQUAL))
            {
                Token aliasToken = Advance();
                Token equalToken = Advance();
                alias = new PrefixAlias(aliasToken, equalToken);
            }

            Expr expression = Expression();

            if (alias == null)
            {
                Token token = Previous();
                if (token.Type == TokenType.STAR)
                {
                    // if we haven't started this select item with a prefix alias, and the previous token is a star
                    // that this expression has been parsed as a column identifier
                    ColumnIdentifier columnIdentifier = (ColumnIdentifier)expression;

                    QualifiedWildcard qualifiedWildCard = new QualifiedWildcard(
                        columnIdentifier.DatabaseName,
                        columnIdentifier.SchemaName,
                        columnIdentifier.ObjectName,
                        token
                    );
                    qualifiedWildCard._databaseToSchemaDot = columnIdentifier._databaseToSchemaDot;
                    qualifiedWildCard._schemaToObjectDot = columnIdentifier._schemaToObjectDot;
                    qualifiedWildCard._objectToStarDot = columnIdentifier._objectToColumnDot;
                    return qualifiedWildCard;
                }
                else
                {
                    alias = Alias();
                }
            }

            return new SelectColumn(expression, alias);
        }

        private FromClause FromClause()
        {
            if (!Match(TokenType.FROM, out Token fromToken))
            {
                return null;
            }

            FromClause fromClause = new FromClause(fromToken);

            fromClause.TableSources.Add(ParseTableSourceItem());

            while (Match(TokenType.COMMA, out Token comma))
            {
                fromClause.TableSources.Add(ParseTableSourceItem(), comma);
            }

            return fromClause;
        }

        private TableSource ParseTableSourceItem()
        {
            TableSource source = ParseTableSourcePrimary();

            while (IsJoinOrPivotStart())
            {
                if (Check(TokenType.PIVOT))
                {
                    source = ParsePivotTableSource(source);
                }
                else if (Check(TokenType.UNPIVOT))
                {
                    source = ParseUnpivotTableSource(source);
                }
                else
                {
                    source = ParseJoinSuffix(source);
                }
            }

            return source;
        }

        private bool IsJoinOrPivotStart()
        {
            TokenType type = Peek().Type;

            if (type == TokenType.JOIN || type == TokenType.INNER)
                return true;

            if (type == TokenType.LEFT || type == TokenType.RIGHT || type == TokenType.FULL)
                return true;

            // CROSS can be CROSS JOIN or CROSS APPLY
            if (type == TokenType.CROSS)
            {
                Token next = PeekNext();
                return next != null && (next.Type == TokenType.JOIN || next.Type == TokenType.APPLY);
            }

            // OUTER APPLY
            if (type == TokenType.OUTER)
            {
                Token next = PeekNext();
                return next != null && next.Type == TokenType.APPLY;
            }

            // PIVOT / UNPIVOT are suffixes on a table source
            if (type == TokenType.PIVOT || type == TokenType.UNPIVOT)
                return true;

            // Join hints (LOOP, HASH, MERGE, REMOTE) are only valid AFTER a join type
            // keyword (INNER, LEFT, etc.), so they don't start a join by themselves.
            // When used standalone like "FROM T LOOP JOIN ...", LOOP is an alias.

            return false;
        }

        private TableSource ParseJoinSuffix(TableSource left)
        {
            // CROSS JOIN
            if (Check(TokenType.CROSS) && CheckNext(TokenType.JOIN))
            {
                return ParseCrossJoin(left);
            }

            // CROSS APPLY
            if (Check(TokenType.CROSS) && CheckNext(TokenType.APPLY))
            {
                return ParseApplyJoin(left, ApplyType.Cross);
            }

            // OUTER APPLY
            if (Check(TokenType.OUTER) && CheckNext(TokenType.APPLY))
            {
                return ParseApplyJoin(left, ApplyType.Outer);
            }

            // Qualified join: [join_hint] [INNER|LEFT|RIGHT|FULL] [OUTER] JOIN ... ON ...
            return ParseQualifiedJoin(left);
        }

        private QualifiedJoin ParseQualifiedJoin(TableSource left)
        {
            // Optional join type
            Token joinTypeToken = null;
            Token outerToken = null;
            JoinType joinType = JoinType.Inner; // default for bare JOIN

            if (Match(TokenType.INNER, out Token innerToken))
            {
                joinTypeToken = innerToken;
                joinType = JoinType.Inner;
            }
            else if (Match(TokenType.LEFT, out Token leftToken))
            {
                joinTypeToken = leftToken;
                joinType = JoinType.LeftOuter;
                Match(TokenType.OUTER, out outerToken);
            }
            else if (Match(TokenType.RIGHT, out Token rightToken))
            {
                joinTypeToken = rightToken;
                joinType = JoinType.RightOuter;
                Match(TokenType.OUTER, out outerToken);
            }
            else if (Match(TokenType.FULL, out Token fullToken))
            {
                joinTypeToken = fullToken;
                joinType = JoinType.FullOuter;
                Match(TokenType.OUTER, out outerToken);
            }

            // Optional join hint (between join type and JOIN keyword)
            Token joinHintToken = null;
            JoinHint? joinHint = null;
            if (Match(TokenType.LOOP, out Token loopToken)) { joinHintToken = loopToken; joinHint = JoinHint.Loop; }
            else if (Match(TokenType.HASH, out Token hashToken)) { joinHintToken = hashToken; joinHint = JoinHint.Hash; }
            else if (Match(TokenType.MERGE, out Token mergeToken)) { joinHintToken = mergeToken; joinHint = JoinHint.Merge; }
            else if (Match(TokenType.REMOTE, out Token remoteToken)) { joinHintToken = remoteToken; joinHint = JoinHint.Remote; }

            Token joinToken = Consume(TokenType.JOIN, "Expected JOIN");
            TableSource right = ParseTableSourcePrimary();
            Token onToken = Consume(TokenType.ON, "Expected ON");
            AST.Predicate onCondition = SearchCondition();

            QualifiedJoin qualifiedJoin = new QualifiedJoin(left, right, joinType, onCondition, joinHint);
            qualifiedJoin._joinHintToken = joinHintToken;
            qualifiedJoin._joinTypeToken = joinTypeToken;
            qualifiedJoin._outerToken = outerToken;
            qualifiedJoin._joinToken = joinToken;
            qualifiedJoin._onToken = onToken;

            return qualifiedJoin;
        }

        private CrossJoin ParseCrossJoin(TableSource left)
        {
            Token crossToken = Consume(TokenType.CROSS, "Expected CROSS");
            Token joinToken = Consume(TokenType.JOIN, "Expected JOIN");
            TableSource right = ParseTableSourcePrimary();

            CrossJoin crossJoin = new CrossJoin(left, right);
            crossJoin._crossToken = crossToken;
            crossJoin._joinToken = joinToken;

            return crossJoin;
        }

        private ApplyJoin ParseApplyJoin(TableSource left, ApplyType applyType)
        {
            Token applyTypeToken = Advance(); // CROSS or OUTER
            Token applyToken = Consume(TokenType.APPLY, "Expected APPLY");
            TableSource right = ParseTableSourcePrimary();

            ApplyJoin applyJoin = new ApplyJoin(left, right, applyType);
            applyJoin._applyTypeToken = applyTypeToken;
            applyJoin._applyToken = applyToken;

            return applyJoin;
        }

        private TableSource ParseTableSourcePrimary()
        {
            // Subquery: (SELECT ...)
            if (Check(TokenType.LEFT_PAREN) && CheckNext(TokenType.SELECT))
            {
                return ParseSubqueryTableSource();
            }

            // Values derived table: (VALUES (...), (...)) AS alias(cols)
            if (Check(TokenType.LEFT_PAREN) && CheckNext(TokenType.VALUES))
            {
                return ParseValuesTableSource();
            }

            // Parenthesized table source: (T1 JOIN T2 ON ...)
            if (Check(TokenType.LEFT_PAREN))
            {
                return ParseParenthesizedTableSource();
            }

            if (Check(TokenType.VARIABLE))
            {
                return ParseTableVariable();
            }

            // Rowset functions: OPENROWSET, OPENQUERY, OPENDATASOURCE
            if (Check(TokenType.OPENROWSET) || Check(TokenType.OPENQUERY) || Check(TokenType.OPENDATASOURCE))
            {
                return ParseRowsetFunction();
            }

            return ParseNamedTableSource();
        }

        private ParenthesizedTableSource ParseParenthesizedTableSource()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
            TableSource inner = ParseTableSourceItem();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            ParenthesizedTableSource parenSource = new ParenthesizedTableSource(inner);
            parenSource._leftParen = leftParen;
            parenSource._rightParen = rightParen;
            parenSource.Alias = Alias();

            return parenSource;
        }

        private TableReference ParseNamedTableSource()
        {
            IdentifierPartsBuffer parts = CollectIdentifierParts();
            Expr.ObjectIdentifier objectId = FunctionIdentifier(parts);

            TableReference tableRef = new TableReference(objectId);

            // FOR SYSTEM_TIME (before alias)
            if (Check(TokenType.FOR) && IsSystemTimeLookahead())
            {
                tableRef.ForSystemTime = ParseForSystemTimeClause();
            }

            tableRef.Alias = Alias();

            // TABLESAMPLE (after alias)
            if (Check(TokenType.TABLESAMPLE))
            {
                tableRef.Tablesample = ParseTablesampleClause();
            }

            // WITH (table hints) (after alias)
            if (Check(TokenType.WITH) && CheckNext(TokenType.LEFT_PAREN))
            {
                tableRef.TableHints = ParseTableHintClause();
            }

            return tableRef;
        }

        private bool IsSystemTimeLookahead()
        {
            Token next = PeekNext();
            return next != null && next.Type == TokenType.IDENTIFIER &&
                   next.Lexeme.Equals("SYSTEM_TIME", StringComparison.OrdinalIgnoreCase);
        }

        private ForSystemTimeClause ParseForSystemTimeClause()
        {
            Token forToken = Consume(TokenType.FOR, "Expected FOR");
            Token systemTimeToken = Consume(TokenType.IDENTIFIER, "Expected SYSTEM_TIME");

            if (Match(TokenType.AS, out Token asToken))
            {
                // AS OF date_time
                Token ofToken = Consume(TokenType.OF, "Expected OF");
                Expr startTime = Expression();

                ForSystemTimeClause clause = new ForSystemTimeClause(SystemTimeType.AsOf, startTime);
                clause._forToken = forToken;
                clause._systemTimeToken = systemTimeToken;
                clause._typeKeyword1 = asToken;
                clause._typeKeyword2 = ofToken;
                return clause;
            }
            else if (Match(TokenType.FROM, out Token fromKw))
            {
                // FROM date_time TO date_time
                Expr startTime = Expression();
                Token toToken = Consume(TokenType.TO, "Expected TO");
                Expr endTime = Expression();

                ForSystemTimeClause clause = new ForSystemTimeClause(SystemTimeType.FromTo, startTime, endTime);
                clause._forToken = forToken;
                clause._systemTimeToken = systemTimeToken;
                clause._typeKeyword1 = fromKw;
                clause._typeKeyword2 = toToken;
                return clause;
            }
            else if (Match(TokenType.BETWEEN, out Token betweenToken))
            {
                // BETWEEN date_time AND date_time
                Expr startTime = Expression();
                Token andToken = Consume(TokenType.AND, "Expected AND");
                Expr endTime = Expression();

                ForSystemTimeClause clause = new ForSystemTimeClause(SystemTimeType.BetweenAnd, startTime, endTime);
                clause._forToken = forToken;
                clause._systemTimeToken = systemTimeToken;
                clause._typeKeyword1 = betweenToken;
                clause._typeKeyword2 = andToken;
                return clause;
            }
            else if (Match(TokenType.CONTAINED, out Token containedToken))
            {
                // CONTAINED IN (date_time, date_time)
                Token inToken = Consume(TokenType.IN, "Expected IN");
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
                Expr startTime = Expression();
                Token comma = Consume(TokenType.COMMA, "Expected ,");
                Expr endTime = Expression();
                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

                ForSystemTimeClause clause = new ForSystemTimeClause(SystemTimeType.ContainedIn, startTime, endTime);
                clause._forToken = forToken;
                clause._systemTimeToken = systemTimeToken;
                clause._typeKeyword1 = containedToken;
                clause._typeKeyword2 = inToken;
                clause._leftParen = leftParen;
                clause._comma = comma;
                clause._rightParen = rightParen;
                return clause;
            }
            else if (Match(TokenType.ALL, out Token allToken))
            {
                // ALL
                ForSystemTimeClause clause = new ForSystemTimeClause(SystemTimeType.All);
                clause._forToken = forToken;
                clause._systemTimeToken = systemTimeToken;
                clause._typeKeyword1 = allToken;
                return clause;
            }
            else
            {
                throw Error(Peek(), "Expected AS OF, FROM...TO, BETWEEN...AND, CONTAINED IN, or ALL after FOR SYSTEM_TIME");
            }
        }

        private TablesampleClause ParseTablesampleClause()
        {
            Token tablesampleToken = Consume(TokenType.TABLESAMPLE, "Expected TABLESAMPLE");

            // Optional SYSTEM keyword
            Token systemToken = null;
            if (Check(TokenType.SYSTEM))
            {
                systemToken = Advance();
            }

            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
            Expr sampleSize = Expression();

            // PERCENT or ROWS
            TableSampleUnit unit;
            Token unitToken;
            if (Match(TokenType.PERCENT, out Token percentToken))
            {
                unit = TableSampleUnit.Percent;
                unitToken = percentToken;
            }
            else if (Match(TokenType.ROWS, out Token rowsToken))
            {
                unit = TableSampleUnit.Rows;
                unitToken = rowsToken;
            }
            else
            {
                throw Error(Peek(), "Expected PERCENT or ROWS");
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            // Optional REPEATABLE(seed)
            Expr repeatSeed = null;
            Token repeatableToken = null;
            Token repeatLeftParen = null;
            Token repeatRightParen = null;
            if (Match(TokenType.REPEATABLE, out repeatableToken))
            {
                repeatLeftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
                repeatSeed = Expression();
                repeatRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");
            }

            TablesampleClause clause = new TablesampleClause(sampleSize, unit, repeatSeed);
            clause._tablesampleToken = tablesampleToken;
            clause._systemToken = systemToken;
            clause._leftParen = leftParen;
            clause._unitToken = unitToken;
            clause._rightParen = rightParen;
            clause._repeatableToken = repeatableToken;
            clause._repeatLeftParen = repeatLeftParen;
            clause._repeatRightParen = repeatRightParen;
            return clause;
        }

        private static readonly Dictionary<string, TableHintType> SimpleTableHints =
            new Dictionary<string, TableHintType>(StringComparer.OrdinalIgnoreCase)
            {
                { "NOEXPAND", TableHintType.NoExpand },
                { "FORCESCAN", TableHintType.ForceScan },
                { "NOLOCK", TableHintType.NoLock },
                { "NOWAIT", TableHintType.NoWait },
                { "PAGLOCK", TableHintType.PageLock },
                { "READCOMMITTED", TableHintType.ReadCommitted },
                { "READCOMMITTEDLOCK", TableHintType.ReadCommittedLock },
                { "READPAST", TableHintType.ReadPast },
                { "READUNCOMMITTED", TableHintType.ReadUncommitted },
                { "REPEATABLEREAD", TableHintType.RepeatableRead },
                { "ROWLOCK", TableHintType.RowLock },
                { "SERIALIZABLE", TableHintType.Serializable },
                { "SNAPSHOT", TableHintType.Snapshot },
                { "TABLOCK", TableHintType.TabLock },
                { "TABLOCKX", TableHintType.TabLockX },
                { "UPDLOCK", TableHintType.UpdLock },
                { "XLOCK", TableHintType.XLock },
            };

        private TableHintClause ParseTableHintClause()
        {
            Token withToken = Consume(TokenType.WITH, "Expected WITH");
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            SyntaxElementList<TableHint> hints = new SyntaxElementList<TableHint>();
            hints.Add(ParseTableHint());

            while (Match(TokenType.COMMA, out Token comma))
            {
                hints.Add(ParseTableHint(), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            TableHintClause clause = new TableHintClause(hints);
            clause._withToken = withToken;
            clause._leftParen = leftParen;
            clause._rightParen = rightParen;
            return clause;
        }

        private TableHint ParseTableHint()
        {
            Token hintToken = Peek();

            // HOLDLOCK is a keyword
            if (Check(TokenType.HOLDLOCK))
            {
                Advance();
                TableHint hint = new TableHint(TableHintType.HoldLock);
                hint._hintToken = hintToken;
                return hint;
            }

            // INDEX hint
            if (Check(TokenType.INDEX))
            {
                Advance();
                TableHint hint;
                if (Match(TokenType.EQUAL, out Token equalsToken))
                {
                    // INDEX = value
                    SyntaxElementList<Expr> indexValues = new SyntaxElementList<Expr>();
                    indexValues.Add(Expression());
                    hint = new TableHint(TableHintType.Index, indexValues);
                    hint._equalsToken = equalsToken;
                }
                else
                {
                    // INDEX(value, ...)
                    Token indexLeftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
                    SyntaxElementList<Expr> indexValues = new SyntaxElementList<Expr>();
                    indexValues.Add(Expression());
                    while (Match(TokenType.COMMA, out Token comma))
                    {
                        indexValues.Add(Expression(), comma);
                    }
                    Token indexRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");
                    hint = new TableHint(TableHintType.Index, indexValues);
                    hint._leftParen = indexLeftParen;
                    hint._rightParen = indexRightParen;
                }
                hint._hintToken = hintToken;
                return hint;
            }

            // Identifier-based hints
            if (Check(TokenType.IDENTIFIER))
            {
                string lexeme = hintToken.Lexeme;

                // Simple keyword hints
                if (SimpleTableHints.TryGetValue(lexeme, out TableHintType hintType))
                {
                    Advance();
                    TableHint hint = new TableHint(hintType);
                    hint._hintToken = hintToken;
                    return hint;
                }

                // FORCESEEK with optional parameters
                if (lexeme.Equals("FORCESEEK", StringComparison.OrdinalIgnoreCase))
                {
                    Advance();
                    TableHint hint;
                    if (Check(TokenType.LEFT_PAREN))
                    {
                        Token fsLeftParen = Advance();
                        Expr indexValue = Expression();
                        Token innerLeftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
                        SyntaxElementList<ColumnName> columns = new SyntaxElementList<ColumnName>();
                        columns.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")));
                        while (Match(TokenType.COMMA, out Token comma))
                        {
                            columns.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")), comma);
                        }
                        Token innerRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");
                        Token fsRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

                        hint = new TableHint(TableHintType.ForceSeek, indexValue, columns);
                        hint._leftParen = fsLeftParen;
                        hint._innerLeftParen = innerLeftParen;
                        hint._innerRightParen = innerRightParen;
                        hint._rightParen = fsRightParen;
                    }
                    else
                    {
                        hint = new TableHint(TableHintType.ForceSeek);
                    }
                    hint._hintToken = hintToken;
                    return hint;
                }

                // SPATIAL_WINDOW_MAX_CELLS = N
                if (lexeme.Equals("SPATIAL_WINDOW_MAX_CELLS", StringComparison.OrdinalIgnoreCase))
                {
                    Advance();
                    Token equalsToken = Consume(TokenType.EQUAL, "Expected =");
                    Expr value = Expression();

                    TableHint hint = new TableHint(TableHintType.SpatialWindowMaxCells, value);
                    hint._hintToken = hintToken;
                    hint._equalsToken = equalsToken;
                    return hint;
                }
            }

            throw Error(Peek(), "Expected table hint");
        }

        private SubqueryReference ParseSubqueryTableSource()
        {
            Expr.Subquery subquery = Subquery();
            SubqueryReference subqueryRef = new SubqueryReference(subquery);
            subqueryRef.Alias = Alias();

            // Optional derived column aliases: (col1, col2, ...)
            if (Check(TokenType.LEFT_PAREN))
            {
                subqueryRef.ColumnAliases = ParseDerivedColumnAliases();
            }

            return subqueryRef;
        }

        private TableVariableReference ParseTableVariable()
        {
            Token variable = Consume(TokenType.VARIABLE, "Expected table variable");
            TableVariableReference varRef = new TableVariableReference(variable);
            varRef.Alias = Alias();

            return varRef;
        }

        private ValuesTableSource ParseValuesTableSource()
        {
            Token outerLeftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
            Token valuesToken = Consume(TokenType.VALUES, "Expected VALUES");

            SyntaxElementList<ValuesRow> rows = new SyntaxElementList<ValuesRow>();
            rows.Add(ParseValuesRow());

            while (Match(TokenType.COMMA, out Token comma))
            {
                rows.Add(ParseValuesRow(), comma);
            }

            Token outerRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            ValuesTableSource valuesSource = new ValuesTableSource(rows);
            valuesSource._outerLeftParen = outerLeftParen;
            valuesSource._valuesToken = valuesToken;
            valuesSource._outerRightParen = outerRightParen;
            valuesSource.Alias = Alias();

            // Optional derived column aliases: (col1, col2, ...)
            if (Check(TokenType.LEFT_PAREN))
            {
                valuesSource.ColumnAliases = ParseDerivedColumnAliases();
            }

            return valuesSource;
        }

        private ValuesRow ParseValuesRow()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            SyntaxElementList<Expr> values = new SyntaxElementList<Expr>();
            values.Add(Expression());

            while (Match(TokenType.COMMA, out Token comma))
            {
                values.Add(Expression(), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            ValuesRow row = new ValuesRow(values);
            row._leftParen = leftParen;
            row._rightParen = rightParen;
            return row;
        }

        private RowsetFunctionReference ParseRowsetFunction()
        {
            // OPENROWSET, OPENQUERY, OPENDATASOURCE are reserved keywords,
            // so we consume the token directly and build the ObjectIdentifier manually.
            Token keywordToken = Advance();
            Expr.ObjectIdentifier functionId = new Expr.ObjectIdentifier(new ObjectName(keywordToken));
            Expr.FunctionCall functionCall = FinishCall(functionId);

            RowsetFunctionReference rowsetRef = new RowsetFunctionReference(functionCall);
            rowsetRef.Alias = Alias();

            return rowsetRef;
        }

        private PivotTableSource ParsePivotTableSource(TableSource source)
        {
            Token pivotToken = Consume(TokenType.PIVOT, "Expected PIVOT");
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            // Parse aggregate function call: e.g. SUM(Amount)
            IdentifierPartsBuffer parts = CollectIdentifierParts();
            Expr.ObjectIdentifier functionId = FunctionIdentifier(parts);
            Expr.FunctionCall aggregateFunction = FinishCall(functionId);

            Token forToken = Consume(TokenType.FOR, "Expected FOR");

            // Parse pivot column identifier
            IdentifierPartsBuffer pivotParts = CollectIdentifierParts();
            Expr.ObjectIdentifier pivotColumn = FunctionIdentifier(pivotParts);

            Token inToken = Consume(TokenType.IN, "Expected IN");
            Token inLeftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            // PIVOT IN values are identifiers that become output column names
            SyntaxElementList<ColumnName> valueList = new SyntaxElementList<ColumnName>();
            valueList.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected identifier for PIVOT value")));
            while (Match(TokenType.COMMA, out Token comma))
            {
                valueList.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected identifier for PIVOT value")), comma);
            }

            Token inRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            PivotTableSource pivot = new PivotTableSource(source, aggregateFunction, pivotColumn, valueList);
            pivot._pivotToken = pivotToken;
            pivot._leftParen = leftParen;
            pivot._forToken = forToken;
            pivot._inToken = inToken;
            pivot._inLeftParen = inLeftParen;
            pivot._inRightParen = inRightParen;
            pivot._rightParen = rightParen;
            pivot.Alias = Alias();

            return pivot;
        }

        private UnpivotTableSource ParseUnpivotTableSource(TableSource source)
        {
            Token unpivotToken = Consume(TokenType.UNPIVOT, "Expected UNPIVOT");
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            // Parse value column identifier
            IdentifierPartsBuffer valueParts = CollectIdentifierParts();
            Expr.ObjectIdentifier valueColumn = FunctionIdentifier(valueParts);

            Token forToken = Consume(TokenType.FOR, "Expected FOR");

            // Parse pivot column identifier
            IdentifierPartsBuffer pivotParts = CollectIdentifierParts();
            Expr.ObjectIdentifier pivotColumn = FunctionIdentifier(pivotParts);

            Token inToken = Consume(TokenType.IN, "Expected IN");
            Token inLeftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            // Parse column list
            SyntaxElementList<ColumnName> columnList = new SyntaxElementList<ColumnName>();
            columnList.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")));
            while (Match(TokenType.COMMA, out Token comma))
            {
                columnList.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")), comma);
            }

            Token inRightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            UnpivotTableSource unpivot = new UnpivotTableSource(source, valueColumn, pivotColumn, columnList);
            unpivot._unpivotToken = unpivotToken;
            unpivot._leftParen = leftParen;
            unpivot._forToken = forToken;
            unpivot._inToken = inToken;
            unpivot._inLeftParen = inLeftParen;
            unpivot._inRightParen = inRightParen;
            unpivot._rightParen = rightParen;
            unpivot.Alias = Alias();

            return unpivot;
        }

        private DerivedColumnAliases ParseDerivedColumnAliases()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");

            SyntaxElementList<ColumnName> columnNames = new SyntaxElementList<ColumnName>();
            columnNames.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")));
            while (Match(TokenType.COMMA, out Token comma))
            {
                columnNames.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            DerivedColumnAliases aliases = new DerivedColumnAliases(columnNames);
            aliases._leftParen = leftParen;
            aliases._rightParen = rightParen;
            return aliases;
        }

        private Alias Alias()
        {
            if (Match(TokenType.AS, out Token asToken))
            {
                // After AS, expect an identifier or contextual keyword
                SuffixAlias alias = new SuffixAlias(ConsumeIdentifierOrContextualKeyword("Expected alias"));
                alias._asKeyword = asToken;

                return alias;
            }
            else if (IsIdentifierOrContextualKeyword())
            {
                // Contextual keywords can be used as aliases without AS
                SuffixAlias alias = new SuffixAlias(Advance());
                alias._asKeyword = ConcreteToken.Empty;
                return alias;
            }

            return null;
        }


        /// <summary>
        /// Parses the column argument for CONTAINS/FREETEXT: * | column | (column_list)
        /// </summary>
        private Predicate.FullTextColumns ParseFullTextColumns()
        {
            if (Check(TokenType.STAR))
            {
                Predicate.FullTextAllColumns allColumns = new Predicate.FullTextAllColumns();
                allColumns._wildcardToken = Advance();
                return allColumns;
            }

            if (Match(TokenType.LEFT_PAREN, out Token leftParen))
            {
                SyntaxElementList<Expr.ColumnIdentifier> columns = new SyntaxElementList<Expr.ColumnIdentifier>();
                IdentifierPartsBuffer parts = CollectIdentifierParts();
                columns.Add(ColumnIdentifier(parts));

                while (Match(TokenType.COMMA, out Token comma))
                {
                    parts = CollectIdentifierParts();
                    columns.Add(ColumnIdentifier(parts), comma);
                }

                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after column list");

                Predicate.FullTextColumnNames columnNames = new Predicate.FullTextColumnNames(columns);
                columnNames._leftParen = leftParen;
                columnNames._rightParen = rightParen;
                return columnNames;
            }

            {
                SyntaxElementList<Expr.ColumnIdentifier> columns = new SyntaxElementList<Expr.ColumnIdentifier>();
                IdentifierPartsBuffer parts = CollectIdentifierParts();
                columns.Add(ColumnIdentifier(parts));

                Predicate.FullTextColumnNames columnNames = new Predicate.FullTextColumnNames(columns);
                return columnNames;
            }
        }

        #region Search Condition Parsing

        private Predicate SearchCondition()
        {
            return OrPredicate();
        }

        private Predicate OrPredicate()
        {
            Predicate left = AndPredicate();

            while (Match(TokenType.OR, out Token orToken))
            {
                Predicate right = AndPredicate();
                Predicate.Or or = new Predicate.Or(left, right);
                or._orToken = orToken;
                left = or;
            }

            return left;
        }

        private Predicate AndPredicate()
        {
            Predicate left = UnaryPredicate();

            while (Match(TokenType.AND, out Token andToken))
            {
                Predicate right = UnaryPredicate();
                Predicate.And and = new Predicate.And(left, right);
                and._andToken = andToken;
                left = and;
            }

            return left;
        }

        private Predicate UnaryPredicate()
        {
            if (Match(TokenType.NOT, out Token notToken))
            {
                Predicate predicate = UnaryPredicate();
                Predicate.Not not = new Predicate.Not(predicate);
                not._notToken = notToken;
                return not;
            }

            return PrimaryPredicate();
        }

        private Predicate PrimaryPredicate()
        {
            // EXISTS (select_expression)
            if (Match(TokenType.EXISTS, out Token existsToken))
            {
                Expr.Subquery subquery = Subquery();
                Predicate.Exists exists = new Predicate.Exists(subquery);
                exists._existsToken = existsToken;
                return exists;
            }

            // CONTAINS ( { column | (column_list) | * } , search_condition [, LANGUAGE language_term] )
            if (Match(TokenType.CONTAINS, out Token containsToken))
            {
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after CONTAINS");
                Predicate.FullTextColumns columns = ParseFullTextColumns();
                Token comma = Consume(TokenType.COMMA, "Expected ',' in CONTAINS");
                Expr searchExpr = Expression();

                Token languageComma = null;
                Token languageKeyword = null;
                Expr language = null;
                if (Match(TokenType.COMMA, out languageComma))
                {
                    languageKeyword = Consume(TokenType.LANGUAGE, "Expected LANGUAGE keyword");
                    language = Expression();
                }

                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after CONTAINS");

                Predicate.Contains contains = new Predicate.Contains(columns, searchExpr);
                contains._containsToken = containsToken;
                contains._leftParen = leftParen;
                contains._comma = comma;
                contains._languageComma = languageComma;
                contains._languageKeyword = languageKeyword;
                contains.Language = language;
                contains._rightParen = rightParen;
                return contains;
            }

            // FREETEXT ( { column | (column_list) | * } , freetext_string [, LANGUAGE language_term] )
            if (Match(TokenType.FREETEXT, out Token freetextToken))
            {
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after FREETEXT");
                Predicate.FullTextColumns columns = ParseFullTextColumns();
                Token comma = Consume(TokenType.COMMA, "Expected ',' in FREETEXT");
                Expr searchExpr = Expression();

                Token languageComma = null;
                Token languageKeyword = null;
                Expr language = null;
                if (Match(TokenType.COMMA, out languageComma))
                {
                    languageKeyword = Consume(TokenType.LANGUAGE, "Expected LANGUAGE keyword");
                    language = Expression();
                }

                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after FREETEXT");

                Predicate.Freetext freetext = new Predicate.Freetext(columns, searchExpr);
                freetext._freetextToken = freetextToken;
                freetext._leftParen = leftParen;
                freetext._comma = comma;
                freetext._languageComma = languageComma;
                freetext._languageKeyword = languageKeyword;
                freetext.Language = language;
                freetext._rightParen = rightParen;
                return freetext;
            }

            // Grouped predicate: (search_condition)
            // Disambiguate from expression grouping like (a + b) > 0 using backtracking.
            // Try parsing as grouped predicate first; if it fails, fall through to
            // expression-based predicate parsing.
            if (Check(TokenType.LEFT_PAREN) && !CheckNext(TokenType.SELECT))
            {
                int saved = _current;
                try
                {
                    Token leftParen = Advance();
                    Predicate inner = SearchCondition();
                    if (Check(TokenType.RIGHT_PAREN))
                    {
                        Token rightParen = Advance();
                        Predicate.Grouping grouping = new Predicate.Grouping(inner);
                        grouping._leftParen = leftParen;
                        grouping._rightParen = rightParen;
                        return grouping;
                    }
                }
                catch (ParseError) { }
                _current = saved;
            }

            // Expression-based predicates: parse left expression, then check what follows
            Expr leftExpr = Expression();

            // comparison_operator expression
            if (IsComparisonOperator())
            {
                Token op = Advance();

                // Check for quantified predicate: op (ALL|SOME|ANY) (select)
                if (Check(TokenType.ALL, TokenType.SOME, TokenType.ANY))
                {
                    Token quantifier = Advance();
                    Expr.Subquery subquery = Subquery();
                    Predicate.Quantifier quant = new Predicate.Quantifier(leftExpr, op, quantifier, subquery);
                    return quant;
                }

                Expr rightExpr = Expression();
                return new Predicate.Comparison(leftExpr, op, rightExpr);
            }

            // [NOT] LIKE expression [ESCAPE string]
            if (Check(TokenType.LIKE) || (Check(TokenType.NOT) && CheckNext(TokenType.LIKE)))
            {
                var (notToken, negated) = TryConsumeNot();
                Token likeToken = Consume(TokenType.LIKE, "Expected LIKE");
                Expr pattern = Expression();

                Expr escapeExpr = null;
                Token escapeToken = null;
                if (Match(TokenType.ESCAPE, out escapeToken))
                {
                    escapeExpr = Expression();
                }

                Predicate.Like like = new Predicate.Like(leftExpr, pattern, escapeExpr, negated);
                like._notToken = notToken;
                like._likeToken = likeToken;
                like._escapeToken = escapeToken;
                return like;
            }

            // [NOT] BETWEEN expression AND expression
            if (Check(TokenType.BETWEEN) || (Check(TokenType.NOT) && CheckNext(TokenType.BETWEEN)))
            {
                var (notToken, negated) = TryConsumeNot();
                Token betweenToken = Consume(TokenType.BETWEEN, "Expected BETWEEN");
                Expr low = Expression();
                Token andToken = Consume(TokenType.AND, "Expected AND in BETWEEN");
                Expr high = Expression();

                Predicate.Between between = new Predicate.Between(leftExpr, low, high, negated);
                between._notToken = notToken;
                between._betweenToken = betweenToken;
                between._andToken = andToken;
                return between;
            }

            // IS [NOT] NULL
            if (Check(TokenType.IS))
            {
                Token isToken = Advance();
                var (notToken, negated) = TryConsumeNot();
                Token nullToken = Consume(TokenType.NULL, "Expected NULL after IS");

                Predicate.Null nullPred = new Predicate.Null(leftExpr, negated);
                nullPred._isToken = isToken;
                nullPred._notToken = notToken;
                nullPred._nullToken = nullToken;
                return nullPred;
            }

            // [NOT] IN (select_expression | expression_list)
            if (Check(TokenType.IN) || (Check(TokenType.NOT) && CheckNext(TokenType.IN)))
            {
                var (notToken, negated) = TryConsumeNot();
                Token inToken = Consume(TokenType.IN, "Expected IN");
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected ( after IN");

                Predicate.In inPred;
                if (Check(TokenType.SELECT))
                {
                    SelectExpression subSelect = SelectExpression();
                    Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ) after IN subquery");
                    Expr.Subquery sub = new Expr.Subquery(subSelect, leftParen, rightParen);
                    inPred = new Predicate.In(leftExpr, negated, sub);
                    inPred._rightParen = rightParen;
                }
                else
                {
                    SyntaxElementList<Expr> values = ParseExpressionList();
                    Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ) after IN list");
                    inPred = new Predicate.In(leftExpr, negated, values);
                    inPred._rightParen = rightParen;
                }

                inPred._notToken = notToken;
                inPred._inToken = inToken;
                inPred._leftParen = leftParen;
                return inPred;
            }

            throw Error(Peek(), "Expected predicate (comparison, LIKE, BETWEEN, IS NULL, IN, or EXISTS)");
        }

        private bool IsComparisonOperator()
        {
            if (IsAtEnd()) return false;
            TokenType type = Peek().Type;
            return type == TokenType.EQUAL || type == TokenType.NOT_EQUAL ||
                   type == TokenType.GREATER || type == TokenType.GREATER_EQUAL ||
                   type == TokenType.LESS || type == TokenType.LESS_EQUAL ||
                   type == TokenType.NOT_LESS || type == TokenType.NOT_GREATER;
        }

        #endregion

        private Expr Expression()
        {
            return Term();
        }

        private Expr Term()
        {
            Expr expr = Factor();

            while (Match(TokenType.PLUS, TokenType.MINUS, TokenType.BITWISE_AND, TokenType.BITWISE_XOR, TokenType.BITWISE_OR, out Token op))
            {
                Expr right = Factor();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expr Factor()
        {
            Expr expr = Unary();

            while (Match(TokenType.STAR, TokenType.SLASH, TokenType.MODULO, out Token op))
            {
                Expr right = Unary();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expr Unary()
        {
            if (Match(TokenType.MINUS, out Token minus))
            {
                return new Expr.Unary(minus, Postfix());
            }
            else if (Match(TokenType.BITWISE_NOT, out Token bitwiseNot))
            {
                return new Expr.Unary(bitwiseNot, Postfix());
            }
            else
            {
                return Postfix();
            }
        }

        private Expr Postfix()
        {
            Expr expr = Primary();

            if (Match(TokenType.COLLATE, out Token collateToken))
            {
                Token collationName = ConsumeIdentifierOrContextualKeyword("Expected collation name after COLLATE");
                Expr.Collate collate = new Expr.Collate(expr);
                collate._collateKeyword = collateToken;
                collate._collationName = collationName;
                return collate;
            }

            return expr;
        }

        private Expr Grouping()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected (");
            Expr expr = Expression();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected )");

            return new Expr.Grouping(expr, leftParen, rightParen);
        }

        private Expr Primary()
        {
            if (Match(TokenType.VARIABLE, out Token variableToken))
            {
                return new Expr.Variable(variableToken);
            }

            if (Match(TokenType.WHOLE_NUMBER, TokenType.DECIMAL, TokenType.STRING, out Token literalToken))
            {
                return new Expr.Literal(literalToken);
            }

            // NULL
            if (Match(TokenType.NULL, out Token nullToken))
            {
                return new Expr.Literal(nullToken);
            }

            // CASE WHEN ... THEN ... ELSE ... END
            if (Check(TokenType.CASE))
            {
                return ParseCaseExpression();
            }

            // CAST(expr AS type) or TRY_CAST(expr AS type)
            if (Check(TokenType.CAST) || Check(TokenType.TRY_CAST))
            {
                return ParseCast();
            }

            // CONVERT(type, expr [, style]) or TRY_CONVERT(type, expr [, style])
            if (Check(TokenType.CONVERT) || Check(TokenType.TRY_CONVERT))
            {
                return ParseConvert();
            }

            if (Check(TokenType.LEFT_PAREN))
            {
                if (CheckNext(TokenType.SELECT))
                {
                    return Subquery();
                }

                return Grouping();
            }

            // Handle COALESCE / NULLIF keywords - standard function call syntax
            if (Match(TokenType.COALESCE, TokenType.NULLIF, out Token builtinFuncToken))
            {
                ObjectIdentifier callee = new ObjectIdentifier(new ObjectName(builtinFuncToken));
                return FinishCall(callee);
            }

            // Handle OPENXML keyword - has optional WITH clause
            if (Match(TokenType.OPENXML, out Token openXmlToken))
            {
                return FinishOpenXml(openXmlToken);
            }

            // Handle IIF(condition, true_value, false_value)
            // First argument is a search condition (predicate), not an expression
            if (Check(TokenType.IIF) && CheckNext(TokenType.LEFT_PAREN))
            {
                Token iifToken = Advance();
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after IIF");
                Predicate condition = SearchCondition();
                Token firstComma = Consume(TokenType.COMMA, "Expected ',' after IIF condition");
                Expr trueValue = Expression();
                Token secondComma = Consume(TokenType.COMMA, "Expected ',' after IIF true value");
                Expr falseValue = Expression();
                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after IIF");

                Expr.Iif iif = new Expr.Iif(condition, trueValue, falseValue);
                iif._iifKeyword = iifToken;
                iif._leftParen = leftParen;
                iif._firstComma = firstComma;
                iif._secondComma = secondComma;
                iif._rightParen = rightParen;
                return iif;
            }

            // Handle ranking functions - they REQUIRE an OVER clause when used as functions
            // But if not followed by '(', treat as column identifier (contextual keyword)
            if (IsRankingFunction() && CheckNext(TokenType.LEFT_PAREN))
            {
                Token rankingToken = Advance();
                ObjectIdentifier callee = new ObjectIdentifier(new ObjectName(rankingToken));
                FunctionCall functionCall = FinishCall(callee);

                // OVER is required for ranking functions
                if (!Check(TokenType.OVER))
                {
                    throw Error(Peek(), $"Ranking function {rankingToken.Lexeme} requires an OVER clause");
                }

                OverClause overClause = ParseOverClause();
                return new Expr.WindowFunction(functionCall, overClause);
            }

            // Handle identifiers (columns) or function calls with optional OVER
            // This includes contextual keywords when not used as functions
            if (IsIdentifierOrContextualKeyword())
            {
                // Collect all the parts separated by dots
                IdentifierPartsBuffer parts = CollectIdentifierParts();

                if (Check(TokenType.LEFT_PAREN))
                {
                    ObjectIdentifier functionIdentifier = FunctionIdentifier(parts);
                    FunctionCall functionCall = FinishCall(functionIdentifier);

                    // Check for optional OVER clause
                    if (Check(TokenType.OVER))
                    {
                        OverClause overClause = ParseOverClause();
                        return new Expr.WindowFunction(functionCall, overClause);
                    }
                    return functionCall;
                }
                else
                {
                    return ColumnIdentifier(parts);
                }
            }

            throw Error(Peek(), $"Unexpected token");
        }


        /// <summary>
        /// Parses OPENXML(idoc, rowpattern [, flags]) [WITH (SchemaDeclaration | TableName)]
        /// </summary>
        private Expr FinishOpenXml(Token openXmlToken)
        {
            ObjectIdentifier callee = new ObjectIdentifier(new ObjectName(openXmlToken));
            Expr.FunctionCall functionCall = FinishCall(callee);

            // TODO: Handle optional WITH clause
            // if (Match(TokenType.WITH))
            // {
            //     Parse SchemaDeclaration or TableName
            //     Return a new OpenXmlExpr that wraps the function call + WITH clause
            // }

            return functionCall;
        }

        private Expr.FunctionCall FinishCall(ObjectIdentifier callee)
        {
            Token leftParen = Advance();

            SyntaxElementList<Expr> arguments = new SyntaxElementList<Expr>();
            if (!Check(TokenType.RIGHT_PAREN))
            {
                // Allow * as function argument, e.g. COUNT(*)
                Expr first = Check(TokenType.STAR)
                    ? (Expr)new Wildcard(Advance())
                    : Expression();
                arguments.Add(first);

                while (Match(TokenType.COMMA, out Token comma))
                {
                    Expr expr = Check(TokenType.STAR)
                        ? (Expr)new Wildcard(Advance())
                        : Expression();
                    arguments.Add(expr, comma);
                }
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expect ')' after arguments.");

            Expr.FunctionCall functionCall = new FunctionCall(callee, arguments);
            functionCall._leftParen = leftParen;
            functionCall._rightParen = rightParen;

            return functionCall;
        }

        #region Window Function Parsing

        /// <summary>
        /// Checks if the current token is a ranking function keyword (without consuming it).
        /// </summary>
        private bool IsRankingFunction()
        {
            return Check(TokenType.ROW_NUMBER) || Check(TokenType.RANK) ||
                   Check(TokenType.DENSE_RANK) || Check(TokenType.NTILE);
        }

        /// <summary>
        /// Parses the OVER clause: OVER (PARTITION BY ... ORDER BY ... ROWS/RANGE ...)
        /// </summary>
        private OverClause ParseOverClause()
        {
            OverClause clause = new OverClause();
            clause._overKeyword = Consume(TokenType.OVER, "Expected OVER");
            clause._leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after OVER");

            // Optional PARTITION BY
            if (Match(TokenType.PARTITION, out Token partitionToken))
            {
                clause._partitionKeyword = partitionToken;
                clause._partitionByKeyword = Consume(TokenType.BY, "Expected BY after PARTITION");
                clause.PartitionBy = ParseExpressionList();
            }

            // Optional ORDER BY
            if (Match(TokenType.ORDER, out Token orderToken))
            {
                clause._orderKeyword = orderToken;
                clause._orderByKeyword = Consume(TokenType.BY, "Expected BY after ORDER");
                clause.OrderBy = ParseOrderByList();
            }

            // Optional ROWS or RANGE frame clause (requires ORDER BY)
            if (Check(TokenType.ROWS, TokenType.RANGE))
            {
                if (clause.OrderBy == null || clause.OrderBy.Count == 0)
                {
                    throw Error(Peek(), "ROWS or RANGE clause requires ORDER BY");
                }
                clause.Frame = ParseWindowFrame();
            }

            clause._rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after OVER clause");
            return clause;
        }

        /// <summary>
        /// Parses a window frame clause: ROWS/RANGE [BETWEEN bound AND bound | bound]
        /// </summary>
        private WindowFrame ParseWindowFrame()
        {
            Token rowsOrRangeToken;
            WindowFrameType frameType;

            if (Match(TokenType.ROWS, out rowsOrRangeToken))
            {
                frameType = WindowFrameType.Rows;
            }
            else
            {
                rowsOrRangeToken = Consume(TokenType.RANGE, "Expected ROWS or RANGE");
                frameType = WindowFrameType.Range;
            }

            WindowFrameBound start;
            WindowFrameBound end = null;
            Token betweenToken = null;
            Token andToken = null;

            // Check for BETWEEN syntax
            if (Match(TokenType.BETWEEN, out betweenToken))
            {
                start = ParseWindowFrameBound(allowFollowing: true);
                andToken = Consume(TokenType.AND, "Expected AND in BETWEEN clause");
                end = ParseWindowFrameBound(allowFollowing: true);
            }
            else
            {
                // Short syntax: just a single bound (implies AND CURRENT ROW)
                start = ParseWindowFrameBound(allowFollowing: false);
            }

            WindowFrame frame = new WindowFrame(frameType, start, end);
            frame._rowsOrRangeToken = rowsOrRangeToken;
            frame._betweenToken = betweenToken;
            frame._andToken = andToken;

            return frame;
        }

        /// <summary>
        /// Parses a single window frame bound (e.g., UNBOUNDED PRECEDING, CURRENT ROW, 3 PRECEDING)
        /// </summary>
        private WindowFrameBound ParseWindowFrameBound(bool allowFollowing)
        {
            WindowFrameBound bound;

            if (Match(TokenType.UNBOUNDED, out Token unboundedToken))
            {
                if (Match(TokenType.PRECEDING, out Token precedingToken))
                {
                    bound = new WindowFrameBound(WindowFrameBoundType.UnboundedPreceding);
                    bound._unboundedToken = unboundedToken;
                    bound._precedingToken = precedingToken;
                }
                else if (allowFollowing && Match(TokenType.FOLLOWING, out Token followingToken))
                {
                    bound = new WindowFrameBound(WindowFrameBoundType.UnboundedFollowing);
                    bound._unboundedToken = unboundedToken;
                    bound._followingToken = followingToken;
                }
                else
                {
                    throw Error(Peek(), allowFollowing
                        ? "Expected PRECEDING or FOLLOWING after UNBOUNDED"
                        : "Expected PRECEDING after UNBOUNDED");
                }
            }
            else if (Match(TokenType.CURRENT, out Token currentToken))
            {
                Token rowToken = Consume(TokenType.ROW, "Expected ROW after CURRENT");
                bound = new WindowFrameBound(WindowFrameBoundType.CurrentRow);
                bound._currentToken = currentToken;
                bound._rowToken = rowToken;
            }
            else if (Check(TokenType.WHOLE_NUMBER))
            {
                Expr offset = new Expr.Literal(Advance());

                if (Match(TokenType.PRECEDING, out Token precedingToken))
                {
                    bound = new WindowFrameBound(WindowFrameBoundType.Preceding, offset);
                    bound._precedingToken = precedingToken;
                }
                else if (allowFollowing && Match(TokenType.FOLLOWING, out Token followingToken))
                {
                    bound = new WindowFrameBound(WindowFrameBoundType.Following, offset);
                    bound._followingToken = followingToken;
                }
                else
                {
                    throw Error(Peek(), allowFollowing
                        ? "Expected PRECEDING or FOLLOWING after number"
                        : "Expected PRECEDING after number");
                }
            }
            else
            {
                throw Error(Peek(), "Expected window frame bound (UNBOUNDED PRECEDING, CURRENT ROW, N PRECEDING, etc.)");
            }

            return bound;
        }

        /// <summary>
        /// Parses a comma-separated list of expressions (for PARTITION BY)
        /// </summary>
        private SyntaxElementList<Expr> ParseExpressionList()
        {
            SyntaxElementList<Expr> list = new SyntaxElementList<Expr>();
            list.Add(Expression());

            while (Match(TokenType.COMMA, out Token comma))
            {
                list.Add(Expression(), comma);
            }

            return list;
        }

        /// <summary>
        /// Parses a comma-separated list of ORDER BY items
        /// </summary>
        private SyntaxElementList<OrderByItem> ParseOrderByList()
        {
            SyntaxElementList<OrderByItem> list = new SyntaxElementList<OrderByItem>();
            list.Add(ParseOrderByItem());

            while (Match(TokenType.COMMA, out Token comma))
            {
                list.Add(ParseOrderByItem(), comma);
            }

            return list;
        }

        private OrderByItem ParseOrderByItem()
        {
            Expr expr = Expression();

            Token orderToken = null;
            bool desc = false;
            if (Match(TokenType.DESC, out orderToken))
            {
                desc = true;
            }
            else
            {
                Match(TokenType.ASC, out orderToken);
            }

            OrderByItem item = new OrderByItem { Expression = expr, Descending = desc };
            item._orderToken = orderToken;
            return item;
        }

        #endregion

        #region CTE Parsing

        /// <summary>
        /// WITH cte1(a, b) AS (SELECT ...), cte2 AS (SELECT ...)
        /// </summary>
        private Cte ParseCte()
        {
            Cte cte = new Cte();
            cte._withToken = Consume(TokenType.WITH, "Expected WITH");

            cte.Ctes.Add(ParseCteDefinition());

            while (Match(TokenType.COMMA, out Token comma))
            {
                cte.Ctes.Add(ParseCteDefinition(), comma);
            }

            return cte;
        }

        /// <summary>
        /// cte_name(col1, col2) AS (SELECT ...)
        /// </summary>
        private CteDefinition ParseCteDefinition()
        {
            CteDefinition def = new CteDefinition();
            def.Name = ConsumeIdentifierOrContextualKeyword("Expected CTE name");

            // Optional column list — if next is NOT AS, it must be (col1, col2)
            if (!Check(TokenType.AS))
            {
                def.ColumnNames = ParseCteColumnNames();
            }

            def._asToken = Consume(TokenType.AS, "Expected AS after CTE name");
            def.Query = Subquery();

            return def;
        }

        /// <summary>
        /// (col1, col2, col3)
        /// </summary>
        private CteColumnNames ParseCteColumnNames()
        {
            CteColumnNames colNames = new CteColumnNames();
            colNames._leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' for CTE column list");

            SyntaxElementList<ColumnName> names = new SyntaxElementList<ColumnName>();
            names.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")));

            while (Match(TokenType.COMMA, out Token comma))
            {
                names.Add(new ColumnName(ConsumeIdentifierOrContextualKeyword("Expected column name")), comma);
            }

            colNames.ColumnNames = names;
            colNames._rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after CTE column list");

            return colNames;
        }

        #endregion

        #region GROUP BY Parsing

        /// <summary>
        /// GROUP BY a, ROLLUP(b, c), GROUPING SETS((a, b), ())
        /// </summary>
        private GroupByClause GroupByClause()
        {
            Token groupKeyword = Consume(TokenType.GROUP, "Expected GROUP");
            Token byKeyword = Consume(TokenType.BY, "Expected BY after GROUP");

            SyntaxElementList<GroupByItem> items = new SyntaxElementList<GroupByItem>();
            items.Add(GroupByItem());

            while (Match(TokenType.COMMA, out Token comma))
            {
                items.Add(GroupByItem(), comma);
            }

            return new GroupByClause(groupKeyword, byKeyword, items);
        }

        /// <summary>
        /// a | ROLLUP(...) | CUBE(...) | GROUPING SETS(...) | () | (a, b)
        /// </summary>
        private GroupByItem GroupByItem()
        {
            // ROLLUP(...)
            if (Check(TokenType.ROLLUP) && CheckNext(TokenType.LEFT_PAREN))
            {
                return ParseGroupByRollup();
            }

            // CUBE(...)
            if (Check(TokenType.CUBE) && CheckNext(TokenType.LEFT_PAREN))
            {
                return ParseGroupByCube();
            }

            // GROUPING SETS(...)
            if (Check(TokenType.GROUPING) && CheckNext(TokenType.SETS))
            {
                return ParseGroupByGroupingSets();
            }

            // () grand total or (a, b) composite
            if (Check(TokenType.LEFT_PAREN))
            {
                return ParseGroupByParenthesized();
            }

            // Simple expression
            Expr expr = Expression();
            return new GroupByExpression(expr);
        }

        /// <summary>
        /// ROLLUP(a, b, (c, d))
        /// </summary>
        private GroupByRollup ParseGroupByRollup()
        {
            Token keyword = Advance(); // ROLLUP
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after ROLLUP");
            SyntaxElementList<GroupByItem> items = ParseGroupByExpressionList();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after ROLLUP arguments");
            return new GroupByRollup(keyword, leftParen, items, rightParen);
        }

        /// <summary>
        /// CUBE(a, b, (c, d))
        /// </summary>
        private GroupByCube ParseGroupByCube()
        {
            Token keyword = Advance(); // CUBE
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after CUBE");
            SyntaxElementList<GroupByItem> items = ParseGroupByExpressionList();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after CUBE arguments");
            return new GroupByCube(keyword, leftParen, items, rightParen);
        }

        /// <summary>
        /// GROUPING SETS((a, b), a, (), ROLLUP(c, d))
        /// </summary>
        private GroupByGroupingSets ParseGroupByGroupingSets()
        {
            Token groupingKeyword = Advance(); // GROUPING
            Token setsKeyword = Consume(TokenType.SETS, "Expected SETS after GROUPING");
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after GROUPING SETS");

            SyntaxElementList<GroupByItem> items = new SyntaxElementList<GroupByItem>();
            items.Add(ParseGroupingSetItem());

            while (Match(TokenType.COMMA, out Token comma))
            {
                items.Add(ParseGroupingSetItem(), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after GROUPING SETS arguments");
            return new GroupByGroupingSets(groupingKeyword, setsKeyword, leftParen, items, rightParen);
        }

        /// <summary>
        /// Items inside ROLLUP/CUBE: a | (a, b)
        /// </summary>
        private SyntaxElementList<GroupByItem> ParseGroupByExpressionList()
        {
            SyntaxElementList<GroupByItem> items = new SyntaxElementList<GroupByItem>();
            items.Add(ParseGroupByExpressionListItem());

            while (Match(TokenType.COMMA, out Token comma))
            {
                items.Add(ParseGroupByExpressionListItem(), comma);
            }

            return items;
        }

        private GroupByItem ParseGroupByExpressionListItem()
        {
            if (Check(TokenType.LEFT_PAREN))
            {
                return ParseGroupByComposite();
            }

            return new GroupByExpression(Expression());
        }

        /// <summary>
        /// (a, b) — composite column group
        /// </summary>
        private GroupByComposite ParseGroupByComposite()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '('");
            SyntaxElementList<Expr> expressions = ParseExpressionList();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after composite group");
            return new GroupByComposite(leftParen, expressions, rightParen);
        }

        /// <summary>
        /// Items inside GROUPING SETS: () | ROLLUP(...) | CUBE(...) | (a, b) | a
        /// </summary>
        private GroupByItem ParseGroupingSetItem()
        {
            // ROLLUP(...) inside GROUPING SETS
            if (Check(TokenType.ROLLUP) && CheckNext(TokenType.LEFT_PAREN))
            {
                return ParseGroupByRollup();
            }

            // CUBE(...) inside GROUPING SETS
            if (Check(TokenType.CUBE) && CheckNext(TokenType.LEFT_PAREN))
            {
                return ParseGroupByCube();
            }

            // () grand total or (a, b) composite
            if (Check(TokenType.LEFT_PAREN))
            {
                return ParseGroupByParenthesized();
            }

            // Simple expression
            Expr expr = Expression();
            return new GroupByExpression(expr);
        }

        /// <summary>
        /// () — grand total, or (a, b) — composite column group
        /// </summary>
        private GroupByItem ParseGroupByParenthesized()
        {
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '('");

            // () grand total
            if (Check(TokenType.RIGHT_PAREN))
            {
                Token rightParen = Advance();
                return new GroupByGrandTotal(leftParen, rightParen);
            }

            // (a, b, ...) composite
            SyntaxElementList<Expr> expressions = ParseExpressionList();
            Token closeParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after composite group");
            return new GroupByComposite(leftParen, expressions, closeParen);
        }

        #endregion

        #region CASE / CAST / CONVERT Parsing

        /// <summary>
        /// CASE expr WHEN val THEN result ... END  (simple)
        /// CASE WHEN condition THEN result ... END  (searched)
        /// </summary>
        private Expr ParseCaseExpression()
        {
            Token caseToken = Consume(TokenType.CASE, "Expected CASE");

            // Searched CASE: CASE WHEN ...
            if (Check(TokenType.WHEN))
            {
                return ParseSearchedCase(caseToken);
            }

            // Simple CASE: CASE expr WHEN ...
            return ParseSimpleCase(caseToken);
        }

        /// <summary>
        /// CASE expr WHEN val THEN result [...] [ELSE default] END
        /// </summary>
        private Expr ParseSimpleCase(Token caseToken)
        {
            Expr operand = Expression();

            List<Expr.SimpleCaseWhen> whenClauses = new List<Expr.SimpleCaseWhen>();
            while (Check(TokenType.WHEN))
            {
                Token whenToken = Advance();
                Expr value = Expression();
                Token thenToken = Consume(TokenType.THEN, "Expected THEN after WHEN value");
                Expr result = Expression();

                Expr.SimpleCaseWhen when = new Expr.SimpleCaseWhen(value, result);
                when._whenToken = whenToken;
                when._thenToken = thenToken;
                whenClauses.Add(when);
            }

            Expr elseResult = null;
            Token elseToken = null;
            if (Match(TokenType.ELSE, out elseToken))
            {
                elseResult = Expression();
            }

            Token endToken = Consume(TokenType.END, "Expected END after CASE expression");

            Expr.SimpleCase simpleCase = new Expr.SimpleCase(operand, whenClauses, elseResult);
            simpleCase._caseToken = caseToken;
            simpleCase._elseToken = elseToken;
            simpleCase._endToken = endToken;
            return simpleCase;
        }

        /// <summary>
        /// CASE WHEN condition THEN result [...] [ELSE default] END
        /// </summary>
        private Expr ParseSearchedCase(Token caseToken)
        {
            List<Expr.SearchedCaseWhen> whenClauses = new List<Expr.SearchedCaseWhen>();
            while (Check(TokenType.WHEN))
            {
                Token whenToken = Advance();
                Predicate condition = SearchCondition();
                Token thenToken = Consume(TokenType.THEN, "Expected THEN after WHEN condition");
                Expr result = Expression();

                Expr.SearchedCaseWhen when = new Expr.SearchedCaseWhen(condition, result);
                when._whenToken = whenToken;
                when._thenToken = thenToken;
                whenClauses.Add(when);
            }

            Expr elseResult = null;
            Token elseToken = null;
            if (Match(TokenType.ELSE, out elseToken))
            {
                elseResult = Expression();
            }

            Token endToken = Consume(TokenType.END, "Expected END after CASE expression");

            Expr.SearchedCase searchedCase = new Expr.SearchedCase(whenClauses, elseResult);
            searchedCase._caseToken = caseToken;
            searchedCase._elseToken = elseToken;
            searchedCase._endToken = endToken;
            return searchedCase;
        }

        /// <summary>
        /// CAST(expr AS VARCHAR(50)) or TRY_CAST(expr AS INT)
        /// </summary>
        private Expr ParseCast()
        {
            Token keyword = Advance(); // CAST or TRY_CAST
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after " + keyword.Lexeme);
            Expr expression = Expression();
            Token asToken = Consume(TokenType.AS, "Expected AS in " + keyword.Lexeme + " expression");
            DataType dataType = ParseDataType();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after " + keyword.Lexeme + " expression");

            Expr.CastExpression cast = new Expr.CastExpression(expression, dataType);
            cast._castKeyword = keyword;
            cast._leftParen = leftParen;
            cast._asToken = asToken;
            cast._rightParen = rightParen;
            return cast;
        }

        /// <summary>
        /// CONVERT(INT, expr) or TRY_CONVERT(VARCHAR(50), expr, 121)
        /// </summary>
        private Expr ParseConvert()
        {
            Token keyword = Advance(); // CONVERT or TRY_CONVERT
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after " + keyword.Lexeme);
            DataType dataType = ParseDataType();
            Token commaAfterType = Consume(TokenType.COMMA, "Expected ',' after data type in " + keyword.Lexeme);
            Expr expression = Expression();

            Expr style = null;
            Token commaAfterExpr = null;
            if (Match(TokenType.COMMA, out commaAfterExpr))
            {
                style = Expression();
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after " + keyword.Lexeme + " expression");

            Expr.ConvertExpression convert = new Expr.ConvertExpression(dataType, expression, style);
            convert._convertKeyword = keyword;
            convert._leftParen = leftParen;
            convert._commaAfterType = commaAfterType;
            convert._commaAfterExpr = commaAfterExpr;
            convert._rightParen = rightParen;
            return convert;
        }

        /// <summary>
        /// INT | VARCHAR(50) | DECIMAL(10, 2) | NVARCHAR(MAX)
        /// </summary>
        private DataType ParseDataType()
        {
            Token typeName = ConsumeIdentifierOrContextualKeyword("Expected data type name");

            if (!Check(TokenType.LEFT_PAREN))
            {
                return new DataType(typeName);
            }

            Token leftParen = Advance();
            SyntaxElementList<Expr> parameters = ParseExpressionList();
            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after data type parameters");

            DataType dt = new DataType(typeName, parameters);
            dt._leftParen = leftParen;
            dt._rightParen = rightParen;
            return dt;
        }

        #endregion

        #region OPTION / Query Hint Parsing

        private static readonly Dictionary<string, QueryHintType> SimpleQueryHints =
            new Dictionary<string, QueryHintType>(StringComparer.OrdinalIgnoreCase)
            {
                { "RECOMPILE", QueryHintType.Recompile },
                { "NO_PERFORMANCE_SPOOL", QueryHintType.NoPerformanceSpool },
                { "IGNORE_NONCLUSTERED_COLUMNSTORE_INDEX", QueryHintType.IgnoreNonclusteredColumnstoreIndex },
                { "DISABLE_OPTIMIZED_PLAN_FORCING", QueryHintType.DisableOptimizedPlanForcing },
            };

        /// <summary>
        /// OPTION ( query_hint [, ...n] )
        /// </summary>
        private OptionClause ParseOptionClause()
        {
            Token optionToken = Consume(TokenType.OPTION, "Expected OPTION");
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after OPTION");

            SyntaxElementList<QueryHint> hints = new SyntaxElementList<QueryHint>();
            hints.Add(ParseQueryHint());

            while (Match(TokenType.COMMA, out Token comma))
            {
                hints.Add(ParseQueryHint(), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')' after query hints");

            OptionClause clause = new OptionClause(hints);
            clause._optionToken = optionToken;
            clause._leftParen = leftParen;
            clause._rightParen = rightParen;
            return clause;
        }

        /// <summary>
        /// query_hint ::= { HASH | ORDER } GROUP | { CONCAT | HASH | MERGE } UNION
        ///   | { LOOP | MERGE | HASH } JOIN | RECOMPILE | FAST N | MAXDOP N | ...
        /// </summary>
        private QueryHint ParseQueryHint()
        {
            // --- Keyword token types first ---

            // HASH GROUP / HASH UNION / HASH JOIN
            if (Check(TokenType.HASH))
            {
                Token hintToken = Advance();
                Token second;
                QueryHintType type;
                if (Check(TokenType.GROUP)) { second = Advance(); type = QueryHintType.HashGroup; }
                else if (Check(TokenType.UNION)) { second = Advance(); type = QueryHintType.HashUnion; }
                else if (Check(TokenType.JOIN)) { second = Advance(); type = QueryHintType.HashJoin; }
                else throw Error(Peek(), "Expected GROUP, UNION, or JOIN after HASH");
                QueryHint hint = new QueryHint(type);
                hint._hintToken = hintToken;
                hint._hintToken2 = second;
                return hint;
            }

            // MERGE UNION / MERGE JOIN
            if (Check(TokenType.MERGE))
            {
                Token hintToken = Advance();
                Token second;
                QueryHintType type;
                if (Check(TokenType.UNION)) { second = Advance(); type = QueryHintType.MergeUnion; }
                else if (Check(TokenType.JOIN)) { second = Advance(); type = QueryHintType.MergeJoin; }
                else throw Error(Peek(), "Expected UNION or JOIN after MERGE");
                QueryHint hint = new QueryHint(type);
                hint._hintToken = hintToken;
                hint._hintToken2 = second;
                return hint;
            }

            // LOOP JOIN
            if (Check(TokenType.LOOP))
            {
                Token hintToken = Advance();
                Token second = Consume(TokenType.JOIN, "Expected JOIN after LOOP");
                QueryHint hint = new QueryHint(QueryHintType.LoopJoin);
                hint._hintToken = hintToken;
                hint._hintToken2 = second;
                return hint;
            }

            // ORDER GROUP
            if (Check(TokenType.ORDER))
            {
                Token hintToken = Advance();
                Token second = Consume(TokenType.GROUP, "Expected GROUP after ORDER");
                QueryHint hint = new QueryHint(QueryHintType.OrderGroup);
                hint._hintToken = hintToken;
                hint._hintToken2 = second;
                return hint;
            }

            // USE HINT / USE PLAN
            if (Check(TokenType.USE))
            {
                Token hintToken = Advance();
                if (IsIdentifierWithLexeme("HINT"))
                {
                    return ParseUseHint(hintToken);
                }
                if (Check(TokenType.PLAN))
                {
                    Token planToken = Advance();
                    Expr value = Expression();
                    return MakeUsePlanHint(hintToken, planToken, value);
                }
                throw Error(Peek(), "Expected HINT or PLAN after USE");
            }

            // FOR TIMESTAMP AS OF 'time'
            if (Check(TokenType.FOR))
            {
                Token forToken = Advance();
                if (IsIdentifierWithLexeme("TIMESTAMP"))
                {
                    Token timestampToken = Advance();
                    Token asToken = Consume(TokenType.AS, "Expected AS after TIMESTAMP");
                    Token ofToken = Consume(TokenType.OF, "Expected OF after AS");
                    Expr value = Expression();
                    QueryHint hint = new QueryHint(QueryHintType.ForTimestamp, value);
                    hint._hintToken = forToken;
                    hint._timestampToken = timestampToken;
                    hint._asToken = asToken;
                    hint._ofToken = ofToken;
                    return hint;
                }
                throw Error(Peek(), "Expected TIMESTAMP after FOR in query hint");
            }

            // TABLE HINT ( exposed_object_name [, table_hint ...] )
            if (Check(TokenType.TABLE))
            {
                Token tableToken = Advance();
                if (IsIdentifierWithLexeme("HINT"))
                {
                    return ParseTableHintQueryHint(tableToken);
                }
                throw Error(Peek(), "Expected HINT after TABLE");
            }

            // --- Identifier lexeme dispatch ---
            if (IsIdentifierOrContextualKeyword())
            {
                string lexeme = Peek().Lexeme;

                // Simple single-token hints
                if (SimpleQueryHints.TryGetValue(lexeme, out QueryHintType simpleType))
                {
                    Token hintToken = Advance();
                    QueryHint hint = new QueryHint(simpleType);
                    hint._hintToken = hintToken;
                    return hint;
                }

                // EXPAND VIEWS
                if (lexeme.Equals("EXPAND", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseTwoTokenHint(QueryHintType.ExpandViews, "VIEWS");
                }

                // FORCE ORDER / FORCE EXTERNALPUSHDOWN / FORCE SCALEOUTEXECUTION
                if (lexeme.Equals("FORCE", StringComparison.OrdinalIgnoreCase))
                {
                    Token hintToken = Advance();
                    if (Check(TokenType.ORDER))
                    {
                        Token second = Advance();
                        QueryHint hint = new QueryHint(QueryHintType.ForceOrder);
                        hint._hintToken = hintToken;
                        hint._hintToken2 = second;
                        return hint;
                    }
                    return ParseForceOrDisableHint(hintToken, true);
                }

                // DISABLE EXTERNALPUSHDOWN / DISABLE SCALEOUTEXECUTION
                if (lexeme.Equals("DISABLE", StringComparison.OrdinalIgnoreCase))
                {
                    Token hintToken = Advance();
                    return ParseForceOrDisableHint(hintToken, false);
                }

                // KEEP PLAN
                if (lexeme.Equals("KEEP", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseTwoTokenHintWithPlan(QueryHintType.KeepPlan);
                }

                // KEEPFIXED PLAN
                if (lexeme.Equals("KEEPFIXED", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseTwoTokenHintWithPlan(QueryHintType.KeepfixedPlan);
                }

                // ROBUST PLAN
                if (lexeme.Equals("ROBUST", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseTwoTokenHintWithPlan(QueryHintType.RobustPlan);
                }

                // CONCAT UNION
                if (lexeme.Equals("CONCAT", StringComparison.OrdinalIgnoreCase))
                {
                    Token hintToken = Advance();
                    Token second = Consume(TokenType.UNION, "Expected UNION after CONCAT");
                    QueryHint hint = new QueryHint(QueryHintType.ConcatUnion);
                    hint._hintToken = hintToken;
                    hint._hintToken2 = second;
                    return hint;
                }

                // FAST N
                if (lexeme.Equals("FAST", StringComparison.OrdinalIgnoreCase))
                    return ParseValueHint(QueryHintType.Fast);

                // MAXDOP N
                if (lexeme.Equals("MAXDOP", StringComparison.OrdinalIgnoreCase))
                    return ParseValueHint(QueryHintType.Maxdop);

                // MAXRECURSION N
                if (lexeme.Equals("MAXRECURSION", StringComparison.OrdinalIgnoreCase))
                    return ParseValueHint(QueryHintType.Maxrecursion);

                // QUERYTRACEON N
                if (lexeme.Equals("QUERYTRACEON", StringComparison.OrdinalIgnoreCase))
                    return ParseValueHint(QueryHintType.QueryTraceOn);

                // MAX_GRANT_PERCENT = N
                if (lexeme.Equals("MAX_GRANT_PERCENT", StringComparison.OrdinalIgnoreCase))
                    return ParseEqualsValueHint(QueryHintType.MaxGrantPercent);

                // MIN_GRANT_PERCENT = N
                if (lexeme.Equals("MIN_GRANT_PERCENT", StringComparison.OrdinalIgnoreCase))
                    return ParseEqualsValueHint(QueryHintType.MinGrantPercent);

                // LABEL = 'name'
                if (lexeme.Equals("LABEL", StringComparison.OrdinalIgnoreCase))
                    return ParseEqualsValueHint(QueryHintType.Label);

                // PARAMETERIZATION { SIMPLE | FORCED }
                if (lexeme.Equals("PARAMETERIZATION", StringComparison.OrdinalIgnoreCase))
                {
                    Token hintToken = Advance();
                    Token modeToken = ConsumeIdentifierOrContextualKeyword("Expected SIMPLE or FORCED");
                    QueryHint hint = new QueryHint(QueryHintType.Parameterization, modeToken);
                    hint._hintToken = hintToken;
                    return hint;
                }

                // OPTIMIZE FOR UNKNOWN / OPTIMIZE FOR ( @var ... )
                if (lexeme.Equals("OPTIMIZE", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseOptimizeHint();
                }
            }

            throw Error(Peek(), "Expected query hint");
        }

        /// <summary>
        /// Helper: two-token hint where second token is an identifier (e.g., EXPAND VIEWS)
        /// </summary>
        private QueryHint ParseTwoTokenHint(QueryHintType type, string expectedSecond)
        {
            Token hintToken = Advance();
            Token second = ConsumeIdentifierOrContextualKeyword("Expected " + expectedSecond);
            QueryHint hint = new QueryHint(type);
            hint._hintToken = hintToken;
            hint._hintToken2 = second;
            return hint;
        }

        /// <summary>
        /// Helper: two-token hint where second token is PLAN keyword (KEEP PLAN, KEEPFIXED PLAN, ROBUST PLAN)
        /// </summary>
        private QueryHint ParseTwoTokenHintWithPlan(QueryHintType type)
        {
            Token hintToken = Advance();
            Token second = Consume(TokenType.PLAN, "Expected PLAN");
            QueryHint hint = new QueryHint(type);
            hint._hintToken = hintToken;
            hint._hintToken2 = second;
            return hint;
        }

        /// <summary>
        /// Helper: FAST N, MAXDOP N, MAXRECURSION N, QUERYTRACEON N
        /// </summary>
        private QueryHint ParseValueHint(QueryHintType type)
        {
            Token hintToken = Advance();
            Expr value = Expression();
            QueryHint hint = new QueryHint(type, value);
            hint._hintToken = hintToken;
            return hint;
        }

        /// <summary>
        /// Helper: MAX_GRANT_PERCENT = N, MIN_GRANT_PERCENT = N, LABEL = 'name'
        /// </summary>
        private QueryHint ParseEqualsValueHint(QueryHintType type)
        {
            Token hintToken = Advance();
            Token equalsToken = Consume(TokenType.EQUAL, "Expected '='");
            Expr value = Expression();
            QueryHint hint = new QueryHint(type, value);
            hint._hintToken = hintToken;
            hint._equalsToken = equalsToken;
            return hint;
        }

        /// <summary>
        /// FORCE EXTERNALPUSHDOWN / FORCE SCALEOUTEXECUTION
        /// DISABLE EXTERNALPUSHDOWN / DISABLE SCALEOUTEXECUTION
        /// </summary>
        private QueryHint ParseForceOrDisableHint(Token hintToken, bool isForce)
        {
            Token second = ConsumeIdentifierOrContextualKeyword("Expected EXTERNALPUSHDOWN or SCALEOUTEXECUTION");
            QueryHintType type;
            if (second.Lexeme.Equals("EXTERNALPUSHDOWN", StringComparison.OrdinalIgnoreCase))
                type = isForce ? QueryHintType.ForceExternalPushdown : QueryHintType.DisableExternalPushdown;
            else if (second.Lexeme.Equals("SCALEOUTEXECUTION", StringComparison.OrdinalIgnoreCase))
                type = isForce ? QueryHintType.ForceScaleoutExecution : QueryHintType.DisableScaleoutExecution;
            else
                throw Error(second, "Expected EXTERNALPUSHDOWN or SCALEOUTEXECUTION");
            QueryHint hint = new QueryHint(type);
            hint._hintToken = hintToken;
            hint._hintToken2 = second;
            return hint;
        }

        /// <summary>
        /// OPTIMIZE FOR UNKNOWN | OPTIMIZE FOR ( @var { UNKNOWN | = literal } [, ...n] )
        /// </summary>
        private QueryHint ParseOptimizeHint()
        {
            Token optimizeToken = Advance();
            Token forToken = Consume(TokenType.FOR, "Expected FOR after OPTIMIZE");

            if (IsIdentifierWithLexeme("UNKNOWN"))
            {
                Token unknownToken = Advance();
                QueryHint hint = new QueryHint(QueryHintType.OptimizeForUnknown);
                hint._hintToken = optimizeToken;
                hint._hintToken2 = forToken;
                hint._unknownToken = unknownToken;
                return hint;
            }

            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after OPTIMIZE FOR");
            SyntaxElementList<OptimizeForVariable> vars = new SyntaxElementList<OptimizeForVariable>();
            vars.Add(ParseOptimizeForVariable());

            while (Match(TokenType.COMMA, out Token comma))
            {
                vars.Add(ParseOptimizeForVariable(), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')'");

            QueryHint ofHint = new QueryHint(QueryHintType.OptimizeFor, vars);
            ofHint._hintToken = optimizeToken;
            ofHint._hintToken2 = forToken;
            ofHint._leftParen = leftParen;
            ofHint._rightParen = rightParen;
            return ofHint;
        }

        /// <summary>
        /// @variable { UNKNOWN | = literal }
        /// </summary>
        private OptimizeForVariable ParseOptimizeForVariable()
        {
            Token variable = Consume(TokenType.VARIABLE, "Expected @variable");

            if (IsIdentifierWithLexeme("UNKNOWN"))
            {
                Token unknownToken = Advance();
                OptimizeForVariable ofv = new OptimizeForVariable(variable);
                ofv._unknownToken = unknownToken;
                return ofv;
            }

            Token equalsToken = Consume(TokenType.EQUAL, "Expected '=' or UNKNOWN");
            Expr value = Expression();
            OptimizeForVariable ofvLit = new OptimizeForVariable(variable, value);
            ofvLit._equalsToken = equalsToken;
            return ofvLit;
        }

        /// <summary>
        /// USE HINT ( 'hint_name' [, ...n] )
        /// </summary>
        private QueryHint ParseUseHint(Token useToken)
        {
            Token hintToken2 = Advance(); // HINT
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after USE HINT");

            SyntaxElementList<Expr> names = new SyntaxElementList<Expr>();
            names.Add(Expression());

            while (Match(TokenType.COMMA, out Token comma))
            {
                names.Add(Expression(), comma);
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')'");

            QueryHint hint = new QueryHint(QueryHintType.UseHint, names);
            hint._hintToken = useToken;
            hint._hintToken2 = hintToken2;
            hint._leftParen = leftParen;
            hint._rightParen = rightParen;
            return hint;
        }

        /// <summary>
        /// TABLE HINT ( exposed_object_name [, table_hint [, ...n]] )
        /// </summary>
        private QueryHint ParseTableHintQueryHint(Token tableToken)
        {
            Token hintToken2 = Advance(); // HINT
            Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected '(' after TABLE HINT");

            IdentifierPartsBuffer parts = CollectIdentifierParts();
            Expr.ObjectIdentifier objectName = FunctionIdentifier(parts);

            SyntaxElementList<TableHint> tableHints = new SyntaxElementList<TableHint>();
            Token commaAfterObjectName = null;

            if (Match(TokenType.COMMA, out commaAfterObjectName))
            {
                tableHints.Add(ParseTableHint());

                while (Match(TokenType.COMMA, out Token comma))
                {
                    tableHints.Add(ParseTableHint(), comma);
                }
            }

            Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ')'");

            QueryHint hint = new QueryHint(QueryHintType.QueryTableHint, objectName, tableHints);
            hint._hintToken = tableToken;
            hint._hintToken2 = hintToken2;
            hint._leftParen = leftParen;
            hint._rightParen = rightParen;
            hint._commaAfterObjectName = commaAfterObjectName;
            return hint;
        }

        /// <summary>
        /// USE PLAN N'xml_plan'
        /// </summary>
        private QueryHint MakeUsePlanHint(Token useToken, Token planToken, Expr value)
        {
            QueryHint hint = new QueryHint(QueryHintType.UsePlan, value);
            hint._hintToken = useToken;
            hint._hintToken2 = planToken;
            return hint;
        }

        /// <summary>
        /// Checks if the current token is an identifier (or contextual keyword) with a specific lexeme.
        /// </summary>
        private bool IsIdentifierWithLexeme(string lexeme)
        {
            if (IsAtEnd()) return false;
            Token token = Peek();
            return (token.Type == TokenType.IDENTIFIER || ContextualKeywords.Contains(token.Type))
                && token.Lexeme.Equals(lexeme, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Contextual Keyword Helpers

        /// <summary>
        /// Checks if the current token is an identifier or a contextual keyword that can be used as an identifier.
        /// </summary>
        private bool IsIdentifierOrContextualKeyword()
        {
            if (IsAtEnd()) return false;
            TokenType type = Peek().Type;
            return type == TokenType.IDENTIFIER || ContextualKeywords.Contains(type);
        }

        /// <summary>
        /// Checks if the given token is an identifier or a contextual keyword.
        /// </summary>
        private bool IsIdentifierOrContextualKeyword(Token token)
        {
            return token.Type == TokenType.IDENTIFIER || ContextualKeywords.Contains(token.Type);
        }

        /// <summary>
        /// Consumes an identifier or contextual keyword token.
        /// </summary>
        private Token ConsumeIdentifierOrContextualKeyword(string message)
        {
            if (IsIdentifierOrContextualKeyword())
            {
                return Advance();
            }
            throw Error(Peek(), message);
        }

        #endregion

        private (Token token, bool negated) TryConsumeNot()
        {
            if (Match(TokenType.NOT, out Token notToken))
                return (notToken, true);
            return (null, false);
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) { return Advance(); }

            throw Error(Peek(), message);
        }

        private Token Consume(TokenType[] types, string message)
        {
            foreach (TokenType type in types)
            {
                if (Check(type)) { return Advance(); }
            }

            throw Error(Peek(), message);
        }

        private bool Match(TokenType type, out Token token)
        {
            if (Check(type))
            {
                token = Advance();
                return true;
            }

            token = null;
            return false;
        }

        private bool Match(TokenType type1, TokenType type2, out Token token)
        {
            if (Check(type1) || Check(type2))
            {
                token = Advance();
                return true;
            }

            token = null;
            return false;
        }

        private bool Match(TokenType type1, TokenType type2, TokenType type3, out Token token)
        {
            if (Check(type1) || Check(type2) || Check(type3))
            {
                token = Advance();
                return true;
            }

            token = null;
            return false;
        }

        private bool Match(TokenType type1, TokenType type2, TokenType type3, TokenType type4, TokenType type5, out Token token)
        {
            if (Check(type1) || Check(type2) || Check(type3) || Check(type4) || Check(type5))
            {
                token = Advance();
                return true;
            }

            token = null;
            return false;
        }

        // Overloads to avoid params array allocations for common cases
        private bool Match(TokenType type)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
            return false;
        }

        private bool Match(TokenType type1, TokenType type2)
        {
            if (Check(type1) || Check(type2))
            {
                Advance();
                return true;
            }
            return false;
        }

        private bool Match(TokenType type1, TokenType type2, TokenType type3)
        {
            if (Check(type1) || Check(type2) || Check(type3))
            {
                Advance();
                return true;
            }
            return false;
        }

        private Token Advance()
        {
            if (!IsAtEnd())
            {
                _current++;
            }

            return Previous();
        }

        private bool Check(TokenType type)
        {
            if (IsAtEnd())
            {
                return false;
            }
            return Peek().Type == type;
        }

        // Overloads to avoid params array allocations for common cases
        private bool Check(TokenType type1, TokenType type2)
        {
            if (IsAtEnd()) return false;
            var currentType = Peek().Type;
            return currentType == type1 || currentType == type2;
        }

        private bool Check(TokenType type1, TokenType type2, TokenType type3)
        {
            if (IsAtEnd()) return false;
            var currentType = Peek().Type;
            return currentType == type1 || currentType == type2 || currentType == type3;
        }

        private bool CheckNext(TokenType type)
        {
            Token token = PeekNext();
            if (token == null) { return false; }

            return token.Type == type;
        }

        private bool IsAtEnd()
        {
            return Peek().Type == TokenType.EOF;
        }

        private Token Peek()
        {
            return _tokens[_current];
        }

        private Token PeekNext()
        {
            if (_current + 1 >= _tokens.Count)
            {
                return null;
            }

            return _tokens[_current + 1];
        }

        private Token Previous()
        {
            return _tokens[_current - 1];
        }

        private ParseError Error(Token token, string message)
        {
            if (token is SourceToken sourceToken)
            {
                int line = sourceToken.Line;
                int columnStart = sourceToken.StartPosition;
                int columnEnd = sourceToken.EndPosition;
                string where = token.Type == TokenType.EOF ? "at end" : $"at '{sourceToken.Lexeme}', column {columnStart}:{columnEnd}. Token: {sourceToken.Type}";
                return new ParseError($"[line {line}] Error {where}. {message}\nIn: {sourceToken.Source}");
            }
            else
            {
                string where = token.Type == TokenType.EOF ? "at end" : $"at '{token.Lexeme}'";
                return new ParseError($"Error {where}: {message}");
            }
        }


        private QualifiedWildcard QualifiedWildcardIdentifier(IdentifierPartsBuffer parts)
        {
            if (IsPattern_ObjectColumn(parts))
            {
                QualifiedWildcard wildcardIdentifier = new QualifiedWildcard(
                     new ObjectName(parts[0].Token),
                     parts[1].Token
                );
                wildcardIdentifier._objectToStarDot = parts[1].DotBefore;
                return wildcardIdentifier;
            }
            else if (IsPattern_SchemaObjectColumn(parts))
            {
                QualifiedWildcard wildcardIdentifier = new QualifiedWildcard(
                     new SchemaName(parts[0].Token),
                     new ObjectName(parts[1].Token),
                     parts[2].Token
                );
                wildcardIdentifier._schemaToObjectDot = parts[1].DotBefore;
                wildcardIdentifier._objectToStarDot = parts[2].DotBefore;
                return wildcardIdentifier;
            }
            else if (IsPattern_DatabaseSchemaObjectColumn(parts))
            {
                QualifiedWildcard wildcardIdentifier = new QualifiedWildcard(
                    new DatabaseName(parts[0].Token),
                    new SchemaName(parts[1].Token),
                    new ObjectName(parts[2].Token),
                    parts[3].Token
                );
                wildcardIdentifier._databaseToSchemaDot = parts[1].DotBefore;
                wildcardIdentifier._schemaToObjectDot = parts[2].DotBefore;
                wildcardIdentifier._objectToStarDot = parts[3].DotBefore;
                return wildcardIdentifier;
            }
            else if (IsPattern_DatabaseObjectColumn_WithSkippedSchema(parts))
            {
                QualifiedWildcard wildcardIdentifier = new QualifiedWildcard(
                    new DatabaseName(parts[0].Token),
                    new ObjectName(parts[2].Token),
                    parts[3].Token
                );
                wildcardIdentifier._databaseToSchemaDot = parts[1].DotBefore;
                wildcardIdentifier._schemaToObjectDot = parts[2].DotBefore;
                wildcardIdentifier._objectToStarDot = parts[3].DotBefore;
                return wildcardIdentifier;
            }
            else
            {
                throw Error(Peek(), "Invalid wildcard identifier format");
            }
        }

        private ObjectIdentifier FunctionIdentifier(IdentifierPartsBuffer parts)
        {
            if (IsPattern_Object(parts))
            {
                return new ObjectIdentifier(
                    new ObjectName(parts[0].Token)
                );
            }
            else if (IsPattern_SchemaObject(parts))
            {
                ObjectIdentifier objectIdentifier = new ObjectIdentifier(
                    new SchemaName(parts[0].Token),
                    new ObjectName(parts[1].Token)
                );
                objectIdentifier._schemaToObjectDot = parts[1].DotBefore;
                return objectIdentifier;
            }
            else if (IsPattern_DatabaseSchemaObject(parts))
            {
                ObjectIdentifier objectIdentifier = new ObjectIdentifier(
                    new DatabaseName(parts[0].Token),
                    new SchemaName(parts[1].Token),
                    new ObjectName(parts[2].Token)
                );
                objectIdentifier._databaseToSchemaDot = parts[1].DotBefore;
                objectIdentifier._schemaToObjectDot = parts[2].DotBefore;
                return objectIdentifier;
            }
            else if (IsPattern_ServerDatabaseSchemaObject(parts))
            {
                ObjectIdentifier objectIdentifier = new ObjectIdentifier(
                    new ServerName(parts[0].Token),
                    new DatabaseName(parts[1].Token),
                    new SchemaName(parts[2].Token),
                    new ObjectName(parts[3].Token)
                );
                objectIdentifier._serverToDatabaseDot = parts[1].DotBefore;
                objectIdentifier._databaseToSchemaDot = parts[2].DotBefore;
                objectIdentifier._schemaToObjectDot = parts[3].DotBefore;
                return objectIdentifier;
            }
            else
            {
                throw Error(Peek(), "Invalid function identifier format");
            }
        }

        private ColumnIdentifier ColumnIdentifier(IdentifierPartsBuffer parts)
        {
            // Pattern match against all valid formats
            if (IsPattern_Column(parts))
            {
                // Pattern: column
                return new ColumnIdentifier(
                    new ColumnName(parts[0].Token));
            }
            else if (IsPattern_ObjectColumn(parts))
            {
                // Pattern: object.column
                return CreateObjectColumn(parts[0], parts[1]);
            }
            else if (IsPattern_SchemaObjectColumn(parts))
            {
                // Pattern: schema.object.column
                return CreateSchemaObjectColumn(parts[0], parts[1], parts[2]);
            }
            else if (IsPattern_DatabaseSchemaObjectColumn(parts))
            {
                // Pattern: database.schema.object.column
                return CreateDatabaseSchemaObjectColumn(parts[0], parts[1], parts[2], parts[3]);
            }
            else if (IsPattern_DatabaseObjectColumn_WithSkippedSchema(parts))
            {
                // Pattern: database..object.column (schema is skipped)
                return CreateDatabaseObjectColumn(parts[0], (SkippedPart)parts[1], parts[2], parts[3]);
            }
            else
            {
                throw Error(Peek(), "Invalid column identifier format");
            }
        }

        // Creation methods for each pattern
        private ColumnIdentifier CreateObjectColumn(IdentifierPart obj, IdentifierPart col)
        {
            ColumnIdentifier identifier = new ColumnIdentifier(
                new ObjectName(obj.Token),
                new ColumnName(col.Token)
            );
            identifier._objectToColumnDot = col.DotBefore;
            return identifier;
        }

        private ColumnIdentifier CreateSchemaObjectColumn(
            IdentifierPart schema,
            IdentifierPart obj,
            IdentifierPart col)
        {
            ColumnIdentifier identifier = new ColumnIdentifier(
                new SchemaName(schema.Token),
                new ObjectName(obj.Token),
                new ColumnName(col.Token)
            );
            identifier._schemaToObjectDot = obj.DotBefore;
            identifier._objectToColumnDot = col.DotBefore;
            return identifier;
        }

        private ColumnIdentifier CreateDatabaseSchemaObjectColumn(
            IdentifierPart db,
            IdentifierPart schema,
            IdentifierPart obj,
            IdentifierPart col)
        {
            ColumnIdentifier identifier = new ColumnIdentifier(
                new DatabaseName(db.Token),
                new SchemaName(schema.Token),
                new ObjectName(obj.Token),
                new ColumnName(col.Token)
            );
            identifier._databaseToSchemaDot = schema.DotBefore;
            identifier._schemaToObjectDot = obj.DotBefore;
            identifier._objectToColumnDot = col.DotBefore;
            return identifier;
        }

        private ColumnIdentifier CreateDatabaseObjectColumn(
            IdentifierPart db,
            SkippedPart skipped,
            IdentifierPart obj,
            IdentifierPart col)
        {
            ColumnIdentifier identifier = new ColumnIdentifier(
                new DatabaseName(db.Token),
                new ObjectName(obj.Token),
                new ColumnName(col.Token)
            );
            identifier._databaseToSchemaDot = skipped.DotBefore;
            identifier._schemaToObjectDot = obj.DotBefore;
            identifier._objectToColumnDot = col.DotBefore;
            return identifier;
        }

        private bool IsPattern_Object(IdentifierPartsBuffer parts)
        {
            return parts.Count == 1;
        }
        private bool IsPattern_SchemaObject(IdentifierPartsBuffer parts)
        {
            return parts.Count == 2
                && !parts[0].IsSkipped
                && !parts[1].IsSkipped;
        }

        private bool IsPattern_DatabaseSchemaObject(IdentifierPartsBuffer parts)
        {
            return parts.Count == 3
                && !parts[0].IsSkipped
                && !parts[1].IsSkipped
                && !parts[2].IsSkipped;
        }

        private bool IsPattern_ServerDatabaseSchemaObject(IdentifierPartsBuffer parts)
        {
            return parts.Count == 4
                && !parts[0].IsSkipped
                && !parts[1].IsSkipped
                && !parts[2].IsSkipped
                && !parts[3].IsSkipped;
        }

        // Pattern recognition methods - these make the valid patterns explicit
        private bool IsPattern_Column(IdentifierPartsBuffer parts)
        {
            return parts.Count == 1;
        }

        private bool IsPattern_ObjectColumn(IdentifierPartsBuffer parts)
        {
            return parts.Count == 2
                && !parts[0].IsSkipped
                && !parts[1].IsSkipped;
        }

        private bool IsPattern_SchemaObjectColumn(IdentifierPartsBuffer parts)
        {
            return parts.Count == 3
                && !parts[0].IsSkipped
                && !parts[1].IsSkipped
                && !parts[2].IsSkipped;
        }

        private bool IsPattern_DatabaseSchemaObjectColumn(IdentifierPartsBuffer parts)
        {
            return parts.Count == 4
                && !parts[0].IsSkipped
                && !parts[1].IsSkipped
                && !parts[2].IsSkipped
                && !parts[3].IsSkipped;
        }

        private bool IsPattern_DatabaseObjectColumn_WithSkippedSchema(IdentifierPartsBuffer parts)
        {
            return parts.Count == 4
                && !parts[0].IsSkipped
                && parts[1].IsSkipped
                && !parts[2].IsSkipped
                && !parts[3].IsSkipped;
        }


        private IdentifierPartsBuffer CollectIdentifierParts()
        {
            IdentifierPartsBuffer parts = new IdentifierPartsBuffer();

            // Get the first part - can be IDENTIFIER, contextual keyword, or STAR
            Token first = ConsumeIdentifierOrContextualKeywordOrStar("Expected identifier or '*' for column reference");
            parts.Add(new IdentifierPart(first, dotBefore: null));

            if (first.Type == TokenType.STAR) { return parts; }

            // Collect remaining parts separated by dots
            while (Check(TokenType.DOT) && parts.Count < 4)
            {
                Token dot = Advance();

                // Check for double-dot pattern (..)
                if (Check(TokenType.DOT))
                {
                    parts.Add(new SkippedPart(dot));
                    continue;
                }

                Token next = ConsumeIdentifierOrContextualKeywordOrStar("Expected identifier or '*' after dot");
                parts.Add(new IdentifierPart(next, dotBefore: dot));

                if (next.Type == TokenType.STAR) { break; }
            }

            return parts;
        }

        /// <summary>
        /// Consumes an identifier, contextual keyword, or STAR token.
        /// </summary>
        private Token ConsumeIdentifierOrContextualKeywordOrStar(string message)
        {
            if (Check(TokenType.STAR) || IsIdentifierOrContextualKeyword())
            {
                return Advance();
            }
            throw Error(Peek(), message);
        }

        private class IdentifierPart
        {
            public Token Token { get; }
            public Token DotBefore { get; }
            public virtual bool IsSkipped => false;

            public IdentifierPart(Token token, Token dotBefore)
            {
                Token = token;
                DotBefore = dotBefore;
            }
        }

        private class SkippedPart : IdentifierPart
        {
            public override bool IsSkipped => true;

            public SkippedPart(Token dotBefore) : base(null, dotBefore)
            {
            }
        }

        /// <summary>
        /// A fixed-size buffer for identifier parts (max 4: database.schema.object.column).
        /// Avoids heap allocation for the common case.
        /// </summary>
        private struct IdentifierPartsBuffer
        {
            private IdentifierPart _part0;
            private IdentifierPart _part1;
            private IdentifierPart _part2;
            private IdentifierPart _part3;
            private int _count;

            public int Count => _count;

            public IdentifierPart this[int index]
            {
                get
                {
                    switch (index)
                    {
                        case 0: return _part0;
                        case 1: return _part1;
                        case 2: return _part2;
                        case 3: return _part3;
                        default: throw new System.IndexOutOfRangeException();
                    }
                }
            }

            public void Add(IdentifierPart part)
            {
                switch (_count)
                {
                    case 0: _part0 = part; break;
                    case 1: _part1 = part; break;
                    case 2: _part2 = part; break;
                    case 3: _part3 = part; break;
                    default: throw new System.InvalidOperationException("Buffer full");
                }
                _count++;
            }
        }
    }
}
