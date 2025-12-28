using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using PdfReader.PostScript.Tokens;

namespace PdfReader.PostScript
{
    // TODO: [HIGH] cleanup and add missing operators
    /// <summary>
    /// Compiles a math-only subset of PostScript procedures into expression tree delegates for PDF Functions.
    /// Produces a vector delegate Action<float[], float[]> where inputs come from the first array in the order
    /// defined by <paramref name="parameterNames"/> and outputs are written to the second array (buffer).
    /// Unsupported tokens/operators cause compilation to fail and the caller should fall back to the interpreter.
    /// NOTE: Only operators commonly used in PDF Type 4 PostScript functions are implemented.
    /// </summary>
    public sealed class PostScriptExpressionCompiler
    {
        /// <summary>
        /// Attempts to compile a math-only PostScript procedure into a reusable vector delegate.
        /// Treats the input parameters as already pushed on the operand stack before executing tokens.
        /// </summary>
        public bool TryCompileMath(PostScriptProcedure procedure, IReadOnlyList<string> parameterNames, out Action<float[], float[]> fn)
        {
            fn = null;
            if (procedure == null)
            {
                return false;
            }

            if (parameterNames == null || parameterNames.Count == 0)
            {
                return false;
            }

            var argsParam = Expression.Parameter(typeof(float[]), "args");
            var bufferParam = Expression.Parameter(typeof(float[]), "buffer");
            var stack = new Stack<Expression>();

            for (int i = 0; i < parameterNames.Count; i++)
            {
                Expression indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                stack.Push(indexExpr);
            }

            foreach (PostScriptToken token in procedure.Tokens)
            {
                if (token is PostScriptNumber number)
                {
                    stack.Push(Expression.Constant(number.Value, typeof(float)));
                    continue;
                }

                if (token is PostScriptLiteralName name)
                {
                    int index = IndexOf(parameterNames, name.Name);
                    if (index < 0)
                    {
                        return false;
                    }

                    Expression indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(index));
                    stack.Push(indexExpr);
                    continue;
                }

                if (token is PostScriptProcedure proc)
                {
                    // Push procedure block marker; consumed by flow-control operators.
                    stack.Push(Expression.Constant(proc, typeof(PostScriptProcedure)));
                    continue;
                }

                if (token is PostScriptExecutableName exec)
                {
                    if (!TryBuildOperator(exec.Name, stack, argsParam, parameterNames))
                    {
                        return false;
                    }

                    continue;
                }

                return false;
            }

            if (stack.Count == 0)
            {
                return false;
            }

            int outputCount = stack.Count;
            var bodyExpressions = new List<Expression>();

            var temp = new List<Expression>(outputCount);
            while (stack.Count > 0)
            {
                temp.Add(EnsureFloat(stack.Pop()));
            }

            for (int i = 0; i < temp.Count; i++)
            {
                int targetIndex = temp.Count - 1 - i;
                var assign = Expression.Assign(
                    Expression.ArrayAccess(bufferParam, Expression.Constant(targetIndex)),
                    temp[i]
                );
                bodyExpressions.Add(assign);
            }

            var block = Expression.Block(bodyExpressions);
            var lambda = Expression.Lambda<Action<float[], float[]>>(block, argsParam, bufferParam);
            try
            {
                fn = lambda.Compile();
                return true;
            }
            catch
            {
                fn = null;
                return false;
            }
        }

        private static int IndexOf(IReadOnlyList<string> names, string name)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static Expression EnsureFloat(Expression expr)
        {
            if (expr.Type == typeof(float))
            {
                return expr;
            }

            if (expr.Type == typeof(double))
            {
                return Expression.Convert(expr, typeof(float));
            }

            if (expr.Type == typeof(bool))
            {
                return Expression.Condition(expr, Expression.Constant(1f), Expression.Constant(0f));
            }

            throw new InvalidOperationException("Unsupported expression type for numeric result: " + expr.Type);
        }

        private static Expression EnsureBool(Expression expr)
        {
            if (expr.Type == typeof(bool))
            {
                return expr;
            }

            return Expression.NotEqual(EnsureFloat(expr), Expression.Constant(0f));
        }

        private static bool TryBuildOperator(string name, Stack<Expression> stack, ParameterExpression argsParam, IReadOnlyList<string> parameterNames)
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

                // Stack manipulation operators
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

                // Conversion and integer math
                case "cvi":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Truncate), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "cvr":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    stack.Push(EnsureFloat(x));
                    return true;
                }
                case "idiv":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    var div = Expression.Divide(EnsureFloat(a), EnsureFloat(b));
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Truncate), new[] { typeof(float) })!, div);
                    stack.Push(call);
                    return true;
                }
                case "mod":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    var ai = Expression.Convert(EnsureFloat(a), typeof(int));
                    var bi = Expression.Convert(EnsureFloat(b), typeof(int));
                    var rem = Expression.Modulo(ai, bi);
                    stack.Push(Expression.Convert(rem, typeof(float)));
                    return true;
                }

                // Unary numeric and logical
                case "neg":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    stack.Push(Expression.Negate(EnsureFloat(x)));
                    return true;
                }
                case "not":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var xb = EnsureBool(x);
                    stack.Push(Expression.Not(xb));
                    return true;
                }
                case "ceiling":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Ceiling), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "floor":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Floor), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "round":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Round), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "truncate":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Truncate), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "exp":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Exp), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "ln":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Log), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "log":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Log10), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "abs":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Abs), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }
                case "sin":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var radians = Expression.Multiply(EnsureFloat(x), Expression.Constant(MathF.PI / 180f));
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Sin), new[] { typeof(float) })!, radians);
                    stack.Push(call);
                    return true;
                }
                case "cos":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var radians = Expression.Multiply(EnsureFloat(x), Expression.Constant(MathF.PI / 180f));
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Cos), new[] { typeof(float) })!, radians);
                    stack.Push(call);
                    return true;
                }
                case "atan":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    var yExpr = EnsureFloat(a);
                    var xExpr = EnsureFloat(b);
                    var atan2 = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Atan2), new[] { typeof(float), typeof(float) })!, yExpr, xExpr);
                    var toDegrees = Expression.Multiply(atan2, Expression.Constant(180f / MathF.PI));
                    var plus360 = Expression.Add(toDegrees, Expression.Constant(360f));
                    var mod360 = Expression.Modulo(plus360, Expression.Constant(360f));
                    stack.Push(mod360);
                    return true;
                }
                case "sqrt":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var call = Expression.Call(typeof(MathF).GetMethod(nameof(MathF.Sqrt), new[] { typeof(float) })!, EnsureFloat(x));
                    stack.Push(call);
                    return true;
                }

                // Binary numeric
                case "add":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.Add(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "sub":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.Subtract(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "mul":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.Multiply(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "div":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.Divide(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }

                // Binary logical / bitwise
                case "and":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    Expression result;
                    if (a.Type == typeof(bool) || b.Type == typeof(bool))
                    {
                        result = Expression.AndAlso(EnsureBool(a), EnsureBool(b));
                    }
                    else
                    {
                        var ai = Expression.Convert(EnsureFloat(a), typeof(int));
                        var bi = Expression.Convert(EnsureFloat(b), typeof(int));
                        result = Expression.Convert(Expression.And(ai, bi), typeof(float));
                    }
                    stack.Push(result);
                    return true;
                }
                case "or":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    Expression result;
                    if (a.Type == typeof(bool) || b.Type == typeof(bool))
                    {
                        result = Expression.OrElse(EnsureBool(a), EnsureBool(b));
                    }
                    else
                    {
                        var ai = Expression.Convert(EnsureFloat(a), typeof(int));
                        var bi = Expression.Convert(EnsureFloat(b), typeof(int));
                        result = Expression.Convert(Expression.Or(ai, bi), typeof(float));
                    }
                    stack.Push(result);
                    return true;
                }
                case "xor":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    Expression result;
                    if (a.Type == typeof(bool) || b.Type == typeof(bool))
                    {
                        result = Expression.ExclusiveOr(EnsureBool(a), EnsureBool(b));
                    }
                    else
                    {
                        var ai = Expression.Convert(EnsureFloat(a), typeof(int));
                        var bi = Expression.Convert(EnsureFloat(b), typeof(int));
                        result = Expression.Convert(Expression.ExclusiveOr(ai, bi), typeof(float));
                    }
                    stack.Push(result);
                    return true;
                }
                case "bitshift":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    var ai = Expression.Convert(EnsureFloat(a), typeof(int));
                    var bi = Expression.Convert(EnsureFloat(b), typeof(int));
                    var zero = Expression.Constant(0);
                    var isNeg = Expression.LessThan(bi, zero);
                    var posCount = Expression.Condition(isNeg, Expression.Negate(bi), bi);
                    var leftShift = Expression.LeftShift(ai, posCount);
                    var rightShift = Expression.RightShift(ai, posCount);
                    var shifted = Expression.Condition(isNeg, rightShift, leftShift);
                    stack.Push(Expression.Convert(shifted, typeof(float)));
                    return true;
                }

                // Relational -> bool
                case "lt":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.LessThan(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "le":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.LessThanOrEqual(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "gt":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.GreaterThan(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "ge":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.GreaterThanOrEqual(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "eq":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.Equal(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }
                case "ne":
                {
                    if (!TryPopBinary(stack, out var a, out var b)) { return false; }
                    stack.Push(Expression.NotEqual(EnsureFloat(a), EnsureFloat(b)));
                    return true;
                }

                // Flow-control blocks
                case "if":
                {
                    if (stack.Count < 2) { return false; }
                    var procExpr = stack.Pop();
                    var condExpr = stack.Pop();
                    if (procExpr is ConstantExpression ce && ce.Value is PostScriptProcedure proc)
                    {
                        if (stack.Count < 1) { return false; }
                        var oldTop = stack.Pop();
                        if (!TryCompileBlock(proc, argsParam, parameterNames, out var blockResult))
                        {
                            return false;
                        }
                        Expression conditionBool = condExpr.Type == typeof(bool) ? condExpr : Expression.NotEqual(EnsureFloat(condExpr), Expression.Constant(0f));
                        var conditionalTop = Expression.Condition(conditionBool, EnsureFloat(blockResult), EnsureFloat(oldTop));
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
                        if (!TryCompileBlock(trueProc, argsParam, parameterNames, out var trueBlock))
                        {
                            return false;
                        }
                        if (!TryCompileBlock(falseProc, argsParam, parameterNames, out var falseBlock))
                        {
                            return false;
                        }
                        Expression conditionBool = condExpr.Type == typeof(bool) ? condExpr : Expression.NotEqual(EnsureFloat(condExpr), Expression.Constant(0f));
                        var ifElse = Expression.Condition(conditionBool, EnsureFloat(trueBlock), EnsureFloat(falseBlock));
                        stack.Push(ifElse);
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        private static bool TryCompileBlock(PostScriptProcedure proc, ParameterExpression argsParam, IReadOnlyList<string> parameterNames, out Expression resultExpr)
        {
            resultExpr = null;
            var tempStack = new Stack<Expression>();
            for (int i = 0; i < parameterNames.Count; i++)
            {
                Expression indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                tempStack.Push(indexExpr);
            }

            foreach (var token in proc.Tokens)
            {
                if (token is PostScriptNumber n)
                {
                    tempStack.Push(Expression.Constant(n.Value, typeof(float)));
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
                    tempStack.Push(indexExpr);
                    continue;
                }
                if (token is PostScriptProcedure innerProc)
                {
                    // Nested procedures are not supported in math subset
                    return false;
                }
                if (token is PostScriptExecutableName ex)
                {
                    if (!TryBuildOperator(ex.Name, tempStack, argsParam, parameterNames))
                    {
                        return false;
                    }
                    continue;
                }
                return false;
            }

            //if (tempStack.Count != 1)
            //{
            //    return false;
            //}

            resultExpr = EnsureFloat(tempStack.Pop());
            return true;
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
