using System;
using System.Collections.Generic;
using System.Linq;

namespace TSQL
{
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

                cte.Query = SelectStatement();

                Consume(TokenType.RIGHT_PAREN, "Expected )");

                cteStmt.Ctes.Add(cte);

            } while (Match(TokenType.COMMA));

            // Parse main query
            cteStmt.MainQuery = SelectStatement();

            return cteStmt;
        }



        private Stmt.Select SelectStatement()
        {
            Consume(TokenType.SELECT, "Expected SELECT");

            Stmt.Select stmt = new Stmt.Select();

            if (Match(TokenType.DISTINCT))
                stmt.Distinct = true;

            if (Match(TokenType.TOP))
            {
                Token topToken = Consume(TokenType.WHOLE_NUMBER, "Expected whole number after TOP");
                stmt.Top = (int)topToken.Literal;
            }

            stmt.Columns = SelectList();

            Consume(TokenType.FROM, "Expected FROM");
            stmt.From = FromClause();

            while (IsJoinKeyword())
                stmt.Joins.Add(JoinClause());

            if (Match(TokenType.WHERE))
                stmt.Where = Expression();

            if (Match(TokenType.GROUP))
            {
                Consume(TokenType.BY, "Expected BY after GROUP");
                stmt.GroupBy = new List<Expr>();
                do
                {
                    stmt.GroupBy.Add(Expression());
                } while (Match(TokenType.COMMA));
            }

            if (Match(TokenType.HAVING))
                stmt.Having = Expression();

            if (Match(TokenType.ORDER))
            {
                Consume(TokenType.BY, "Expected BY after ORDER");
                stmt.OrderBy = new List<OrderByItem>();
                do
                {
                    Expr expr = Expression();
                    bool desc = Match(TokenType.DESC);
                    if (!desc) Match(TokenType.ASC);
                    stmt.OrderBy.Add(new OrderByItem() { Expression = expr, Descending = desc });
                } while (Match(TokenType.COMMA));
            }

            return stmt;
        }


        private List<SelectColumn> SelectList()
        {
            List<SelectColumn> columns = new List<SelectColumn>();
            do
            {
                columns.Add(SelectColumn());
            } while (Match(TokenType.COMMA));
            return columns;
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
                Consume(TokenType.LEFT_PAREN, "Expected (");
                Stmt.Select subquery = SelectStatement();
                Consume(TokenType.RIGHT_PAREN, "Expected )");

                string alias = null;
                if (Match(TokenType.AS))
                    alias = Consume(TokenType.IDENTIFIER, "Expected alias").Lexeme;

                return new FromClause() { Subquery = subquery, Alias = alias };
            }

            string tableName = Consume(TokenType.IDENTIFIER, "Expected table name").Lexeme;
            string tableAlias = null;

            if (Check(TokenType.IDENTIFIER))
                tableAlias = Advance().Lexeme;

            return new FromClause() { TableName = tableName, Alias = tableAlias };
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
                Consume(TokenType.LEFT_PAREN, "Expected (");
                join.Subquery = SelectStatement();
                Consume(TokenType.RIGHT_PAREN, "Expected )");

                if (Match(TokenType.AS))
                    join.Alias = Consume(TokenType.IDENTIFIER, "Expected alias").Lexeme;
            }
            else
            {
                join.TableName = Consume(TokenType.IDENTIFIER, "Expected table name").Lexeme;
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

        private Expr Primary()
        {
            if (Match(TokenType.WHOLE_NUMBER, TokenType.DECIMAL, TokenType.STRING))
                return new Expr.Literal() { Value = Previous().Literal };

            if (Match(TokenType.LEFT_PAREN))
            {
                if (Check(TokenType.SELECT))
                {
                    Stmt.Select subquery = SelectStatement();
                    Consume(TokenType.RIGHT_PAREN, "Expected )");
                    return new Expr.Subquery { Query = subquery };
                }

                Expr expr = Expression();
                Consume(TokenType.RIGHT_PAREN, "Expected )");
                return expr;
            }

            if (Match(TokenType.STAR))
                return new Expr.Column() { ColumnName = "*" };

            if (Check(TokenType.IDENTIFIER))
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
                    string column = Consume(TokenType.IDENTIFIER, "Expected column").Lexeme;
                    return new Expr.Column() { TableAlias = name, ColumnName = column };
                }

                return new Expr.Column() { ColumnName = name };
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


        private bool IsAtEnd()
        {
            return Peek().Type == TokenType.EOF;
        }

        private Token Peek()
        {
            return _tokens[_current];
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
