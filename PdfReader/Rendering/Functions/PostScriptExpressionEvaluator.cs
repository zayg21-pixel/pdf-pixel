using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace PdfReader.Rendering.Functions
{
    /// <summary>
    /// Stack-based evaluator for PostScript Calculator functions (PDF Type 4).
    /// Supports numeric literals, basic arithmetic, stack operations, and nested blocks.
    /// </summary>
    public class PostScriptExpressionEvaluator
    {
        private readonly List<PostScriptToken> _tokens;

        public PostScriptExpressionEvaluator(string code)
        {
            var root = Tokenize(code);

            if (root.Count == 1 && root[0] is PostScriptProcedure procedure)
            {
                _tokens = procedure.Tokens;
            }
            else
            {
                _tokens = root;
            }
        }

        /// <summary>
        /// Tokenizes the provided PostScript code into a list of tokens.
        /// </summary>
        /// <returns>A list of <see cref="PostScriptToken"/> objects representing the tokens parsed from the input code.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the input contains unmatched opening or closing braces.</exception>
        private List<PostScriptToken> Tokenize(string code)
        {
            List<PostScriptToken> tokens = new();
            int position = 0;

            while (position < code.Length)
            {
                // Skip whitespace
                while (position < code.Length && char.IsWhiteSpace(code[position]))
                    position++;

                if (position >= code.Length)
                    break;

                char c = code[position];

                if (c == '{')
                {
                    // Parse nested procedure
                    position++;
                    int start = position;
                    int braceCount = 1;

                    while (position < code.Length && braceCount > 0)
                    {
                        if (code[position] == '{')
                        {
                            braceCount++;
                        }
                        else if (code[position] == '}')
                        {
                            braceCount--;
                        }
                        position++;
                    }

                    if (braceCount != 0)
                    {
                        throw new InvalidOperationException("Unmatched braces");
                    }

                    string inner = code.Substring(start, position - start - 1);
                    tokens.Add(new PostScriptProcedure(Tokenize(inner)));
                    continue;
                }

                if (c == '}')
                {
                    throw new InvalidOperationException("Unexpected closing brace");
                }

                // Read token until whitespace or brace
                int tokenStart = position;
                while (position < code.Length && !char.IsWhiteSpace(code[position]) &&
                       code[position] != '{' && code[position] != '}')
                {
                    position++;
                }

                string token = code.Substring(tokenStart, position - tokenStart);
                if (float.TryParse(token, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture,
                                   out float number))
                {
                    tokens.Add(new PostScriptNumber(number));
                }
                else
                {
                    tokens.Add(new PostScriptOperator(token));
                }
            }

            return tokens;
        }

        /// <summary>
        /// Evaluates the current collection of tokens and applies their effects to the specified stack.
        /// </summary>
        /// <remarks>This method processes the tokens in the current instance and modifies the provided
        /// stack based on the evaluation results. Ensure the stack is properly initialized before calling this
        /// method.</remarks>
        /// <param name="stack">The stack to which the tokens' effects will be applied. Must not be null.</param>
        public void EvaluateTokens(Stack<PostScriptToken> stack)
        {
            EvaluateTokens(_tokens, stack);
        }

        private void EvaluateTokens(List<PostScriptToken> tokens, Stack<PostScriptToken> stack)
        {
            foreach (PostScriptToken token in tokens)
            {
                switch (token)
                {
                    case PostScriptNumber numberToken:
                    {
                        stack.Push(numberToken);
                        break;
                    }
                    case PostScriptProcedure procedureToken:
                    {
                        stack.Push(procedureToken);
                        break;
                    }
                    case PostScriptOperator operatorToken:
                    {
                        ExecuteOperator(operatorToken.Name, stack);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Executes a PostScript operator using a stack of PSToken values.
        /// </summary>
        /// <param name="name">The operator name.</param>
        /// <param name="stack">The stack for evaluation.</param>
        private void ExecuteOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "add":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a + b));
                    break;
                }
                case "sub":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a - b));
                    break;
                }
                case "mul":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a * b));
                    break;
                }
                case "div":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a / b));
                    break;
                }
                case "mod":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a % b));
                    break;
                }
                case "abs":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Abs(a)));
                    break;
                }
                case "neg":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(-a));
                    break;
                }
                case "sqrt":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Sqrt(a)));
                    break;
                }
                case "sin":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Sin(a * MathF.PI / 180.0f)));
                    break;
                }
                case "cos":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Cos(a * MathF.PI / 180.0f)));
                    break;
                }
                case "exp":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Pow(a, b)));
                    break;
                }
                case "ln":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Log(a)));
                    break;
                }
                case "log":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Log10(a)));
                    break;
                }
                case "floor":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Floor(a)));
                    break;
                }
                case "ceiling":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Ceiling(a)));
                    break;
                }
                case "round":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Round(a)));
                    break;
                }
                case "truncate":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Truncate(a)));
                    break;
                }
                case "min":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Min(a, b)));
                    break;
                }
                case "max":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(MathF.Max(a, b)));
                    break;
                }
                case "eq":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a == b ? 1f : 0f));
                    break;
                }
                case "ne":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a != b ? 1f : 0f));
                    break;
                }
                case "gt":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a > b ? 1f : 0f));
                    break;
                }
                case "ge":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a >= b ? 1f : 0f));
                    break;
                }
                case "lt":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a < b ? 1f : 0f));
                    break;
                }
                case "le":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a <= b ? 1f : 0f));
                    break;
                }
                case "and":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber((a != 0f && b != 0f) ? 1f : 0f));
                    break;
                }
                case "or":
                {
                    EnsureStackCount(stack, 2);
                    float b = ((PostScriptNumber)stack.Pop()).Value;
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber((a != 0f || b != 0f) ? 1f : 0f));
                    break;
                }
                case "not":
                {
                    EnsureStackCount(stack, 1);
                    float a = ((PostScriptNumber)stack.Pop()).Value;
                    stack.Push(new PostScriptNumber(a == 0f ? 1f : 0f));
                    break;
                }
                case "dup":
                {
                    EnsureStackCount(stack, 1);
                    PostScriptToken top = stack.Peek();
                    stack.Push(top);
                    break;
                }
                case "exch":
                {
                    EnsureStackCount(stack, 2);
                    PostScriptToken a = stack.Pop();
                    PostScriptToken b = stack.Pop();
                    stack.Push(a);
                    stack.Push(b);
                    break;
                }
                case "pop":
                {
                    EnsureStackCount(stack, 1);
                    stack.Pop();
                    break;
                }
                case "roll":
                {
                    EnsureStackCount(stack, 2);
                    int n = (int)((PostScriptNumber)stack.Pop()).Value;
                    int j = (int)((PostScriptNumber)stack.Pop()).Value;
                    if (n <= 0 || stack.Count < n)
                    {
                        break;
                    }
                    var temp = new PostScriptToken[n];
                    for (int i = n - 1; i >= 0; i--)
                    {
                        temp[i] = stack.Pop();
                    }
                    int roll = ((j % n) + n) % n;
                    var rolled = new PostScriptToken[n];
                    for (int i = 0; i < n; i++)
                    {
                        rolled[i] = temp[(i + n - roll) % n];
                    }
                    for (int i = 0; i < n; i++)
                    {
                        stack.Push(rolled[i]);
                    }
                    break;
                }
                case "index":
                {
                    EnsureStackCount(stack, 1);
                    int n = (int)((PostScriptNumber)stack.Pop()).Value;
                    if (n < 0 || n >= stack.Count)
                    {
                        throw new InvalidOperationException("Stack range error");
                    }
                    PostScriptToken[] arr = new PostScriptToken[stack.Count];
                    stack.CopyTo(arr, 0);
                    stack.Push(arr[stack.Count - n - 1]);
                    break;
                }
                case "copy":
                {
                    EnsureStackCount(stack, 1);
                    int n = (int)((PostScriptNumber)stack.Pop()).Value;
                    if (n < 0 || stack.Count < n)
                    {
                        break;
                    }
                    var temp = new PostScriptToken[n];
                    for (int i = n - 1; i >= 0; i--)
                    {
                        temp[i] = stack.Pop();
                    }
                    for (int i = 0; i < n; i++)
                    {
                        stack.Push(temp[i]);
                    }
                    for (int i = 0; i < n; i++)
                    {
                        stack.Push(temp[i]);
                    }
                    break;
                }
                case "clear":
                {
                    stack.Clear();
                    break;
                }
                case "exec":
                {
                    EnsureStackCount(stack, 1);
                    PostScriptToken procToken = stack.Pop();
                    if (procToken is not PostScriptProcedure proc)
                    {
                        throw new InvalidOperationException("exec expects a procedure");
                    }
                    EvaluateTokens(proc.Tokens, stack);
                    break;
                }
                case "if":
                {
                    EnsureStackCount(stack, 2);
                    PostScriptToken procToken = stack.Pop();
                    float cond = ((PostScriptNumber)stack.Pop()).Value;
                    if (procToken is not PostScriptProcedure proc)
                    {
                        throw new InvalidOperationException("if expects a procedure");
                    }
                    if (cond != 0f)
                    {
                        EvaluateTokens(proc.Tokens, stack);
                    }
                    break;
                }
                case "ifelse":
                {
                    EnsureStackCount(stack, 3);
                    PostScriptToken procFalseToken = stack.Pop();
                    PostScriptToken procTrueToken = stack.Pop();
                    float cond = ((PostScriptNumber)stack.Pop()).Value;
                    if (procTrueToken is not PostScriptProcedure procTrue || procFalseToken is not PostScriptProcedure procFalse)
                    {
                        throw new InvalidOperationException("ifelse expects two procedures");
                    }
                    if (cond != 0f)
                    {
                        EvaluateTokens(procTrue.Tokens, stack);
                    }
                    else
                    {
                        EvaluateTokens(procFalse.Tokens, stack);
                    }
                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported operator: {name}");
                }
            }
        }

        private static void EnsureStackCount(Stack<PostScriptToken> stack, int required)
        {
            if (stack.Count < required)
            {
                throw new InvalidOperationException($"Stack underflow: expected {required}, found {stack.Count}");
            }
        }
    }
}
