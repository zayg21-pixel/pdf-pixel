using PdfRender.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfRender.PostScript
{
    public partial class PostScriptEvaluator
    {
        private bool TryProcessLogicOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "eq":
                {
                    BinaryRelational(stack, (l, r) => l == r);
                    return true;
                }
                case "ne":
                {
                    BinaryRelational(stack, (l, r) => l != r);
                    return true;
                }
                case "gt":
                {
                    BinaryRelational(stack, (l, r) => l > r);
                    return true;
                }
                case "ge":
                {
                    BinaryRelational(stack, (l, r) => l >= r);
                    return true;
                }
                case "lt":
                {
                    BinaryRelational(stack, (l, r) => l < r);
                    return true;
                }
                case "le":
                {
                    BinaryRelational(stack, (l, r) => l <= r);
                    return true;
                }
                case "and":
                {
                    BinaryLogical(stack, (a, b) => a & b);
                    return true;
                }
                case "or":
                {
                    BinaryLogical(stack, (a, b) => a | b);
                    return true;
                }
                case "xor":
                {
                    BinaryLogical(stack, (a, b) => a ^ b);
                    return true;
                }
                case "not":
                {
                    UnaryLogical(stack);
                    return true;
                }
            }

            return false;
        }

        private static void UnaryLogical(Stack<PostScriptToken> stack)
        {
            Ensure(stack, 1);
            PostScriptToken operand = stack.Pop();
            stack.Push(!operand);
        }

        private void BinaryLogical(Stack<PostScriptToken> stack, Func<PostScriptToken, PostScriptToken, PostScriptToken> predicate)
        {
            Ensure(stack, 2);
            PostScriptToken right = stack.Pop();
            PostScriptToken left = stack.Pop();

            stack.Push(predicate(left, right));
        }

        private void BinaryRelational(Stack<PostScriptToken> stack, Func<PostScriptToken, PostScriptToken, bool> predicate)
        {
            Ensure(stack, 2);
            PostScriptToken right = stack.Pop();
            PostScriptToken left = stack.Pop();
            var result = predicate(left, right);
            stack.Push(new PostScriptBoolean(result));
        }
    }
}
