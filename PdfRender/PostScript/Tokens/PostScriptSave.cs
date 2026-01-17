using System.Runtime.CompilerServices;

namespace PdfRender.PostScript.Tokens
{
    /// <summary>
    /// Represents a save state object pushed by 'save'. Restore not implemented, placeholder only.
    /// </summary>
    public sealed class PostScriptSave : PostScriptToken
    {
        public override string ToString()
        {
            return "Save";
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }
    }
}
