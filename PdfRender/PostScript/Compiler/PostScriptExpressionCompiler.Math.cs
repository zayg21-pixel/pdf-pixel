using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace PdfRender.PostScript.Compiler
{
    public sealed partial class PostScriptExpressionCompiler
    {
        private static bool TryBuildOperatorMath(string name, Stack<Expression> stack)
        {
            switch (name)
            {
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
            }

            return false;
        }
    }
}
