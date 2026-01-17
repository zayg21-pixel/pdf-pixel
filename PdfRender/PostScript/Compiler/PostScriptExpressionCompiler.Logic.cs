using System.Collections.Generic;
using System.Linq.Expressions;

namespace PdfRender.PostScript.Compiler
{
    public sealed partial class PostScriptExpressionCompiler
    {
        private static bool TryBuildOperatorLogic(string name, Stack<Expression> stack)
        {
            switch (name)
            {
                case "not":
                {
                    if (!TryPopUnary(stack, out var x)) { return false; }
                    var xb = EnsureBool(x);
                    stack.Push(Expression.Not(xb));
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
            }

            return false;
        }
    }
}
