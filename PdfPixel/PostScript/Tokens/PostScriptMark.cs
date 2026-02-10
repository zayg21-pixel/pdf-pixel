namespace PdfPixel.PostScript.Tokens
{
    /// <summary>
    /// Sentinel token representing a PostScript stack mark (produced by 'mark').
    /// Used by cleartomark and counttomark operators.
    /// </summary>
    public sealed class PostScriptMark : PostScriptToken
    {
        public static readonly PostScriptMark Instance = new PostScriptMark();

        private PostScriptMark()
        {
        }

        public override bool EqualsToken(PostScriptToken other)
        {
            return ReferenceEquals(this, other);
        }
        public override int GetHashCode()
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        }
        public override string ToString()
        {
            return "Mark";
        }
    }
}
