using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Procedure ( { ... } ) token; holds nested token list.
    /// Access level currently unused (future: executeonly / noaccess for hiding contents).
    /// </summary>
    public sealed class PostScriptProcedure : PostScriptToken, IPostScriptCollection
    {
        public PostScriptProcedure(List<PostScriptToken> tokens)
        {
            Tokens = tokens;
        }
        public List<PostScriptToken> Tokens { get; }

        public IReadOnlyList<PostScriptToken> Items => Tokens;

        public override string ToString()
        {
            int count = Tokens == null ?0 : Tokens.Count;
            return "Procedure(count=" + count + ", access=" + AccessLevel + ")";
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
