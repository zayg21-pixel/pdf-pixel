using PdfRender.PostScript.Tokens;
using System.Collections.Generic;

namespace PdfRender.PostScript
{
    public partial class PostScriptEvaluator
    {
        private bool TryProcessCollectionOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                // Array / object helpers
                case "array":
                {
                    CreateArray(stack);
                    return true;
                }
                case "dict":
                {
                    DictOperator(stack);
                    return true;
                }
                case "def":
                {
                    DefOperator(stack);
                    return true;
                }
                case "put":
                {
                    SetOperator(stack); // renamed from ArrayPut to reflect generic composite put
                    return true;
                }
                // Dictionary operators
                case "begin":
                {
                    // PostScript: <dict> begin
                    var dictToken = PopOfType<PostScriptDictionary>(stack);
                    dictToken.EnsureAccess(PostScriptAccessOperation.Read); // Require readable dictionary (we don't mutate here).
                    _dictStack.Push(dictToken);
                    return true;
                }
                case "get":
                {
                    GetOperator(stack);
                    return true;
                }
                case "where":
                {
                    WhereOperator(stack);
                    return true;
                }
                case "known":
                {
                    KnownOperator(stack);
                    return true;
                }
                case "currentdict":
                {
                    PostScriptDictionary top = _dictStack.Peek();
                    top.EnsureAccess(PostScriptAccessOperation.Read);
                    stack.Push(top);

                    return true;
                }
                case "end":
                {
                    _dictStack.Pop();
                    return true;
                }
            }
            return false;
        }

        private void CreateArray(Stack<PostScriptToken> stack)
        {
            // PostScript: <n> array creates array with length n (filled with default null tokens conceptually).
            var capacityNumber = PopOfType<PostScriptNumber>(stack);
            int capacity = (int)capacityNumber.Value;
            if (capacity < 0)
            {
                capacity = 0;
            }
            stack.Push(new PostScriptArray(capacity));
        }

        private void SetOperator(Stack<PostScriptToken> stack)
        {
            // Generic PostScript put: <composite> <keyOrIndex> <value> put
            Ensure(stack, 3);
            PostScriptToken value = stack.Pop();
            PostScriptToken keyOrIndex = stack.Pop();
            PostScriptToken composite = stack.Pop();

            // Directly delegate to composite; unsupported types will throw from SetValue base implementation.
            composite.SetValue(keyOrIndex, value);
        }

        private void DefOperator(Stack<PostScriptToken> stack)
        {
            Ensure(stack, 2);
            PostScriptToken valueToken = stack.Pop();
            PostScriptLiteralName nameToken = PopOfType<PostScriptLiteralName>(stack);

            PostScriptDictionary topDict = _dictStack.Peek();
            topDict.EnsureAccess(PostScriptAccessOperation.Modify);
            topDict.Entries[nameToken.Name] = valueToken;
        }

        private void DictOperator(Stack<PostScriptToken> stack)
        {
            // PostScript: <n> dict -> new dictionary with max length hint n (ignored here)
            PopOfType<PostScriptNumber>(stack); // size hint ignored
            stack.Push(new PostScriptDictionary());
        }

        private void GetOperator(Stack<PostScriptToken> stack)
        {
            // PostScript: <composite> <keyOrIndex> get
            Ensure(stack, 2);
            PostScriptToken keyOrIndex = stack.Pop();
            PostScriptToken composite = stack.Pop();
            // Directly delegate; unsupported types will throw from GetValue base implementation.
            PostScriptToken value = composite.GetValue(keyOrIndex);
            stack.Push(value);
        }

        private void WhereOperator(Stack<PostScriptToken> stack)
        {
            // PostScript: <name> where -> <dict> true | false
            var nameToken = PopOfType<PostScriptLiteralName>(stack); // Spec-compliant: only literal names.
            string lookup = nameToken.Name;
            foreach (PostScriptDictionary dict in _dictStack)
            {
                dict.EnsureAccess(PostScriptAccessOperation.Read);
                if (dict.Entries.ContainsKey(lookup))
                {
                    stack.Push(dict);
                    stack.Push(new PostScriptBoolean(true));
                    return;
                }
            }
            stack.Push(new PostScriptBoolean(false));
        }

        private void KnownOperator(Stack<PostScriptToken> stack)
        {
            // PostScript: <dict> <name> known -> bool (popping reversed order)
            Ensure(stack, 2);
            var nameToken = PopOfType<PostScriptLiteralName>(stack);
            var dict = PopOfType<PostScriptDictionary>(stack);
            dict.EnsureAccess(PostScriptAccessOperation.Read);
            bool exists = dict.Entries.ContainsKey(nameToken.Name);
            stack.Push(new PostScriptBoolean(exists));
        }
    }
}
