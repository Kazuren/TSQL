//using TSQL.AST;
//using TSQL.AST.Visitors;

//namespace TSQL.Tests
//{
//    public class AstPrettyPrinterTests
//    {

//        [Fact]
//        public void Print_PrintsCorrectly()
//        {
//            Expr expr = new Expr.Query()
//            {
//                SelectMode = Expr.Query.SelectModeEnum.None,
//                SelectList = new List<Expr>()
//                {
//                    new Expr.Constant(5),
//                    new Expr.Constant(""),
//                    new Expr.TablePrefix(
//                        "Table1",
//                        new Expr.Star()
//                    ),
//                    new Expr.TablePrefix(
//                        "Table1",
//                        new Expr.Column(
//                            "Column1"
//                        )
//                    ),
//                    new Expr.Alias(
//                        "Column2",
//                        new Expr.Column(
//                            "Column1"
//                        )
//                    ),
//                    new Expr.Alias(
//                        "Column4",
//                         new Expr.TablePrefix(
//                            "Table1",
//                            new Expr.Column(
//                                "Column1"
//                            )
//                        )
//                    ),
//                    new Expr.Column(
//                        "Column3"
//                    )
//                },
//                Top = new Expr.Top()
//                {
//                    Expression = new Expr.Constant(5)
//                }
//            };


//            //expr.Transform<Expr.Query>(e => e.depth == 1, e => e.SelectList.Add(new Expr.Column("Abc")));
//            //expr.Transform<Expr.Query>(e => e is Expr.Query, q => q.SelectList.Add(new Expr.Column("Abc"));
//            //expr.AddColumn(new Expr.Column("Abc"));


//            AstPrettyPrinter printer = new AstPrettyPrinter();

//            string result = printer.Print(expr);

//            Assert.Equal(
//                "SELECT TOP (5) 5, '', Table1.*, Table1.Column1, Column1 AS Column2, Table1.Column1 AS Column4, Column3",
//                result
//            );
//        }
//    }
//}
