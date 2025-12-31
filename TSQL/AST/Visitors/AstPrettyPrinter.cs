namespace TSQL
{
    public interface IAstPrettyPrinter
    {
        string Print(Expr expr);
    }

    //public class AstPrettyPrinter : Expr.Visitor<StringBuilder>, IAstPrettyPrinter
    //{
    //    private readonly StringBuilder _builder;

    //    public AstPrettyPrinter()
    //    {
    //        _builder = new StringBuilder();
    //    }

    //    public string Print(Expr expr)
    //    {
    //        _builder.Clear();
    //        expr.Accept(this);
    //        return _builder.ToString();
    //    }

    //    //public StringBuilder VisitAliasPrefix(Expr.Alias expr)
    //    //{
    //    //    expr.Expr.Accept(this);
    //    //    _builder.Append(" AS ");
    //    //    _builder.Append(expr.Token.Lexeme);
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitBinaryExpr(Expr.Binary expr)
    //    //{
    //    //    expr.Left.Accept(this);
    //    //    _builder.Append(' ');
    //    //    _builder.Append(expr.Op.Lexeme);
    //    //    _builder.Append(' ');
    //    //    expr.Right.Accept(this);
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitColumnExpr(Expr.Column expr)
    //    //{
    //    //    _builder.Append(expr.Token.Lexeme);
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitCommonTableExpr(Expr.CommonTable expr)
    //    //{
    //    //    throw new System.NotImplementedException();
    //    //}

    //    //public StringBuilder VisitConstantExpr(Expr.Constant expr)
    //    //{
    //    //    if (expr.Value == null)
    //    //    {
    //    //        _builder.Append("NULL");
    //    //    }
    //    //    else if (expr.Value is string s)
    //    //    {
    //    //        _builder.Append('\'');
    //    //        _builder.Append(s);
    //    //        _builder.Append('\'');
    //    //    }
    //    //    else
    //    //    {
    //    //        _builder.Append(expr.Value.ToString());
    //    //    }
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitGroupingExpr(Expr.Grouping expr)
    //    //{
    //    //    _builder.Append('(');
    //    //    expr.Accept(this);
    //    //    _builder.Append(')');
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitQueryExpr(Expr.Query expr)
    //    //{
    //    //    _builder.Append("SELECT");

    //    //    switch (expr.SelectMode)
    //    //    {
    //    //        case Expr.Query.SelectModeEnum.All:
    //    //            _builder.Append(" ALL");
    //    //            break;
    //    //        case Expr.Query.SelectModeEnum.Distinct:
    //    //            _builder.Append(" DISTINCT");
    //    //            break;
    //    //        case Expr.Query.SelectModeEnum.None:
    //    //        default:
    //    //            break;
    //    //    }

    //    //    if (expr.Top != null)
    //    //    {
    //    //        _builder.Append(' ');
    //    //        expr.Top.Accept(this);
    //    //    }

    //    //    _builder.Append(' ');
    //    //    bool first = true;
    //    //    foreach (Expr item in expr.SelectList)
    //    //    {
    //    //        if (!first) _builder.Append(", ");
    //    //        item.Accept(this);
    //    //        first = false;
    //    //    }
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitStarExpr(Expr.Star expr)
    //    //{
    //    //    _builder.Append('*');
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitTablePrefix(Expr.TablePrefix expr)
    //    //{
    //    //    _builder.Append(expr.Token.Lexeme);
    //    //    _builder.Append('.');
    //    //    expr.Right.Accept(this);
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitTopExpr(Expr.Top expr)
    //    //{
    //    //    _builder.Append("TOP (");
    //    //    expr.Expression.Accept(this);
    //    //    _builder.Append(')');
    //    //    if (expr.Percent)
    //    //    {
    //    //        _builder.Append(" PERCENT");
    //    //    }
    //    //    if (expr.WithTies)
    //    //    {
    //    //        _builder.Append(" WITH TIES");
    //    //    }
    //    //    return _builder;
    //    //}


    //    //public StringBuilder VisitUnaryExpr(Expr.Unary expr)
    //    //{
    //    //    _builder.Append(expr.Op.Lexeme);
    //    //    expr.Right.Accept(this);
    //    //    return _builder;
    //    //}

    //    //public StringBuilder VisitVariableExpr(Expr.Variable expr)
    //    //{
    //    //    _builder.Append(expr.Name.Lexeme);
    //    //    return _builder;
    //    //}
    //}
}
