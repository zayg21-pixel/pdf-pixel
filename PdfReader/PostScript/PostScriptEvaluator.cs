using Microsoft.Extensions.Logging;
using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfReader.PostScript
{
    /// <summary>
    /// Extended PostScript evaluator supporting basic arithmetic plus limited object construction
    /// (names, strings, arrays) and 'def', 'array', 'put' operators needed for Type1 font header parsing.
    /// Restricted interpreter (no graphics ops, no file IO).
    /// </summary>
    public partial class PostScriptEvaluator
    {
        private readonly List<PostScriptToken> _tokens;
        private readonly ILogger<PostScriptEvaluator> _logger;

        // Dictionary stack (system then user); executable names are resolved through this before builtins.
        private readonly Stack<PostScriptDictionary> _dictStack = new Stack<PostScriptDictionary>();
        private readonly PostScriptDictionary _systemDict = new PostScriptDictionary();
        private readonly PostScriptDictionary _userDict = new PostScriptDictionary();

        private int _loopDepth;
        private bool _exitRequested;
        private bool _stopRequested;
        private int _stoppedDepth;

        public PostScriptEvaluator(ReadOnlySpan<byte> code, ILogger<PostScriptEvaluator> logger)
        {
            _logger = logger;
            _tokens = Tokenize(code);
            _dictStack.Push(_systemDict); // systemdict bottom
            _dictStack.Push(_userDict); // userdict top
            _loopDepth = 0;
            _exitRequested = false;
            _stopRequested = false;
            _stoppedDepth = 0;
        }

        /// <summary>
        /// Inject or override a value in the system dictionary (e.g., FontDirectory).
        /// </summary>
        public void SetSystemValue(string name, PostScriptToken value)
        {
            if (string.IsNullOrWhiteSpace(name) || value == null)
            {
                return;
            }
            _systemDict.Entries[name] = value;
        }

        /// <summary>
        /// Evaluate tokens against a stack.
        /// </summary>
        public void EvaluateTokens(List<PostScriptToken> tokens, Stack<PostScriptToken> stack)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (_stopRequested && _stoppedDepth == 0)
                {
                    // Hard stop outside any stopped handler: terminate evaluation early.
                    break;
                }
                PostScriptToken token = tokens[i];
                switch (token)
                {
                    case PostScriptNumber number:
                    {
                        stack.Push(number);
                        break;
                    }
                    case PostScriptString textString:
                    {
                        stack.Push(textString);
                        break;
                    }
                    case PostScriptBinaryString binaryString:
                    {
                        stack.Push(binaryString);
                        break;
                    }
                    case PostScriptLiteralName literalName:
                    {
                        stack.Push(literalName);
                        break;
                    }
                    case PostScriptArray array:
                    {
                        stack.Push(array);
                        break;
                    }
                    case PostScriptProcedure procedure:
                    {
                        stack.Push(procedure);
                        break;
                    }
                    case PostScriptExecutableName exec:
                    {
                        ExecuteOperator(exec.Name, stack);
                        break;
                    }
                    case PostScriptBoolean b:
                    {
                        stack.Push(b);
                        break;
                    }
                }
                // Early exit propagation: if exit requested outside any loop just clear flag (defensive).
                if (_exitRequested && _loopDepth <= 0)
                {
                    _exitRequested = false;
                }
                // Clear stray stop if no handler active (left set for outer abort semantics).
                if (_stopRequested && _stoppedDepth == 0)
                {
                    // Intentionally left as is.
                }
            }
        }

        /// <summary>
        /// Evaluate the program's root tokens against a stack.
        /// </summary>
        public void EvaluateTokens(Stack<PostScriptToken> stack)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }
            EvaluateTokens(_tokens, stack);
        }

        private bool TryLookupDict(string name, out PostScriptToken value)
        {
            foreach (PostScriptDictionary dict in _dictStack)
            {
                if (dict.Entries.TryGetValue(name, out value))
                {
                    return true;
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Executes an operator or looks up a defined name/procedure via the dictionary stack.
        /// </summary>
        private void ExecuteOperator(string name, Stack<PostScriptToken> stack)
        {
            if (_stopRequested && _stoppedDepth == 0)
            {
                // Hard stop: ignore further operators.
                return;
            }
            // Dictionary stack lookup (system/user and any begin-added dictionaries)
            if (TryLookupDict(name, out PostScriptToken dictValue))
            {
                if (dictValue is PostScriptProcedure dictProc)
                {
                    EvaluateTokens(dictProc.Tokens, stack);
                }
                else
                {
                    stack.Push(dictValue);
                }
                return;
            }

            if (TryProcessMathOperator(name, stack))
            {
                return;
            }

            if (TryProcessLogicOperator(name, stack))
            {
                return;
            }

            if (TryProcessStackOperator(name, stack))
            {
                return;
            }

            if (TryExecuteFlowControlOperator(name, stack))
            {
                return;
            }

            if (TryProcessCollectionOperator(name, stack))
            {
                return;
            }

            switch (name)
            {
                case "readonly":
                {
                    AccessSetOperator(stack, PostScriptAccess.ReadOnly);
                    break;
                }
                case "executeonly":
                {
                    AccessSetOperator(stack, PostScriptAccess.ExecuteOnly);
                    break;
                }
                case "noaccess":
                {
                    AccessSetOperator(stack, PostScriptAccess.NoAccess);
                    break;
                }
                case "load":
                {
                    LoadOperator(stack);
                    break;
                }
                case "save":
                {
                    // TODO: Marker-only 'save' implementation; does not snapshot VM state. Future enhancement could capture dictionary stack and operand depth.
                    stack.Push(new PostScriptSave());
                    break;
                }
                case "restore":
                {
                    // TODO: Marker-only 'restore' implementation; pops objects until a PostScriptSave marker is found, discarding it. No actual state rollback.
                    PostScriptSave foundMarker = null;
                    var temp = new Stack<PostScriptToken>();
                    while (stack.Count > 0)
                    {
                        var top = stack.Pop();
                        if (top is PostScriptSave saveMarker)
                        {
                            foundMarker = saveMarker;
                            break;
                        }
                        // Discard non-marker tokens above save.
                    }
                    // If no marker found, nothing else to do.
                    break;
                }
                default:
                {
                    _logger?.LogWarning("Unsupported PostScript operator/name: {Operator}", name);
                    break;
                }
            }
        }
    }

    // TODO: Font-related PostScript operators pending or partial:
    // - findfont: lookup font in FontDirectory // Typically unnecessary for embedded PDF Type1 fonts; PDF provides font dict.
    // - definefont: register font dictionary under name // Optional; PDF font dictionaries are already instantiated.
    // - scalefont: apply scaling to FontMatrix / metrics // Rarely needed; scaling handled via PDF font matrix.
    // - makefont: compose new font with matrix // Optional; out of scope currently.
    // - setfont: select current font (not needed for static analysis) // Confirmed not required for header parsing.
    // - stringwidth: compute glyph string width (optional) // Could be deferred until width metrics needed.
    // - charpath / setcachedevice / setcharwidth (Type1 charstring build ops) // Charstring interpreter will handle; not in header evaluator.
    // - save / restore: implement proper memory snapshot and rollback // 'save' stub present; 'restore' pending if needed.
    // - noaccess / executeonly: mark dictionary / procedure access flags (readonly covers subset) // Could extend readonly flag.
    // - readonly variants: xreadonly if encountered (alias) // Not yet observed; easy alias.
    // - currentfile: access underlying stream (not required here) // For 'currentfile eexec' boundary; safe sentinel stub.
    // - count / mark / cleartomark: stack markers (not currently used in font headers observed) // Implement only if encountered.
    // - getinterval / putinterval: substring / subarray extraction (rare in plain Type1 headers) // Defer.
    // - astore / aload: array packing/unpacking helpers // Not yet required.
    // - for / repeat / loop / exit / stop: flow control (for implemented, others omitted) // 'for' done; others pending on demand.
    // - where / load / known: dictionary queries (implemented) // Implemented.
    // - get / put: dictionary and array element access (array put implemented; dict put TBD) // 'get' done; dict 'put' pending.
    // - length / type: object inspection (not yet implemented) // Potential future improvement.
    // - token / search: string parsing (not required for static font programs) // Out of scope.
    // - index / roll / copy / dup / exch / pop / clear: stack ops (implemented subset) // Implemented.
    // - if / ifelse / exec / bind (bind omitted; could pre-bind procedures for speed) // Implemented except 'bind'.
}
