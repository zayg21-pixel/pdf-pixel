using System;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Executable name token (bare name without leading slash). Subject to dictionary / builtin lookup.
    /// </summary>
    public sealed class PostScriptExecutableName : PostScriptToken
    {
        /// <summary>
        /// Initializes the executable name token with the specified name.
        /// </summary>
        /// <param name="name">The bare name string as it appears in the PostScript source.</param>
        public PostScriptExecutableName(string name) => Name = name;

        /// <summary>
        /// Gets the bare name string that will be looked up in the dictionary stack or dispatched as a builtin operator.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns a diagnostic string showing the executable name enclosed in braces.
        /// </summary>
        public override string ToString() => "ExecutableName: {" + Name + "}";

        /// <summary>
        /// Returns true when <paramref name="other"/> is an executable name token with an identical name (ordinal comparison).
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => other is PostScriptExecutableName n && string.Equals(Name, n.Name, StringComparison.Ordinal);

        /// <summary>
        /// Returns a hash code derived from the name string using ordinal comparison rules.
        /// </summary>
        public override int GetHashCode() => (Name == null) ? 0 : StringComparer.Ordinal.GetHashCode(Name);

        /// <summary>
        /// Compares this token's name to another executable name's name using ordinal string ordering.
        /// Throws if <paramref name="other"/> is not an executable name token.
        /// </summary>
        /// <param name="other">The token to compare against.</param>
        /// <returns>A negative value, zero, or a positive value indicating relative ordering.</returns>
        public override int CompareToToken(PostScriptToken? other)
        {
            if (other is not PostScriptExecutableName n)
            {
                throw new InvalidOperationException("Name comparison requires name operand.");
            }

            return string.CompareOrdinal(Name, n.Name);
        }
    }
}
