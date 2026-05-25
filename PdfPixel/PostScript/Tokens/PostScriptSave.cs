using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Represents a save state object pushed by 'save'. Restore not implemented, placeholder only.
    /// </summary>
    public sealed class PostScriptSave : PostScriptToken
    {
        public override string ToString() => "Save";

        public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
}
