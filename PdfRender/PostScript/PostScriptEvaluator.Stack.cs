using Microsoft.Extensions.Logging;
using PdfRender.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfRender.PostScript
{
    /// <summary>
    /// Partial implementation of the <see cref="PostScriptEvaluator"/> focused on operand stack manipulation
    /// operators (dup, exch, pop, roll, index, copy, clear) plus marker and diagnostic helpers
    /// (mark, count, counttomark, cleartomark, stack, pstack). These operators provide core stack
    /// semantics required by restricted PostScript execution for PDF Type1 font programs and
    /// calculator functions while intentionally limiting introspection and side effects.
    /// </summary>
    public partial class PostScriptEvaluator
    {
        private bool TryProcessStackOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                // Stack manipulation
                case "dup":
                {
                    Ensure(stack, 1);
                    stack.Push(stack.Peek());
                    return true;
                }
                case "exch":
                {
                    Ensure(stack, 2);
                    var first = stack.Pop();
                    var second = stack.Pop();
                    stack.Push(first);
                    stack.Push(second);
                    return true;
                }
                case "pop":
                {
                    Ensure(stack, 1);
                    stack.Pop();
                    return true;
                }
                case "roll":
                {
                    Roll(stack);
                    return true;
                }
                case "index":
                {
                    IndexOp(stack);
                    return true;
                }
                case "copy":
                {
                    CopyOp(stack);
                    return true;
                }
                case "clear":
                {
                    stack.Clear();
                    return true;
                }
                case "mark":
                {
                    stack.Push(PostScriptMark.Instance);
                    return true;
                }
                case "count":
                {
                    stack.Push(new PostScriptNumber(stack.Count));
                    return true;
                }
                case "counttomark":
                {
                    CountToMarkOp(stack);
                    return true;
                }
                case "cleartomark":
                {
                    ClearToMarkOp(stack);
                    return true;
                }
                case "stack":
                {
                    // Debug: logs only count (no enumeration to avoid exposing executeonly contents by reference).
                    _logger?.LogInformation("PostScript stack operator invoked. Count={Count}", stack.Count);
                    return true;
                }
                case "pstack":
                {
                    // Debug: logs only count (full dump omitted intentionally).
                    _logger?.LogInformation("PostScript pstack operator invoked. Count={Count}", stack.Count);
                    return true;
                }
            }
            return false;
        }

        private void Roll(Stack<PostScriptToken> stack)
        {
            // roll <n> <j> (PostScript order: segmentCount then rollCount) but implementation previously popped rollCount first.
            Ensure(stack, 2);
            var rollCountToken = PopOfType<PostScriptNumber>(stack);
            var segmentCountToken = PopOfType<PostScriptNumber>(stack);
            int rollCount = (int)rollCountToken.Value;
            int segmentCount = (int)segmentCountToken.Value;
            if (segmentCount <= 0 || segmentCount > stack.Count)
            {
                throw new InvalidOperationException("rangecheck: roll segment count out of range");
            }

            var buffer = new PostScriptToken[segmentCount];
            for (int i = segmentCount - 1; i >= 0; i--)
            {
                buffer[i] = stack.Pop();
            }

            int normalized = ((rollCount % segmentCount) + segmentCount) % segmentCount;
            for (int i = 0; i < segmentCount; i++)
            {
                stack.Push(buffer[(i + segmentCount - normalized) % segmentCount]);
            }
        }

        private void IndexOp(Stack<PostScriptToken> stack)
        {
            Ensure(stack, 1);
            var indexToken = PopOfType<PostScriptNumber>(stack);
            int index = (int)indexToken.Value;
            if (index < 0 || index >= stack.Count)
            {
                throw new InvalidOperationException("rangecheck: index operand out of range");
            }
            var snapshot = stack.ToArray(); // Top of stack at snapshot[0]
            stack.Push(snapshot[index]); // Shallow reference duplication.
        }

        private void CopyOp(Stack<PostScriptToken> stack)
        {
            Ensure(stack, 1);
            var countToken = PopOfType<PostScriptNumber>(stack);
            int count = (int)countToken.Value;
            if (count < 0 || count > stack.Count)
            {
                throw new InvalidOperationException("rangecheck: copy count out of range");
            }
            var snapshot = stack.ToArray(); // Top at index0
            for (int i = count - 1; i >= 0; i--)
            {
                stack.Push(snapshot[i]);
            }
        }

        private void CountToMarkOp(Stack<PostScriptToken> stack)
        {
            int depthAboveMark = 0;
            foreach (var token in stack)
            {
                if (token is PostScriptMark)
                {
                    stack.Push(new PostScriptNumber(depthAboveMark));
                    return;
                }
                depthAboveMark++;
            }
            // No mark found; PostScript spec: unmatchedmark error.
            throw new InvalidOperationException("unmatchedmark: counttomark with no mark on stack");
        }

        private void ClearToMarkOp(Stack<PostScriptToken> stack)
        {
            // Pop until mark encountered, then pop mark. If no mark -> raise unmatchedmark error.
            bool found = false;
            while (stack.Count > 0)
            {
                var top = stack.Pop();
                if (top is PostScriptMark)
                {
                    found = true;
                    break;
                }
                // discard regular tokens
            }
            if (!found)
            {
                throw new InvalidOperationException("unmatchedmark: cleartomark with no mark on stack");
            }
        }
    }
}
