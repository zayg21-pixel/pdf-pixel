using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Represents a save state object pushed by 'save'. Restore not implemented, placeholder only.
    /// </summary>
    public sealed class PostScriptSave : PostScriptToken
    {
        /// <summary>
        /// Returns the diagnostic string "Save".
        /// </summary>
        public override string ToString() => "Save";

        /// <summary>
        /// Returns true only when <paramref name="other"/> is the same object instance.
        /// </summary>
        public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

        /// <summary>
        /// Returns a hash code based on object identity.
        /// </summary>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
