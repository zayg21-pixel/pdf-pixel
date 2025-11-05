using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfReader.PostScript
{
    /// <summary>
    /// Helper method implementations for operator execution (extracted from main evaluator for clarity).
    /// </summary>
    public partial class PostScriptEvaluator
    {
        private static void Ensure(Stack<PostScriptToken> stack, int required)
        {
            if (stack.Count < required)
            {
                throw new InvalidOperationException("Stack underflow");
            }
        }

        private static T PopOfType<T>(Stack<PostScriptToken> stack) where T : PostScriptToken
        {
            Ensure(stack, 1);
            PostScriptToken token = stack.Pop();
            if (token is not T tValue)
            {
                throw new InvalidOperationException("Type mismatch: expected " + typeof(T).Name + ", got " + token.GetType().Name);
            }

            return tValue;
        }

        private void AccessSetOperator(Stack<PostScriptToken> stack, PostScriptAccess access)
        {
            Ensure(stack, 1);
            PostScriptToken top = stack.Pop();
            top.SetAccessLevel(access);
            stack.Push(top);
        }

        private void LoadOperator(Stack<PostScriptToken> stack)
        {
            Ensure(stack, 1);
            PostScriptToken nameToken = stack.Pop();
            string lookupName = null;
            if (nameToken is PostScriptLiteralName litName)
            {
                lookupName = litName.Name;
            }
            else if (nameToken is PostScriptExecutableName execName)
            {
                lookupName = execName.Name;
            }
            if (lookupName == null)
            {
                stack.Push(nameToken);
                return;
            }
            if (TryLookupDict(lookupName, out PostScriptToken valueToken))
            {
                if (valueToken is PostScriptDictionary d && d.AccessLevel == PostScriptAccess.NoAccess)
                {
                    // Cannot load from noaccess dictionary.
                    stack.Push(nameToken);
                    return;
                }
                stack.Push(valueToken);
            }
            else
            {
                stack.Push(nameToken);
            }
        }
    }
}
