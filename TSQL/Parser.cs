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

    // TODO: support CAST(exp AS data_type) SYNTAX. special case? or support it everywhere?

    // dollar sign ($) columns need no unique handling, column identifier already handles them just fine
    /*
    Legend:
    ? -> the group before it can appear zero or one time but not more.
    * -> the group before it can appear zero or more times.
    + -> the group before it can appear one or more times.
    | -> one of the following (OR) e.g. a | b = a OR b
    () -> grouping

    // TODO: think if we should make TOP NOT support NULL / DECIMAL / STRING
    // maybe we can do that by parsing the Expression 
    // and check the previous Token and if it was NULL / DECIMAL / STRING to throw an error

    Grammar:
        Statements:
            Statement -> cte_statement | select_statement
            cte_statement -> "WITH" cte_list select_expression
            select_statement -> select_expression
           
        Expressions:
            expression -> term
            term -> factor ( ("-" | "+") factor )*
            factor -> unary ( ( "/" | "*") unary )*
            unary -> ("-") scalar_subquery | scalar_subquery
            scalar_subquery -> ( "(" select_expression ")" ) | primary
            primary -> 
                "NULL" | WHOLE_NUMBER | DECIMAL | STRING | VARIABLE
                | column_expression | ( "(" expression ")" ) 
                | scalar_function | window_function
        

        Syntax nodes:
            column_expression -> fully_qualified_identifier
            fully_qualified_identifier -> (IDENTIFIER ".")? (IDENTIFIER ".")? (IDENTIFIER ".")? IDENTIFIER

            scalar_function -> function_call
            function_call -> fully_qualified_identifier "(" (expression_list)? ")" 
            expression_list -> expression ("," expression)*

            select_expression -> "SELECT" ("DISTINCT")? ("TOP" (WHOLE_NUMBER | parenthesized_expression) ("PERCENT")? ("WITH TIES")? )? select_list (from_clause)? (where_clause)? (group_by_clause)? (having_clause)? (order_by_clause)?
            parenthesized_expression -> ( "(" select_expression | expression ")" ) 
            wildcard -> STAR
            qualified_wildcard -> (IDENTIFIER ".")? (IDENTIFIER ".")? (IDENTIFIER ".") STAR
            select_item -> wildcard | qualified_wildcard | expression (("AS")? IDENTIFIER)?
            select_list -> select_item ("," select_item)*

            where_clause -> "WHERE search_condition
            group_by_clause -> "GROUP BY"
            having_clause -> "HAVING"
            order_by_clause -> "ORDER BY"
            cte_list -> cte_definition ( "," cte_definition )*
            cte_definition -> IDENTIFIER ( cte_column_list )? "AS" ( "(" select_expression ")" )
            cte_column_list -> "(" IDENTIFIER ("," IDENTIFIER)* ")"

            comparison_operator = ("=" | "!=" | "<>" | ">" | ">=" | "<" | "<=" | "!>" | "!<" )

            ---------------- WHERE ---------------
            comparison_predicate -> expression comparison_operator expression 
            like_predicate -> expression ("NOT")? "LIKE" expression ("ESCAPE" STRING)?
            between_predicate -> expression ("NOT")? "BETWEEN" expression "AND" expression
            null_predicate -> expression IS ("NOT")? "NULL"
            contains_predicate -> "CONTAINS" "(" ( fully_qualified_identifier | "*" ) , STRING ")"
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
                null_predicate | contains_predicate | in_predicate | 
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
            values_table_source -> "VALUES" values_row ("," values_row)* (("AS")? IDENTIFIER)? ( "(" IDENTIFIER ("," IDENTIFIER)* ")" )?
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
        private class ParseError : Exception
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
            TokenType.REPEATABLE
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
            // Check if query starts with WITH (CTE)
            //if (Check(TokenType.WITH))
            //{
            //    return CteStatement();
            //}

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

        //private Stmt.Cte CteStatement()
        //{
        //    Consume(TokenType.WITH, "Expected WITH");

        //    Stmt.Cte cteStmt = new Stmt.Cte();

        //    // Parse CTE definitions
        //    do
        //    {
        //        CteDefinition cte = new CteDefinition();
        //        cte.Name = Consume(TokenType.IDENTIFIER, "Expected CTE name").Lexeme;

        //        // Optional column list
        //        if (Match(TokenType.LEFT_PAREN))
        //        {
        //            // Check if this is the AS clause subquery or column list
        //            if (Check(TokenType.SELECT))
        //            {
        //                // It's the subquery, back up
        //                _current--;
        //            }
        //            else
        //            {
        //                cte.ColumnNames = new List<string>();
        //                do
        //                {
        //                    cte.ColumnNames.Add(Consume(TokenType.IDENTIFIER, "Expected column name").Lexeme);
        //                } while (Match(TokenType.COMMA));

        //                Consume(TokenType.RIGHT_PAREN, "Expected )");
        //            }
        //        }

        //        Consume(TokenType.AS, "Expected AS");
        //        Consume(TokenType.LEFT_PAREN, "Expected (");

        //        cte.Query = SelectExpression();

        //        Consume(TokenType.RIGHT_PAREN, "Expected )");

        //        cteStmt.Ctes.Add(cte);

        //    } while (Match(TokenType.COMMA));

        //    // Parse main query
        //    cteStmt.MainQuery = SelectExpression();

        //    return cteStmt;
        //}


        private Stmt.Select SelectStatement()
        {
            return new Stmt.Select(SelectExpression());
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

            if (Match(TokenType.TOP))
            {
                Expr expr = Expression();
                selectExpr.Top = new TopClause(expr);
            }

            do
            {
                SelectItem selectColumn = SelectItem();

                // Check if there's a comma after this column
                Token comma = null;
                if (Check(TokenType.COMMA))
                {
                    comma = Advance(); // Capture the comma token
                }

                selectExpr.Columns.Add(selectColumn, comma);
            } while (Previous().Type == TokenType.COMMA); // Continue if we just consumed a comma

            selectExpr.From = FromClause();

            if (Match(TokenType.WHERE, out Token whereToken))
            {
                selectExpr._whereKeyword = whereToken;
                selectExpr.Where = SearchCondition();
            }

            return selectExpr;
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

            while (IsJoinStart())
            {
                source = ParseJoinSuffix(source);
            }

            return source;
        }

        private bool IsJoinStart()
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

            // Parenthesized table source: (T1 JOIN T2 ON ...)
            if (Check(TokenType.LEFT_PAREN))
            {
                return ParseParenthesizedTableSource();
            }

            if (Check(TokenType.VARIABLE))
            {
                return ParseTableVariable();
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

            return subqueryRef;
        }

        private TableVariableReference ParseTableVariable()
        {
            Token variable = Consume(TokenType.VARIABLE, "Expected table variable");
            TableVariableReference varRef = new TableVariableReference(variable);
            varRef.Alias = Alias();

            return varRef;
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

            // CONTAINS (column, search_condition)
            if (Match(TokenType.CONTAINS, out Token containsToken))
            {
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected ( after CONTAINS");
                Expr column;
                if (Check(TokenType.STAR))
                {
                    column = new Expr.Wildcard(Advance());
                }
                else
                {
                    column = Expression();
                }
                Token comma = Consume(TokenType.COMMA, "Expected , in CONTAINS");
                Expr searchExpr = Expression();
                Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ) after CONTAINS");

                Predicate.Contains contains = new Predicate.Contains(column, searchExpr);
                contains._containsToken = containsToken;
                contains._leftParen = leftParen;
                contains._comma = comma;
                contains._rightParen = rightParen;
                return contains;
            }

            // Grouped predicate: (predicate)
            // Must disambiguate from subquery expressions
            if (Check(TokenType.LEFT_PAREN) && !CheckNext(TokenType.SELECT))
            {
                // Could be a grouped predicate or an expression starting with (
                // Try parsing as predicate group - if the content after ( starts with
                // something that looks like a predicate, treat it as grouped predicate
                // This is tricky because (a + b) could be an expression or a predicate start
                // We only treat it as a grouped predicate at the search_condition level
                // when we know we're already in a predicate context
                // Fall through to expression-based predicate parsing below
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
                Token notToken = null;
                bool negated = false;
                if (Match(TokenType.NOT, out notToken))
                {
                    negated = true;
                }
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
                Token notToken = null;
                bool negated = false;
                if (Match(TokenType.NOT, out notToken))
                {
                    negated = true;
                }
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
                Token notToken = null;
                bool negated = false;
                if (Match(TokenType.NOT, out notToken))
                {
                    negated = true;
                }
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
                Token notToken = null;
                bool negated = false;
                if (Match(TokenType.NOT, out notToken))
                {
                    negated = true;
                }
                Token inToken = Consume(TokenType.IN, "Expected IN");
                Token leftParen = Consume(TokenType.LEFT_PAREN, "Expected ( after IN");

                Predicate.In inPred;
                if (Check(TokenType.SELECT))
                {
                    SelectExpression subSelect = SelectExpression();
                    Token rightParen = Consume(TokenType.RIGHT_PAREN, "Expected ) after IN subquery");
                    Expr.Subquery sub = new Expr.Subquery(subSelect, leftParen, rightParen);
                    inPred = new Predicate.In(leftExpr, negated, sub);
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

            while (Match(TokenType.PLUS, TokenType.MINUS))
            {
                Token op = Previous();
                Expr right = Factor();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expr Factor()
        {
            Expr expr = Unary();

            while (Match(TokenType.STAR, TokenType.SLASH, out Token op))
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
                return new Expr.Unary(minus, Primary());
            }
            else
            {
                return Primary();
            }
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

            if (Check(TokenType.LEFT_PAREN))
            {
                if (CheckNext(TokenType.SELECT))
                {
                    return Subquery();
                }

                return Grouping();
            }

            // Handle COALESCE keyword - uses standard function call syntax with optional OVER
            if (Match(TokenType.COALESCE, out Token coalesceToken))
            {
                ObjectIdentifier callee = new ObjectIdentifier(new ObjectName(coalesceToken));
                return FinishCall(callee);
            }

            // Handle OPENXML keyword - has optional WITH clause
            if (Match(TokenType.OPENXML, out Token openXmlToken))
            {
                return FinishOpenXml(openXmlToken);
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
                do
                {
                    Expr expr = Expression();

                    // Check if there's a comma after this argument
                    Token comma = null;
                    if (Check(TokenType.COMMA))
                    {
                        comma = Advance(); // capture the comma token
                    }

                    arguments.Add(expr, comma);
                } while (Previous().Type == TokenType.COMMA);
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
            do
            {
                Expr expr = Expression();
                Token comma = Check(TokenType.COMMA) ? Advance() : null;
                list.Add(expr, comma);
            } while (Previous().Type == TokenType.COMMA);
            return list;
        }

        /// <summary>
        /// Parses a comma-separated list of ORDER BY items
        /// </summary>
        private SyntaxElementList<OrderByItem> ParseOrderByList()
        {
            SyntaxElementList<OrderByItem> list = new SyntaxElementList<OrderByItem>();
            do
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

                Token comma = Check(TokenType.COMMA) ? Advance() : null;
                list.Add(item, comma);
            } while (Previous().Type == TokenType.COMMA);
            return list;
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

        private bool IsJoinKeyword()
        {
            return Check(TokenType.INNER, TokenType.LEFT, TokenType.RIGHT,
                         TokenType.FULL, TokenType.CROSS, TokenType.JOIN);
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

        private bool Check(params TokenType[] types)
        {
            if (IsAtEnd()) return false;
            return types.Contains(Peek().Type);
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
