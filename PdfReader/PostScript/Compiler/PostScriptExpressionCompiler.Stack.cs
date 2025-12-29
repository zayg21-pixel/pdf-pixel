using System.Collections.Generic;
using System.Linq.Expressions;

namespace PdfReader.PostScript.Compiler
{
    public sealed partial class PostScriptExpressionCompiler
    {
        private static bool TryBuildOperatorStack(string name, Stack<Expression> stack)
        {
            switch (name)
            {
                case "true":
                {
                    stack.Push(Expression.Constant(true));
                    return true;
                }
                case "false":
                {
                    stack.Push(Expression.Constant(false));
                    return true;
                }
                case "dup":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    stack.Push(x);
                    stack.Push(x);
                    return true;
                }
                case "exch":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(b);
                    stack.Push(a);
                    return true;
                }
                case "pop":
                {
                    if (stack.Count < 1) { return false; }
                    stack.Pop();
                    return true;
                }
                case "copy":
                {
                    if (!TryPopConstInt(stack, out int n)) { return false; }
                    if (n < 0 || stack.Count < n) { return false; }
                    var buffer = new List<Expression>(n);
                    for (int i = 0; i < n; i++) { buffer.Add(stack.Pop()); }
                    buffer.Reverse();
                    for (int i = 0; i < buffer.Count; i++) { stack.Push(buffer[i]); }
                    for (int i = 0; i < buffer.Count; i++) { stack.Push(buffer[i]); }
                    return true;
                }
                case "roll":
                {
                    if (!TryPopConstInt(stack, out int j)) { return false; }
                    if (!TryPopConstInt(stack, out int n)) { return false; }
                    if (n <= 0 || stack.Count < n) { return false; }
                    int normalized = ((j % n) + n) % n;
                    var buffer = new Expression[n];
                    for (int i = n - 1; i >= 0; i--) { buffer[i] = stack.Pop(); }
                    for (int i = 0; i < n; i++)
                    {
                        int sourceIndex = (i + n - normalized) % n;
                        stack.Push(buffer[sourceIndex]);
                    }
                    return true;
                }
                case "index":
                {
                    if (!TryPopConstInt(stack, out int n)) { return false; }
                    if (n < 0 || stack.Count <= n) { return false; }
                    Expression[] arr = stack.ToArray();
                    Expression target = arr[n];
                    stack.Push(target);
                    return true;
                }
            }

            return false;
        }

        private static bool TryPopUnary(Stack<Expression> stack, out Expression x)
        {
            x = null;
            if (stack.Count < 1) { return false; }
            x = stack.Pop();
            return true;
        }

        private static bool TryPopBinary(Stack<Expression> stack, out Expression a, out Expression b)
        {
            a = null; b = null;
            if (stack.Count < 2) { return false; }
            b = stack.Pop();
            a = stack.Pop();
            return true;
        }

        private static bool TryPopConstInt(Stack<Expression> stack, out int value)
        {
            value = 0;
            if (stack.Count < 1) { return false; }
            var expr = stack.Pop();
            if (expr is ConstantExpression c && c.Type == typeof(float))
            {
                value = (int)(float)c.Value;
                return true;
            }
            return false;
        }
    }
}
