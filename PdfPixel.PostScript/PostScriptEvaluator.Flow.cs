using Microsoft.Extensions.Logging;
using PdfPixel.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfPixel.PostScript;

/// <summary>
/// Flow/control operator implementations for the PostScript evaluator. Handles conditional execution
/// (if, ifelse), loop constructs (for, repeat, loop, forall), and interpreter control transfers
/// (exit, stop, stopped).
/// </summary>
public partial class PostScriptEvaluator
{
    /// <summary>
    /// Attempts to handle a flow-control operator by name. Covers conditional execution (if, ifelse),
    /// loop constructs (for, repeat, loop, forall), interpreter control transfers (exec, exit, stop,
    /// stopped), and the bind optimization stub. Returns true if the operator was recognised and handled;
    /// false if it is not a flow-control operator.
    /// </summary>
    /// <param name="name">Name of the PostScript operator to dispatch.</param>
    /// <param name="stack">The operand stack used by the current evaluation context.</param>
    /// <returns>True if <paramref name="name"/> is a flow-control operator; otherwise false.</returns>
    public bool TryExecuteFlowControlOperator(string name, Stack<PostScriptToken> stack)
    {
        if (stack == null)
        {
            throw new ArgumentNullException(nameof(stack));
        }

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
                // bind replaces executable operator names within a procedure with direct references to
                // those operators, so later redefinitions of the same names no longer affect the procedure.
                // Names are always resolved dynamically here instead, so a redefined operator would be
                // picked up where real PostScript would keep the original. This only matters for inputs
                // that redefine built-in operator names, which does not occur in Type1 font, CMap, or
                // PDF function PostScript in practice.
                // TODO: [LOW] implement bind's early-binding semantics, can be found in fit11-talk.pdf
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
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
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
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
        procedure.EnsureAccess(PostScriptAccessOperation.Execute);
        _loopDepth++;
        while (!_exitRequested && !_stopRequested)
        {

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
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
        procedure.EnsureAccess(PostScriptAccessOperation.Execute);
        foreach (PostScriptToken inner in procedure.Tokens)
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
        PostScriptToken token = stack.Pop();
        return token switch
        {
            PostScriptBoolean b => b.BooleanValue,
            PostScriptNumber n => n.Number !=0f,
            _ => false
        };
    }

    private void IfOp(Stack<PostScriptToken> stack)
    {
        Ensure(stack,2);
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
        bool condition = PopCondition(stack);
        if (!condition)
        {
            return;
        }

        procedure.EnsureAccess(PostScriptAccessOperation.Execute);
        foreach (PostScriptToken inner in procedure.Tokens)
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
        PostScriptProcedure falseProcedure = PopOfType<PostScriptProcedure>(stack);
        PostScriptProcedure trueProcedure = PopOfType<PostScriptProcedure>(stack);
        bool condition = PopCondition(stack);
        PostScriptProcedure chosen = condition ? trueProcedure : falseProcedure;
        chosen.EnsureAccess(PostScriptAccessOperation.Execute);
        foreach (PostScriptToken inner in chosen.Tokens)
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
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
        PostScriptNumber limit = PopOfType<PostScriptNumber>(stack);
        PostScriptNumber increment = PopOfType<PostScriptNumber>(stack);
        PostScriptNumber initial = PopOfType<PostScriptNumber>(stack);
        procedure.EnsureAccess(PostScriptAccessOperation.Execute);
        float startValue = initial.Number;
        float stepValue = increment.Number;
        float endValue = limit.Number;
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
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
        PostScriptToken composite = stack.Pop();
        procedure.EnsureAccess(PostScriptAccessOperation.Execute);
        _loopDepth++;
        switch (composite)
        {
            case PostScriptArray array:
            {
                foreach (PostScriptToken element in array.Elements)
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
                foreach (KeyValuePair<string, PostScriptToken> kvp in dict.Entries)
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
                for (int i =0; i < str.Data.Length; i++)
                {
                    if (_exitRequested || _stopRequested)
                    {
                        break;
                    }

                    int code = str.Data[i];
                    stack.Push(new PostScriptNumber(code));
                    EvaluateTokens(procedure.Tokens, stack);
                }

                break;
            }
            case PostScriptBinaryString bin:
            {
                byte[] data = bin.Data;
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
        PostScriptProcedure procedure = PopOfType<PostScriptProcedure>(stack);
        PostScriptNumber countToken = PopOfType<PostScriptNumber>(stack);
        var count = (int)countToken.Number;
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
