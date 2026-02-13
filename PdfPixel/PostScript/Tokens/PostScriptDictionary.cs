using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// PostScript dictionary object (literal << ... >> or created via 'dict').
    /// </summary>
    public sealed class PostScriptDictionary : PostScriptToken
    {
        public PostScriptDictionary()
        {
            Entries = new CaseInsensitiveGetterDictionary<PostScriptToken>();
        }
        public PostScriptDictionary(IDictionary<string, PostScriptToken> entries)
        {
            var caseInsensitiveEntries = new CaseInsensitiveGetterDictionary<PostScriptToken>();

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    caseInsensitiveEntries[entry.Key] = entry.Value;
                }

            }

            Entries = caseInsensitiveEntries;
        }

        public IDictionary<string, PostScriptToken> Entries { get; }

        public override string ToString()
        {
            int count = Entries == null ? 0 : Entries.Count;
            return "Dictionary(count=" + count + ", access=" + AccessLevel + ")";
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        public override PostScriptToken GetValue(PostScriptToken keyOrIndex)
        {
            EnsureAccess(PostScriptAccessOperation.Read);
            if (keyOrIndex is not PostScriptLiteralName literalName)
            {
                throw new InvalidOperationException("typecheck: dictionary key must be literal name");
            }
            if (!Entries.TryGetValue(literalName.Name, out PostScriptToken value))
            {
                throw new InvalidOperationException("undefined: key not found in dictionary");
            }
            return value;
        }

        public override void SetValue(PostScriptToken keyOrIndex, PostScriptToken value)
        {
            EnsureAccess(PostScriptAccessOperation.Modify);
            if (keyOrIndex is not PostScriptLiteralName literalName)
            {
                throw new InvalidOperationException("typecheck: dictionary key must be literal name");
            }
            Entries[literalName.Name] = value;
        }
    }
}
