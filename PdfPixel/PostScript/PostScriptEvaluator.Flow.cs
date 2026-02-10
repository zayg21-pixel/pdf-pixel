using PdfPixel.PostScript.Tokens;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PdfPixel.PostScript
{
    /// <summary>
    /// Flow/control operator implementations for the PostScript evaluator. Handles conditional execution
    /// (if, ifelse), loop constructs (for, repeat, loop, forall), and interpreter control transfers
    /// (exit, stop, stopped).
    /// </summary>
    public partial class PostScriptEvaluator
    {
        public bool TryExecuteFlowControlOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "exec":
                {
                    ExecProc(stack);
                    return true;
                }
                case "if":
                {
                    IfOp(stack);
                    return true;
                }
                case "ifelse":
                {
                    IfElseOp(stack);
                    return true;
                }
                case "for":
                {
                    ForLoopOperator(stack);
                    return true;
                }
                case "forall":
                {
                    ForAllOperator(stack);
                    return true;
                }
                case "repeat":
                {
                    RepeatOperator(stack);
                    return true;
                }
                case "loop":
                {
                    LoopOperator(stack);
                    return true;
                }
                case "exit":
                {
                    ExitOperator();
                    return true;
                }
                case "stop":
                {
                    StopOperator();
                    return true;
                }
                case "stopped":
                {
                    StoppedOperator(stack);
                    return true;
                }
                case "bind":
                {
                    // Logging only; bind optimization not implemented.
                    _logger.LogDebug("Flow bind (not implemented)");
                    return true;
                }
            }

            return false;
        }

        private void ExitOperator()
        {
            if (_loopDepth <=0)
            {
                throw new System.InvalidOperationException("exit: no active loop to exit");
            }
            _exitRequested = true;
        }

        private void StopOperator()
        {
            // 'stop' signals an early termination; if inside a 'stopped' handler it is captured, else hard-aborts evaluation.
            _stopRequested = true;
            if (_stoppedDepth <=0)
            {
                // Outside stopped context: throw for visibility but retain flag so outer dispatch halts.
                throw new System.InvalidOperationException("stop: no active stopped context");
            }
        }

        private void StoppedOperator(Stack<PostScriptToken> stack)
        {
            // stopped expects: <proc> stopped ; executes proc and pushes true if a stop occurred, else false.
            Ensure(stack,1);
            var procedure = PopOfType<PostScriptProcedure>(stack);
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            int previousStoppedDepth = _stoppedDepth;
            bool previousStopFlag = _stopRequested;
            _stoppedDepth++;
            _stopRequested = false; // clear for inner scope
            EvaluateTokens(procedure.Tokens, stack);
            bool didStop = _stopRequested;
            // Restore outer state
            _stopRequested = previousStopFlag;
            _stoppedDepth = previousStoppedDepth;
            stack.Push(new PostScriptBoolean(didStop));
        }

        private void LoopOperator(Stack<PostScriptToken> stack)
        {
            // loop expects a single procedure operand: proc loop
            Ensure(stack,1);
            var procedure = PopOfType<PostScriptProcedure>(stack);
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            _loopDepth++;
            while (true)
            {
                if (_exitRequested || _stopRequested)
                {
                    break;
                }
                EvaluateTokens(procedure.Tokens, stack);
            }
            _loopDepth--;
            if (_loopDepth ==0 && _exitRequested)
            {
                _exitRequested = false;
            }
        }

        private void ExecProc(Stack<PostScriptToken> stack)
        {
            var procedure = PopOfType<PostScriptProcedure>(stack);
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            foreach (var inner in procedure.Tokens)
            {
                if (inner is PostScriptExecutableName exec)
                {
                    ExecuteOperator(exec.Name, stack);
                }
                else
                {
                    stack.Push(inner);
                }
                if (_exitRequested && _loopDepth <=0)
                {
                    _exitRequested = false;
                }
                if (_stopRequested && _stoppedDepth <=0)
                {
                    // Hard stop outside handler: cease early.
                    break;
                }
            }
        }

        private bool PopCondition(Stack<PostScriptToken> stack)
        {
            var token = stack.Pop();
            return token switch
            {
                PostScriptBoolean b => b.Value,
                PostScriptNumber n => n.Value !=0f,
                _ => false
            };
        }

        private void IfOp(Stack<PostScriptToken> stack)
        {
            Ensure(stack,2);
            var procedure = PopOfType<PostScriptProcedure>(stack);
            bool condition = PopCondition(stack);
            if (!condition)
            {
                return;
            }
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            foreach (var inner in procedure.Tokens)
            {
                if (inner is PostScriptExecutableName exec)
                {
                    ExecuteOperator(exec.Name, stack);
                }
                else
                {
                    stack.Push(inner);
                }
                if (_exitRequested && _loopDepth <=0)
                {
                    _exitRequested = false;
                }
                if (_stopRequested && _stoppedDepth <=0)
                {
                    break;
                }
            }
        }

        private void IfElseOp(Stack<PostScriptToken> stack)
        {
            Ensure(stack,3);
            var falseProcedure = PopOfType<PostScriptProcedure>(stack);
            var trueProcedure = PopOfType<PostScriptProcedure>(stack);
            bool condition = PopCondition(stack);
            var chosen = condition ? trueProcedure : falseProcedure;
            chosen.EnsureAccess(PostScriptAccessOperation.Execute);
            foreach (var inner in chosen.Tokens)
            {
                if (inner is PostScriptExecutableName exec)
                {
                    ExecuteOperator(exec.Name, stack);
                }
                else
                {
                    stack.Push(inner);
                }
                if (_exitRequested && _loopDepth <=0)
                {
                    _exitRequested = false;
                }
                if (_stopRequested && _stoppedDepth <=0)
                {
                    break;
                }
            }
        }

        private void ForLoopOperator(Stack<PostScriptToken> stack)
        {
            Ensure(stack,4);
            var procedure = PopOfType<PostScriptProcedure>(stack);
            var limit = PopOfType<PostScriptNumber>(stack);
            var increment = PopOfType<PostScriptNumber>(stack);
            var initial = PopOfType<PostScriptNumber>(stack);
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            float startValue = initial.Value;
            float stepValue = increment.Value;
            float endValue = limit.Value;
            if (stepValue ==0f)
            {
                return;
            }
            _loopDepth++;
            if (stepValue >0f)
            {
                for (float current = startValue; current <= endValue; current += stepValue)
                {
                    if (_exitRequested || _stopRequested)
                    {
                        break;
                    }
                    stack.Push(new PostScriptNumber(current));
                    EvaluateTokens(procedure.Tokens, stack);
                }
            }
            else
            {
                for (float current = startValue; current >= endValue; current += stepValue)
                {
                    if (_exitRequested || _stopRequested)
                    {
                        break;
                    }
                    stack.Push(new PostScriptNumber(current));
                    EvaluateTokens(procedure.Tokens, stack);
                }
            }
            _loopDepth--;
            if (_loopDepth ==0 && _exitRequested)
            {
                _exitRequested = false;
            }
        }

        private void ForAllOperator(Stack<PostScriptToken> stack)
        {
            Ensure(stack,2);
            var procedure = PopOfType<PostScriptProcedure>(stack);
            var composite = stack.Pop();
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            _loopDepth++;
            switch (composite)
            {
                case PostScriptArray array:
                {
                    foreach (var element in array.Elements)
                    {
                        if (_exitRequested || _stopRequested)
                        {
                            break;
                        }
                        stack.Push(element);
                        EvaluateTokens(procedure.Tokens, stack);
                    }
                    break;
                }
                case PostScriptDictionary dict:
                {
                    foreach (var kvp in dict.Entries)
                    {
                        if (_exitRequested || _stopRequested)
                        {
                            break;
                        }
                        stack.Push(new PostScriptLiteralName(kvp.Key));
                        stack.Push(kvp.Value);
                        EvaluateTokens(procedure.Tokens, stack);
                    }
                    break;
                }
                case PostScriptString str:
                {
                    for (int i =0; i < str.Value.Length; i++)
                    {
                        if (_exitRequested || _stopRequested)
                        {
                            break;
                        }
                        int code = str.Value[i];
                        stack.Push(new PostScriptNumber(code));
                        EvaluateTokens(procedure.Tokens, stack);
                    }
                    break;
                }
                case PostScriptBinaryString bin:
                {
                    var data = bin.Data;
                    for (int i =0; i < data.Length; i++)
                    {
                        if (_exitRequested || _stopRequested)
                        {
                            break;
                        }
                        stack.Push(new PostScriptNumber(data[i]));
                        EvaluateTokens(procedure.Tokens, stack);
                    }
                    break;
                }
                default:
                {
                    _loopDepth--;
                    throw new System.InvalidOperationException("typecheck: forall operand not iterable");
                }
            }
            _loopDepth--;
            if (_loopDepth ==0 && _exitRequested)
            {
                _exitRequested = false;
            }
        }

        private void RepeatOperator(Stack<PostScriptToken> stack)
        {
            Ensure(stack,2);
            var procedure = PopOfType<PostScriptProcedure>(stack);
            var countToken = PopOfType<PostScriptNumber>(stack);
            int count = (int)countToken.Value;
            if (count <0)
            {
                throw new System.InvalidOperationException("rangecheck: repeat count negative");
            }
            procedure.EnsureAccess(PostScriptAccessOperation.Execute);
            _loopDepth++;
            for (int i =0; i < count; i++)
            {
                if (_exitRequested || _stopRequested)
                {
                    break;
                }
                EvaluateTokens(procedure.Tokens, stack);
            }
            _loopDepth--;
            if (_loopDepth ==0 && _exitRequested)
            {
                _exitRequested = false;
            }
        }
    }
}
