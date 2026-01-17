using System.Collections.Generic;
using System.Linq.Expressions;
using PdfRender.PostScript.Tokens;

namespace PdfRender.PostScript.Compiler
{
    public sealed partial class PostScriptExpressionCompiler
    {
        private static bool TryBuildOperatorFlow(string name, Stack<Expression> stack, ParameterExpression argsParam, IReadOnlyList<string> parameterNames)
        {
            switch (name)
            {
                case "if":
                {
                    if (stack.Count < 2) { return false; }
                    var procExpr = stack.Pop();
                    var condExpr = stack.Pop();
                    if (procExpr is ConstantExpression ce && ce.Value is PostScriptProcedure proc)
                    {
                        if (stack.Count < 1) { return false; }
                        var oldTop = stack.Pop();
                        if (!TryCompileBlockStack(proc, argsParam, parameterNames, out var blockStack))
                        {
                            return false;
                        }
                        if (blockStack.Count < 1) { return false; }
                        var blockResultTop = EnsureFloat(blockStack.Pop());
                        Expression conditionBool = condExpr.Type == typeof(bool) ? condExpr : Expression.NotEqual(EnsureFloat(condExpr), Expression.Constant(0f));
                        var conditionalTop = Expression.Condition(conditionBool, EnsureFloat(blockResultTop), EnsureFloat(oldTop));
                        stack.Push(conditionalTop);
                        return true;
                    }
                    return false;
                }
                case "ifelse":
                {
                    if (stack.Count < 3) { return false; }
                    var falseProcExpr = stack.Pop();
                    var trueProcExpr = stack.Pop();
                    var condExpr = stack.Pop();
                    if (falseProcExpr is ConstantExpression cf && cf.Value is PostScriptProcedure falseProc &&
                        trueProcExpr is ConstantExpression ct && ct.Value is PostScriptProcedure trueProc)
                    {
                        if (!TryCompileBlockStack(trueProc, argsParam, parameterNames, out var trueStack))
                        {
                            return false;
                        }
                        if (!TryCompileBlockStack(falseProc, argsParam, parameterNames, out var falseStack))
                        {
                            return false;
                        }
                        if (trueStack.Count < 1 || falseStack.Count < 1) { return false; }
                        var trueTop = EnsureFloat(trueStack.Pop());
                        var falseTop = EnsureFloat(falseStack.Pop());
                        Expression conditionBool = condExpr.Type == typeof(bool) ? condExpr : Expression.NotEqual(EnsureFloat(condExpr), Expression.Constant(0f));
                        var ifElse = Expression.Condition(conditionBool, EnsureFloat(trueTop), EnsureFloat(falseTop));
                        stack.Push(ifElse);
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        private static bool TryCompileBlockStack(PostScriptProcedure proc, ParameterExpression argsParam, IReadOnlyList<string> parameterNames, out Stack<Expression> resultStack)
        {
            resultStack = new Stack<Expression>();

            for (int i = 0; i < parameterNames.Count; i++)
            {
                Expression indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                resultStack.Push(indexExpr);
            }

            foreach (var token in proc.Tokens)
            {
                if (token is PostScriptNumber n)
                {
                    resultStack.Push(Expression.Constant(n.Value, typeof(float)));
                    continue;
                }
                if (token is PostScriptLiteralName ln)
                {
                    int index = IndexOf(parameterNames, ln.Name);
                    if (index < 0)
                    {
                        return false;
                    }
                    Expression indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(index));
                    resultStack.Push(indexExpr);
                    continue;
                }
                if (token is PostScriptProcedure innerProc)
                {
                    // Push procedure block marker; consumed by flow-control operators (e.g., if/ifelse)
                    resultStack.Push(Expression.Constant(innerProc, typeof(PostScriptProcedure)));
                    continue;
                }
                if (token is PostScriptExecutableName ex)
                {
                    if (!TryBuildOperator(ex.Name, resultStack, argsParam, parameterNames))
                    {
                        return false;
                    }
                    continue;
                }
                return false;
            }

            return true;
        }
    }
}
