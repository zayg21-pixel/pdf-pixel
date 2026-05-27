using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using PdfPixel.PostScript.Tokens;

namespace PdfPixel.PostScript.Compiler
{
    /// <summary>
    /// Compiles a math-only subset of PostScript procedures into expression tree delegates for PDF Functions.
    /// Produces a vector delegate Action<float[], float[]> where inputs come from the first array in the order
    /// defined by <paramref name="parameterNames"/> and outputs are written to the second array (buffer).
    /// Unsupported tokens/operators cause compilation to fail and the caller should fall back to the interpreter.
    /// NOTE: Only operators commonly used in PDF Type 4 PostScript functions are implemented.
    /// </summary>
    public sealed partial class PostScriptExpressionCompiler
    {
        /// <summary>
        /// Attempts to compile a math-only PostScript procedure into a reusable vector delegate.
        /// Treats the input parameters as already pushed on the operand stack before executing tokens.
        /// </summary>
        public bool TryCompileMath(PostScriptProcedure procedure, IReadOnlyList<string> parameterNames, out Action<float[], float[]>? fn)
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

            ParameterExpression argsParam = Expression.Parameter(typeof(float[]), "args");
            ParameterExpression bufferParam = Expression.Parameter(typeof(float[]), "buffer");

            if (!TryCompileBlockStack(procedure, argsParam, parameterNames, out Stack<Expression> stack))
            {
                return false;
            }

            if (stack.Count == 0)
            {
                return false;
            }

            int outputCount = stack.Count;
            List<Expression> bodyExpressions = [];

            List<Expression> temp = new(outputCount);
            while (stack.Count > 0)
            {
                temp.Add(EnsureFloat(stack.Pop()));
            }

            for (int i = 0; i < temp.Count; i++)
            {
                int targetIndex = temp.Count - 1 - i;
                BinaryExpression assign = Expression.Assign(
                    Expression.ArrayAccess(bufferParam, Expression.Constant(targetIndex)),
                    temp[i]
                );
                bodyExpressions.Add(assign);
            }

            BlockExpression block = Expression.Block(bodyExpressions);
            Expression<Action<float[], float[]>> lambda = Expression.Lambda<Action<float[], float[]>>(block, argsParam, bufferParam);
            fn = lambda.Compile();
            return true;
        }

        private static bool TryBuildOperator(string name, Stack<Expression> stack, ParameterExpression argsParam, IReadOnlyList<string> parameterNames)
        {
            // Route to category-specific handlers. Each returns true if the operator name was handled.
            if (TryBuildOperatorStack(name, stack))
            {
                return true;
            }

            if (TryBuildOperatorMath(name, stack))
            {
                return true;
            }

            if (TryBuildOperatorLogic(name, stack))
            {
                return true;
            }

            if (TryBuildOperatorFlow(name, stack, argsParam, parameterNames))
            {
                return true;
            }

            return false;
        }
    }
}
