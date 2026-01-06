using System;
using System.Collections.Generic;
using System.Linq;
using static TSQL.Expr;

namespace TSQL
{
    // TODO: maybe have the CTE statements as part of the SELECT statement? as we do with the other clauses
    /*
    Legend:
    ? -> the group before it can appear zero or one time but not more.
    * -> the group before it can appear zero or more times.
    + -> the group before it can appear one or more times.
    | -> one of the following (OR) e.g. a | b = a OR b
    () -> grouping

    // We have to allow stupid shit through or else it's going to get too complicated imo
    // who cares if we pass something silly to a TOP clause like a string when it only takes numbers?
    // the purpose of the grammar is so there's no ambiguity in token orders and the resulting AST/CST
    // e.g. a b c tokens could be parsed as either (a + b) + c or a + (b + c)

    Grammar:
        Statements:
            Statement -> cte_statement | select_statement
            cte_statement -> "WITH" cte_list select_expression
            select_statement -> select_expression
           
        Expressions:
            TODO: add "column expression" and have a seperate "object expression" for table names etc. figure that out when we get to the from
            TODO: add a function_expression
            TODO: add a variable_expression (starts with @ I think?)

            expression -> term
            term -> factor ( ("-" | "+") factor )*
            factor -> unary ( ( "/" | "*") unary )*
            unary -> ("-") scalar_subquery | scalar_subquery
            scalar_subquery -> ( "(" select_expression ")" ) | primary
            primary -> "NULL" | WHOLE_NUMBER | DECIMAL | STRING | column_expression | ( "(" expression ")" )
        

        Syntax nodes: // not going to be Expr or Stmt classes but rather just helper classes
            column_expression -> (IDENTIFIER ".")? (IDENTIFIER ".")? (IDENTIFIER ".")? IDENTIFIER
            select_expression -> "SELECT" ("DISTINCT")? ("TOP" (WHOLE_NUMBER | parenthesized_expression) ("PERCENT")? ("WITH TIES")? )? select_list (from_clause)? (where_clause)? (group_by_clause)? (having_clause)? (order_by_clause)?
            parenthesized_expression -> ( "(" select_expression | expression ")" ) 

            select_list -> expression ("," expression)*
            from_clause -> "FROM fully_qualified_identifier ("," fully_qualified_identifier)*"
            where_clause -> "WHERE search_condition
            group_by_clause -> "GROUP BY"
            having_clause -> "HAVING"
            order_by_clause -> "ORDER BY"
            cte_list -> cte_definition ( "," cte_definition )*
            cte_definition -> IDENTIFIER ( cte_column_list )? "AS" ( "(" select_expression ")" )
            cte_column_list -> "(" IDENTIFIER ("," IDENTIFIER)* ")"

            comparison_operator = ("=" | "!=" | "<>" | ">" | ">=" | "<" | "<=" | "!>" | "!<" )

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
                comparison | like_comparison | between_predicate | 
                null_predicate | contains_predicate | in_predicate | 
                quantifier_predicate | exists_predicate | "(" predicate ")"
    */

    public class Parser
    {
        private class ParseError : Exception { }

        private readonly List<Token> _tokens;
        private int _current = 0;

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

            return SelectStatement();
        }

        public Stmt.Select ParseSelect()
        {
            Reset();
            return SelectStatement();
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
                SelectColumn selectColumn = SelectColumn();

                // Check if there's a comma after this column
                Token comma = null;
                if (Check(TokenType.COMMA))
                {
                    comma = Advance(); // Capture the comma token
                }

                selectExpr.Columns.Add(selectColumn, comma);
            } while (Previous().Type == TokenType.COMMA); // Continue if we just consumed a comma

            selectExpr.From = FromClause();

            //while (IsJoinKeyword())
            //{
            //    selectExpr.Joins.Add(JoinClause());
            //}

            //if (Match(TokenType.FROM))
            //{
            //    Token fromToken = Previous();
            //    selectExpr.From = FromClause();

            //}

            //if (Match(TokenType.WHERE))
            //{
            //    selectExpr.Where = Expression();
            //}

            //if (Match(TokenType.GROUP))
            //{
            //    Consume(TokenType.BY, "Expected BY after GROUP");
            //    do
            //    {
            //        selectExpr.GroupBy.Add(Expression());
            //    } while (Match(TokenType.COMMA));
            //}

            //if (Match(TokenType.HAVING))
            //{
            //    selectExpr.Having = Expression();
            //}

            //if (Match(TokenType.ORDER))
            //{
            //    Consume(TokenType.BY, "Expected BY after ORDER");
            //    do
            //    {
            //        Expr expr = Expression();
            //        bool desc = Match(TokenType.DESC);
            //        if (!desc) Match(TokenType.ASC);
            //        selectExpr.OrderBy.Add(new OrderByItem() { Expression = expr, Descending = desc });
            //    } while (Match(TokenType.COMMA));
            //}

            return selectExpr;
        }


        private SelectColumn SelectColumn()
        {
            Expr expr = Expression();
            Alias alias = Alias();

            return new SelectColumn(expr, alias);
        }

        private FromClause FromClause()
        {
            if (!Match(TokenType.FROM, out Token fromToken))
            {
                return null;
            }

            if (Check(TokenType.LEFT_PAREN))
            {
                Expr.Subquery subquery = Subquery();

                Alias alias = null;
                if (Match(TokenType.AS, out Token asToken))
                {
                    alias = new Alias(Consume(TokenType.IDENTIFIER, "Expected alias"));
                    alias._asKeyword = asToken;
                }
                else if (Match(TokenType.IDENTIFIER, out Token aliasToken))
                {
                    alias = new Alias(aliasToken);
                    alias._asKeyword = ConcreteToken.Empty;
                }

                return new FromClause(fromToken) { TableSource = new SubqueryReference() { Subquery = subquery }, Alias = alias };
            }
            else
            {
                Token tableName = Consume(TokenType.IDENTIFIER, "Expected table name");
                Alias tableAlias = Alias();

                return new FromClause(fromToken)
                {
                    TableSource = new TableReference()
                    {
                        TableName = tableName
                    },
                    Alias = tableAlias
                };
            }
        }

        private Alias Alias()
        {
            if (Match(TokenType.AS, out Token asToken))
            {
                Alias alias = new Alias(Consume(TokenType.IDENTIFIER, "Expected alias"));
                alias._asKeyword = asToken;

                return alias;
            }
            else if (Match(TokenType.IDENTIFIER, out Token aliasToken))
            {
                Alias alias = new Alias(aliasToken);
                alias._asKeyword = ConcreteToken.Empty;
                return alias;
            }

            return null;
        }


        //private JoinClause JoinClause()
        //{
        //    JoinClause join = new JoinClause();

        //    if (Match(TokenType.INNER)) join.JoinType = "INNER";
        //    else if (Match(TokenType.LEFT)) join.JoinType = "LEFT";
        //    else if (Match(TokenType.RIGHT)) join.JoinType = "RIGHT";
        //    else if (Match(TokenType.FULL)) join.JoinType = "FULL";
        //    else if (Match(TokenType.CROSS)) join.JoinType = "CROSS";

        //    Match(TokenType.OUTER);
        //    Consume(TokenType.JOIN, "Expected JOIN");

        //    if (Check(TokenType.LEFT_PAREN))
        //    {
        //        join.TableSource = new SubqueryReference() { Subquery = Subquery() };

        //        if (Match(TokenType.AS))
        //        {
        //            join.Alias = Consume(TokenType.IDENTIFIER, "Expected alias").Lexeme;
        //        }
        //    }
        //    else
        //    {
        //        join.TableSource = new TableReference() { TableName = Consume(TokenType.IDENTIFIER, "Expected table name").Lexeme };
        //        if (Check(TokenType.IDENTIFIER))
        //            join.Alias = Advance().Lexeme;
        //    }

        //    if (Match(TokenType.ON))
        //        join.OnCondition = Expression();

        //    return join;
        //}

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
            Expr expr = Primary();

            while (Match(TokenType.STAR, TokenType.SLASH))
            {
                Token op = Previous();
                Expr right = Primary();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
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
            if (Match(new TokenType[] { TokenType.WHOLE_NUMBER, TokenType.DECIMAL, TokenType.STRING }, out Token literalToken))
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

            if (Check(TokenType.IDENTIFIER, TokenType.STAR))
            {
                // Collect all the parts separated by dots
                List<IdentifierPart> parts = CollectIdentifierParts();

                if (Check(TokenType.LEFT_PAREN))
                {
                    if (Previous().Type == TokenType.STAR)
                    {
                        Error(Peek(), "Can't call '*'");
                    }

                    ObjectIdentifier functionIdentifier = FunctionIdentifier(parts);
                    return FinishCall(functionIdentifier);
                }
                else
                {
                    return ColumnIdentifier(parts);
                }
            }

            throw new Exception($"Unexpected token: {Peek()}");
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

        private bool Match(TokenType[] types, out Token token)
        {
            foreach (TokenType type in types)
            {
                if (Check(type))
                {
                    token = Advance();
                    return true;
                }
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

        private bool Match(params TokenType[] types)
        {
            foreach (TokenType type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
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
            return new ParseError();
        }


        private ObjectIdentifier FunctionIdentifier(List<IdentifierPart> parts)
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

        private ColumnIdentifier ColumnIdentifier(List<IdentifierPart> parts)
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
            var identifier = new ColumnIdentifier(
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
            var identifier = new ColumnIdentifier(
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
            var identifier = new ColumnIdentifier(
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
            var identifier = new ColumnIdentifier(
                new DatabaseName(db.Token),
                new ObjectName(obj.Token),
                new ColumnName(col.Token)
            );
            // The double-dot pattern uses the first dot for database-to-schema
            // and we skip over the second dot of the ".."
            identifier._databaseToSchemaDot = skipped.DotBefore;
            identifier._objectToColumnDot = col.DotBefore;
            return identifier;
        }

        private bool IsPattern_Object(List<IdentifierPart> parts)
        {
            return parts.Count == 1;
        }
        private bool IsPattern_SchemaObject(List<IdentifierPart> parts)
        {
            return parts.Count == 2
                && parts[0] is IdentifierPart
                && parts[1] is IdentifierPart;
        }

        private bool IsPattern_DatabaseSchemaObject(List<IdentifierPart> parts)
        {
            return parts.Count == 3
                && parts[0] is IdentifierPart
                && parts[1] is IdentifierPart
                && parts[2] is IdentifierPart;
        }

        private bool IsPattern_ServerDatabaseSchemaObject(List<IdentifierPart> parts)
        {
            return parts.Count == 4
                && parts[0] is IdentifierPart
                && parts[1] is IdentifierPart
                && parts[2] is IdentifierPart
                && parts[3] is IdentifierPart;
        }

        // Pattern recognition methods - these make the valid patterns explicit
        private bool IsPattern_Column(List<IdentifierPart> parts)
        {
            return parts.Count == 1;
        }

        private bool IsPattern_ObjectColumn(List<IdentifierPart> parts)
        {
            return parts.Count == 2
                && parts[0] is IdentifierPart
                && parts[1] is IdentifierPart;
        }



        private bool IsPattern_SchemaObjectColumn(List<IdentifierPart> parts)
        {
            return parts.Count == 3
                && parts[0] is IdentifierPart
                && parts[1] is IdentifierPart
                && parts[2] is IdentifierPart;
        }

        private bool IsPattern_DatabaseSchemaObjectColumn(List<IdentifierPart> parts)
        {
            return parts.Count == 4
                && parts[0] is IdentifierPart
                && parts[1] is IdentifierPart
                && parts[2] is IdentifierPart
                && parts[3] is IdentifierPart;
        }

        private bool IsPattern_DatabaseObjectColumn_WithSkippedSchema(List<IdentifierPart> parts)
        {
            return parts.Count == 4
                && parts[0] is IdentifierPart
                && parts[1] is SkippedPart
                && parts[2] is IdentifierPart
                && parts[3] is IdentifierPart;
        }


        private List<IdentifierPart> CollectIdentifierParts()
        {
            var parts = new List<IdentifierPart>();

            // Get the first part
            Token first = Consume(new TokenType[] { TokenType.IDENTIFIER, TokenType.STAR }, "Expected identifier or '*' for column reference");
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

                Token next = Consume(new TokenType[] { TokenType.IDENTIFIER, TokenType.STAR }, "Expected identifier or '*' after dot");
                parts.Add(new IdentifierPart(next, dotBefore: dot));

                if (next.Type == TokenType.STAR) { break; }
            }

            return parts;
        }

        private class IdentifierPart
        {
            public Token Token { get; }
            public Token DotBefore { get; }

            public IdentifierPart(Token token, Token dotBefore)
            {
                Token = token;
                DotBefore = dotBefore;
            }
        }

        private class SkippedPart : IdentifierPart
        {
            public SkippedPart(Token dotBefore) : base(null, dotBefore)
            {
            }
        }
    }
}
