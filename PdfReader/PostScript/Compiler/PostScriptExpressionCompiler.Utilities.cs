using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace PdfReader.PostScript.Compiler
{
    public sealed partial class PostScriptExpressionCompiler
    {
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
    }
}
