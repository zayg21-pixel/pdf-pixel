using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// PostScript dictionary object (literal &lt;&lt; ... &gt;&gt; or created via 'dict').
    /// </summary>
    public sealed class PostScriptDictionary : PostScriptToken
    {
        /// <summary>
        /// Initializes an empty PostScript dictionary with a case-insensitive getter backing store.
        /// </summary>
        public PostScriptDictionary() => Entries = new CaseInsensitiveGetterDictionary<PostScriptToken>();

        /// <summary>
        /// Initializes a PostScript dictionary populated with the entries from <paramref name="entries"/>.
        /// Keys are copied using their original casing into a case-insensitive getter backing store.
        /// </summary>
        /// <param name="entries">Initial key-value pairs to populate the dictionary.</param>
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

        /// <summary>
        /// Gets the key-value store backing this PostScript dictionary. Keys support case-insensitive lookup.
        /// </summary>
        public IDictionary<string, PostScriptToken> Entries { get; }

        /// <summary>
        /// Returns a diagnostic string showing the number of entries and the current access level.
        /// </summary>
        public override string ToString()
        {
            int count = (Entries?.Count) ?? 0;
            return "Dictionary(count=" + count + ", access=" + AccessLevel + ")";
        }

        /// <summary>
        /// Returns true only when <paramref name="other"/> is the same object instance, matching PostScript dictionary identity semantics.
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

        /// <summary>
        /// Returns a hash code based on object identity.
        /// </summary>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

        /// <summary>
        /// Retrieves the value associated with a literal name key. Throws if the key is not a literal name
        /// or is not present in the dictionary.
        /// </summary>
        /// <param name="keyOrIndex">A <see cref="PostScriptLiteralName"/> token used as the lookup key.</param>
        /// <returns>The token stored under the specified key.</returns>
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

        /// <summary>
        /// Stores or replaces a value under a literal name key. Throws if the key is not a literal name
        /// or if the access level prohibits modification.
        /// </summary>
        /// <param name="keyOrIndex">A <see cref="PostScriptLiteralName"/> token used as the storage key.</param>
        /// <param name="value">The token value to store.</param>
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
