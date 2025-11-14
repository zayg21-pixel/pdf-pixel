using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfReader.PostScript
{
    public partial class PostScriptEvaluator
    {
        private bool TryProcessMathOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "add":
                {
                    BinaryNumeric(stack, (a, b) => a + b);
                    return true;
                }
                case "sub":
                {
                    BinaryNumeric(stack, (a, b) => a - b);
                    return true;
                }
                case "mul":
                {
                    BinaryNumeric(stack, (a, b) => a * b);
                    return true;
                }
                case "div":
                {
                    BinaryNumeric(stack, (a, b) => a / b);
                    return true;
                }
                case "idiv":
                {
                    BinaryNumeric(stack, (a, b) => MathF.Truncate(a / b));
                    return true;
                }
                case "bitshift":
                {
                    BinaryNumeric(stack, (a, b) => b < 0 ? ((int)a >> (int)b) : ((int)a << (int)b));
                    return true;
                }
                case "cvi":
                {
                    UnaryNumeric(stack, a => (int)a);
                    return true;
                }
                case "cvr":
                {
                    UnaryNumeric(stack, a => a);
                    return true;
                }
                case "srand":
                {
                    var seed = PopOfType<PostScriptNumber>(stack).Value;
                    _random = new Random((int)seed);
                    return true;
                }
                case "rand":
                {
                    float value = (float)_random.NextDouble();
                    stack.Push(new PostScriptNumber(value));
                    return true;
                }
                case "mod":
                {
                    BinaryNumeric(stack, (a, b) => a % b);
                    return true;
                }
                case "abs":
                {
                    UnaryNumeric(stack, MathF.Abs);
                    return true;
                }
                case "neg":
                {
                    UnaryNumeric(stack, a => -a);
                    return true;
                }
                case "sqrt":
                {
                    UnaryNumeric(stack, MathF.Sqrt);
                    return true;
                }
                case "sin":
                {
                    UnaryNumeric(stack, a => MathF.Sin(a * MathF.PI / 180f));
                    return true;
                }
                case "cos":
                {
                    UnaryNumeric(stack, a => MathF.Cos(a * MathF.PI / 180f));
                    return true;
                }
                case "exp":
                {
                    BinaryNumeric(stack, MathF.Pow);
                    return true;
                }
                case "ln":
                {
                    UnaryNumeric(stack, MathF.Log);
                    return true;
                }
                case "log":
                {
                    UnaryNumeric(stack, MathF.Log10);
                    return true;
                }
                case "floor":
                {
                    UnaryNumeric(stack, MathF.Floor);
                    return true;
                }
                case "ceiling":
                {
                    UnaryNumeric(stack, MathF.Ceiling);
                    return true;
                }
                case "round":
                {
                    UnaryNumeric(stack, MathF.Round);
                    return true;
                }
                case "truncate":
                {
                    UnaryNumeric(stack, MathF.Truncate);
                    return true;
                }
                case "min":
                {
                    BinaryNumeric(stack, MathF.Min);
                    return true;
                }
                case "max":
                {
                    BinaryNumeric(stack, MathF.Max);
                    return true;
                }
                case "atan":
                {
                    BinaryNumeric(stack, (y, x) => ((MathF.Atan2(y, x) * 180f / MathF.PI) + 360) % 360);
                    return true;
                }
            }

            return false;
        }

        private void BinaryNumeric(Stack<PostScriptToken> stack, Func<float, float, float> operation)
        {
            Ensure(stack, 2);
            
            float right = PopOfType<PostScriptNumber>(stack).Value;
            float left = PopOfType<PostScriptNumber>(stack).Value;
            stack.Push(new PostScriptNumber(operation(left, right)));
        }

        private void UnaryNumeric(Stack<PostScriptToken> stack, Func<float, float> operation)
        {
            Ensure(stack, 1);
            float value = PopOfType<PostScriptNumber>(stack).Value;
            float result = operation(value);
            stack.Push(new PostScriptNumber(result));
        }
    }
}
