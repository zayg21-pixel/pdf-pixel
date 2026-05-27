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
        public PostScriptDictionary() => Entries = new CaseInsensitiveGetterDictionary<PostScriptToken>();

        public PostScriptDictionary(IDictionary<string, PostScriptToken> entries)
        {
            CaseInsensitiveGetterDictionary<PostScriptToken> caseInsensitiveEntries = [];

            if (entries != null)
            {
                foreach (KeyValuePair<string, PostScriptToken> entry in entries)
                {
                    caseInsensitiveEntries[entry.Key] = entry.Value;
                }

            }

            Entries = caseInsensitiveEntries;
        }

        public IDictionary<string, PostScriptToken> Entries { get; }

        public override string ToString()
        {
            int count = (Entries?.Count) ?? 0;
            return "Dictionary(count=" + count + ", access=" + AccessLevel + ")";
        }

        public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

        public override PostScriptToken GetValue(PostScriptToken keyOrIndex)
        {
            EnsureAccess(PostScriptAccessOperation.Read);
            if (keyOrIndex is not PostScriptLiteralName literalName)
            {
                throw new InvalidOperationException("typecheck: dictionary key must be literal name");
            }

            if (!Entries.TryGetValue(literalName.Name, out PostScriptToken? value))
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
