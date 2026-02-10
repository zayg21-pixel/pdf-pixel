using PdfPixel.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfPixel.PostScript
{
    partial class PostScriptEvaluator
    {
        private bool TryProcessCMapOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "begincmap":
                {
                    // No operation
                    return true;
                }
                case "endcmap":
                {
                    // No operation
                    return true;
                }
                case "begincodespacerange":
                {
                    PushCMapOperatorBack(stack, "begincodespacerange");
                    return true;
                }
                case "endcodespacerange":
                {
                    PushCMapTokensToDictionary(stack, "begincodespacerange", "codespacerange");

                    return true;
                }
                case "beginbfchar":
                {
                    PushCMapOperatorBack(stack, "beginbfchar");
                    return true;
                }
                case "endbfchar":
                {
                    PushCMapTokensToDictionary(stack, "beginbfchar", "bfchar");
                    return true;
                }
                case "beginbfrange":
                {
                    PushCMapOperatorBack(stack, "beginbfrange");
                    return true;
                }
                case "endbfrange":
                {
                    PushCMapTokensToDictionary(stack, "beginbfrange", "bfrange");
                    return true;
                }
                case "begincidchar":
                {
                    PushCMapOperatorBack(stack, "begincidchar");
                    return true;
                }
                case "endcidchar":
                {
                    PushCMapTokensToDictionary(stack, "begincidchar", "cidchar");
                    return true;
                }
                case "begincidrange":
                {
                    PushCMapOperatorBack(stack, "begincidrange");
                    return true;
                }
                case "endcidrange":
                {
                    PushCMapTokensToDictionary(stack, "begincidrange", "cidrange");
                    return true;
                }
                case "beginnotdefrange":
                {
                    PushCMapOperatorBack(stack, "beginnotdefrange");
                    return true;
                }
                case "endnotdefrange":
                {
                    PushCMapTokensToDictionary(stack, "beginnotdefrange", "notdefrange");
                    return true;
                }
                case "usecmap":
                {
                    Ensure(stack, 1);
                    PostScriptToken cmapNameToken = stack.Pop();

                    PostScriptDictionary topDict = _dictStack.Peek();
                    topDict.EnsureAccess(PostScriptAccessOperation.Modify);

                    AddCMapToUseCMapArray(topDict, cmapNameToken);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static void PushCMapOperatorBack(Stack<PostScriptToken> stack, string cmapOperator)
        {
            Ensure(stack, 1);
            stack.Pop(); // entries count, not needed
            stack.Push(new PostScriptExecutableName(cmapOperator));
        }

        private void PushCMapTokensToDictionary(Stack<PostScriptToken> stack, string expectedName, string key)
        {
            List<PostScriptToken> tokens = new List<PostScriptToken>();
            while (stack.Count > 0)
            {
                PostScriptToken token = stack.Pop();
                if (token is PostScriptExecutableName nameToken && nameToken.Name == expectedName)
                {
                    break;
                }
                tokens.Add(token);
            }

            PostScriptDictionary topDict = _dictStack.Peek();
            topDict.EnsureAccess(PostScriptAccessOperation.Modify);

            var tokenArray = tokens.ToArray();
            Array.Reverse(tokenArray);
            var groupArray = new PostScriptArray(tokenArray);

            if (topDict.Entries.TryGetValue(key, out PostScriptToken existingToken) && existingToken is PostScriptArray existingArray)
            {
                // Merge with existing array of arrays
                var merged = new List<PostScriptToken>(existingArray.Elements.Length + 1);
                merged.AddRange(existingArray.Elements);
                merged.Add(groupArray);
                topDict.Entries[key] = new PostScriptArray(merged.ToArray());
            }
            else
            {
                // Create new array of arrays
                topDict.Entries[key] = new PostScriptArray([groupArray]);
            }
        }


        private static void AddCMapToUseCMapArray(PostScriptDictionary dict, PostScriptToken cmapToken)
        {
            if (dict.Entries.TryGetValue("usecmap", out PostScriptToken existingToken) && existingToken is PostScriptArray existingArray)
            {
                var merged = new List<PostScriptToken>(existingArray.Elements.Length + 1);
                merged.AddRange(existingArray.Elements);
                merged.Add(cmapToken);
                dict.Entries["usecmap"] = new PostScriptArray(merged.ToArray());
            }
            else
            {
                dict.Entries["usecmap"] = new PostScriptArray([cmapToken]);
            }
        }
    }
}
