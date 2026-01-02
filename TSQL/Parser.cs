using System;
using System.Collections.Generic;
using System.Linq;

namespace TSQL
{
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

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        public Stmt Parse()
        {
            // Check if query starts with WITH (CTE)
            if (Check(TokenType.WITH))
            {
                return CteStatement();
            }

            return SelectStatement();
        }

        public Stmt.Select ParseSelect()
        {
            return SelectStatement();
        }

        private Stmt.Cte CteStatement()
        {
            Consume(TokenType.WITH, "Expected WITH");

            Stmt.Cte cteStmt = new Stmt.Cte();

            // Parse CTE definitions
            do
            {
                CteDefinition cte = new CteDefinition();
                cte.Name = Consume(TokenType.IDENTIFIER, "Expected CTE name").Lexeme;

                // Optional column list
                if (Match(TokenType.LEFT_PAREN))
                {
                    // Check if this is the AS clause subquery or column list
                    if (Check(TokenType.SELECT))
                    {
                        // It's the subquery, back up
                        _current--;
                    }
                    else
                    {
                        cte.ColumnNames = new List<string>();
                        do
                        {
                            cte.ColumnNames.Add(Consume(TokenType.IDENTIFIER, "Expected column name").Lexeme);
                        } while (Match(TokenType.COMMA));

                        Consume(TokenType.RIGHT_PAREN, "Expected )");
                    }
                }

                Consume(TokenType.AS, "Expected AS");
                Consume(TokenType.LEFT_PAREN, "Expected (");

                cte.Query = SelectExpression();

                Consume(TokenType.RIGHT_PAREN, "Expected )");

                cteStmt.Ctes.Add(cte);

            } while (Match(TokenType.COMMA));

            // Parse main query
            cteStmt.MainQuery = SelectExpression();

            return cteStmt;
        }


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


            if (Match(TokenType.FROM))
            {
                Token fromToken = Previous();
                selectExpr.From = FromClause();

                while (IsJoinKeyword())
                {
                    selectExpr.Joins.Add(JoinClause());
                }
            }

            if (Match(TokenType.WHERE))
            {
                selectExpr.Where = Expression();
            }

            if (Match(TokenType.GROUP))
            {
                Consume(TokenType.BY, "Expected BY after GROUP");
                do
                {
                    selectExpr.GroupBy.Add(Expression());
                } while (Match(TokenType.COMMA));
            }

            if (Match(TokenType.HAVING))
            {
                selectExpr.Having = Expression();
            }

            if (Match(TokenType.ORDER))
            {
                Consume(TokenType.BY, "Expected BY after ORDER");
                do
                {
                    Expr expr = Expression();
                    bool desc = Match(TokenType.DESC);
                    if (!desc) Match(TokenType.ASC);
                    selectExpr.OrderBy.Add(new OrderByItem() { Expression = expr, Descending = desc });
                } while (Match(TokenType.COMMA));
            }

            return selectExpr;
        }


        private SelectColumn SelectColumn()
        {
            Expr expr = Expression();
            string alias = null;

            if (Match(TokenType.AS))
                alias = Consume(TokenType.IDENTIFIER, "Expected alias").Lexeme;

            return new SelectColumn() { Expression = expr, Alias = alias };
        }


        private FromClause FromClause()
        {
            if (Check(TokenType.LEFT_PAREN))
            {
                Expr.Subquery subquery = Subquery();

                string alias = null;
                if (Match(TokenType.AS))
                    alias = Consume(TokenType.IDENTIFIER, "Expected alias").Lexeme;

                return new FromClause() { TableSource = new SubqueryReference() { Subquery = subquery }, Alias = alias };
            }

            string tableName = Consume(TokenType.IDENTIFIER, "Expected table name").Lexeme;
            string tableAlias = null;

            if (Check(TokenType.IDENTIFIER))
                tableAlias = Advance().Lexeme;

            return new FromClause() { TableSource = new TableReference() { TableName = tableName }, Alias = tableAlias };
        }


        private JoinClause JoinClause()
        {
            JoinClause join = new JoinClause();

            if (Match(TokenType.INNER)) join.JoinType = "INNER";
            else if (Match(TokenType.LEFT)) join.JoinType = "LEFT";
            else if (Match(TokenType.RIGHT)) join.JoinType = "RIGHT";
            else if (Match(TokenType.FULL)) join.JoinType = "FULL";
            else if (Match(TokenType.CROSS)) join.JoinType = "CROSS";

            Match(TokenType.OUTER);
            Consume(TokenType.JOIN, "Expected JOIN");

            if (Check(TokenType.LEFT_PAREN))
            {
                join.TableSource = new SubqueryReference() { Subquery = Subquery() };

                if (Match(TokenType.AS))
                    join.Alias = Consume(TokenType.IDENTIFIER, "Expected alias").Lexeme;
            }
            else
            {
                join.TableSource = new TableReference() { TableName = Consume(TokenType.IDENTIFIER, "Expected table name").Lexeme };
                if (Check(TokenType.IDENTIFIER))
                    join.Alias = Advance().Lexeme;
            }

            if (Match(TokenType.ON))
                join.OnCondition = Expression();

            return join;
        }

        private Expr Expression()
        {
            return Or();
        }

        private Expr Or()
        {
            Expr expr = And();

            while (Match(TokenType.OR))
            {
                Token op = Previous();
                Expr right = And();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expr And()
        {
            Expr expr = Equality();

            while (Match(TokenType.AND))
            {
                Token op = Previous();
                Expr right = Equality();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expr Equality()
        {
            Expr expr = Comparison();

            while (Match(TokenType.EQUAL, TokenType.NOT_EQUAL))
            {
                Token op = Previous();
                Expr right = Comparison();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
        }

        private Expr Comparison()
        {
            Expr expr = Term();

            while (Match(TokenType.GREATER, TokenType.GREATER_EQUAL, TokenType.LESS, TokenType.LESS_EQUAL))
            {
                Token op = Previous();
                Expr right = Term();
                expr = new Expr.Binary() { Left = expr, Operator = op, Right = right };
            }

            return expr;
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
            if (Match(TokenType.WHOLE_NUMBER, TokenType.DECIMAL, TokenType.STRING))
            {
                return new Expr.Literal(Previous());
            }

            if (Check(TokenType.LEFT_PAREN))
            {
                if (CheckNext(TokenType.SELECT))
                {
                    Expr.Subquery subquery = Subquery();
                    return subquery;
                }

                return Grouping();
            }

            if (Match(TokenType.STAR))
            {
                return new Expr.Identifier("*");
            }

            if (Check(TokenType.IDENTIFIER, TokenType.STAR))
            {
                string name = Advance().Lexeme;

                if (Match(TokenType.LEFT_PAREN))
                {
                    Expr.Function func = new Expr.Function() { Name = name };
                    if (!Check(TokenType.RIGHT_PAREN))
                    {
                        do
                        {
                            func.Arguments.Add(Expression());
                        } while (Match(TokenType.COMMA));
                    }
                    Consume(TokenType.RIGHT_PAREN, "Expected )");
                    return func;
                }

                if (Match(TokenType.DOT))
                {
                    // TODO fix: this will break if we have stuff like tablename.* because * is not considered an IDENTIFIER
                    Token column = Consume(new TokenType[] { TokenType.IDENTIFIER, TokenType.STAR }, "Expected column name or '*'");
                    // TODO add name as part of the identifier, in a loop probably
                    return new Expr.Identifier(column);
                }

                return new Expr.Identifier(name);
            }

            throw new Exception($"Unexpected token: {Peek()}");
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
    }
}
