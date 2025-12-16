using Microsoft.Extensions.Logging;
using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private readonly PostScriptDictionary _resources = new PostScriptDictionary();
        private readonly PostScriptDictionary _userDict = new PostScriptDictionary();

        private Random _random = new Random();
        private int _loopDepth;
        private bool _exitRequested;
        private bool _stopRequested;
        private int _stoppedDepth;

        public PostScriptEvaluator(ReadOnlySpan<byte> code, bool appendExec, ILogger<PostScriptEvaluator> logger)
        {
            _logger = logger;
            _tokens = Tokenize(code);
            string codeString = Encoding.ASCII.GetString(code.ToArray());

            if (appendExec)
            {
                _tokens.Add(new PostScriptExecutableName("exec"));
            }

            _dictStack.Push(_systemDict); // systemdict bottom
            _dictStack.Push(_userDict); // userdict top
            _loopDepth = 0;
            _exitRequested = false;
            _stopRequested = false;
            _stoppedDepth = 0;
        }

        /// <summary>
        /// Attempts to compile the root tokens (procedure) into a math-only vector delegate using expression trees.
        /// Only supports operators used by PDF PostScript functions. Returns false if unsupported tokens are found.
        /// </summary>
        /// <param name="parameterNames">Input parameter names in order.</param>
        /// <param name="fn">Compiled delegate if successful; otherwise null.</param>
        /// <returns>True if compilation succeeded; otherwise false.</returns>
        public bool TryCompile(IReadOnlyList<string> parameterNames, out Func<float[], float[]> fn)
        {
            fn = null;
            if (parameterNames == null || parameterNames.Count == 0)
            {
                return false;
            }

            // The root program should have a single procedure to execute (due to appended exec).
            // Extract the first procedure or build a synthetic one from tokens.
            PostScriptProcedure rootProc = null;
            foreach (PostScriptToken token in _tokens)
            {
                if (token is PostScriptProcedure proc)
                {
                    rootProc = proc;
                    break;
                }
            }

            if (rootProc == null)
            {
                // Fallback: wrap all tokens as a procedure (common for simple streams).
                rootProc = new PostScriptProcedure(_tokens.ToList());
            }

            var compiler = new PostScriptExpressionCompiler();
            bool ok = compiler.TryCompileMath(rootProc, parameterNames, out var compiled);
            if (!ok || compiled == null)
            {
                return false;
            }

            fn = compiled;
            return true;
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
        /// Evaluate the program's root tokens against a stack.
        /// </summary>
        public void EvaluateTokens(Stack<PostScriptToken> stack)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            _loopDepth = 0;
            _exitRequested = false;
            _stopRequested = false;
            _stoppedDepth = 0;

            EvaluateTokens(_tokens, stack);
        }

        /// <summary>
        /// Evaluate tokens against a stack.
        /// </summary>
        private void EvaluateTokens(List<PostScriptToken> tokens, Stack<PostScriptToken> stack)
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
                    case PostScriptDictionary dictionary:
                    {
                        stack.Push(dictionary);
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

            if (TryProcessCollectionOperator(name, stack))
            {
                return;
            }

            // File-related stub operators (currentfile, closefile, eexec)
            if (TryExecuteIOOperator(name, stack))
            {
                return;
            }

            if (TryProcessCMapOperator(name, stack))
            {
                return;
            }

            if (TryProcessResourceOperator(name, stack))
            {
                return;
            }

            switch (name)
            {
                case "systemdict":
                {
                    stack.Push(_systemDict);
                    break;
                }
                case "userdict":
                {
                    stack.Push(_userDict);
                    break;
                }
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
                    stack.Push(new PostScriptSave());
                    break;
                }
                case "restore":
                {
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
                    // push as literal name instead
                    stack.Push(new PostScriptLiteralName(name));
                    break;
                }
            }
        }
    }
}
