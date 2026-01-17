using Microsoft.Extensions.Logging;
using PdfRender.PostScript.Tokens;
using System.Collections.Generic;

namespace PdfRender.PostScript
{
    partial class PostScriptEvaluator
    {
        public const string FontResourceCategory = "Font";

        private bool TryProcessResourceOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "findresource":
                {
                    var category = stack.Pop();
                    var resourceName = stack.Pop();
                    var categoryResource = _resources.GetValue(category);

                    if (categoryResource is PostScriptDictionary dict)
                    {
                        var resource = dict.GetValue(resourceName);
                        stack.Push(resource);
                    }
                    else
                    {
                        _logger?.LogWarning("Resource category not found: {Category}", category);
                    }

                    return true;
                }
                case "definefont":
                {
                    var value = stack.Pop();
                    var resourceName = PopOfType<PostScriptLiteralName>(stack);

                    SetResourceValue(FontResourceCategory, resourceName.Name, value);

                    value.SetAccessLevel(PostScriptAccess.ReadOnly);

                    stack.Push(value);

                    return true;
                }
                case "defineresource":
                {
                    var category = PopOfType<PostScriptLiteralName>(stack);
                    var value = stack.Pop();
                    var resourceName = PopOfType<PostScriptLiteralName>(stack);

                    SetResourceValue(category.Name, resourceName.Name, value);

                    value.SetAccessLevel(PostScriptAccess.ReadOnly);

                    stack.Push(value);

                    return true;
                }
                case "undefineresource":
                {
                    var category = PopOfType<PostScriptLiteralName>(stack);
                    var resourceName = PopOfType<PostScriptLiteralName>(stack);

                    if (_resources.Entries.TryGetValue(category.Name, out PostScriptToken categoryToken) && categoryToken is PostScriptDictionary categoryDict)
                    {
                        if (categoryDict.Entries.Remove(resourceName.Name))
                        {
                            // Resource removed
                        }
                        else
                        {
                            _logger?.LogWarning("Resource '{ResourceName}' not found in category '{Category}' for undefineresource.", resourceName.Name, category.Name);
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("Resource category not found: {Category} for undefineresource.", category.Name);
                    }
                    return true;
                }
                case "resourcestatus":
                {
                    _logger?.LogWarning("resourcestatus operator is not implemented.");
                    return true;
                }
                case "resourceforall":
                {
                    _logger?.LogWarning("resourceforall operator is not implemented.");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Add or override a resource value in the Resources dictionary.
        /// </summary>
        public void SetResourceValue(string category, string name, PostScriptToken value)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (!_resources.Entries.TryGetValue(category, out PostScriptToken categoryToken) || categoryToken is not PostScriptDictionary categoryDict)
            {
                categoryDict = new PostScriptDictionary();
                _resources.Entries[category] = categoryDict;
            }

            categoryDict.Entries[name] = value;
        }

        /// <summary>
        /// Retrieves a resource value from the Resources dictionary or null if not found.
        /// </summary>
        public PostScriptToken GetResourceValue(string category, string name)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (_resources.Entries.TryGetValue(category, out PostScriptToken categoryToken) && categoryToken is PostScriptDictionary categoryDict
                && categoryDict.Entries.TryGetValue(name, out PostScriptToken resourceValue))
            {
                return resourceValue;
            }

            return null;
        }

        /// <summary>
        /// Returns the entire resource category dictionary or null if not found.
        /// </summary>
        public PostScriptDictionary GetResourceCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return null;
            }
            if (_resources.Entries.TryGetValue(category, out PostScriptToken categoryToken) && categoryToken is PostScriptDictionary categoryDict)
            {
                return categoryDict;
            }

            return null;
        }
    }
}
