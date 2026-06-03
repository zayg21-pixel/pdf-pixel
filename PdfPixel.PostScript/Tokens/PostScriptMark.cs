namespace PdfPixel.PostScript.Tokens;

/// <summary>
/// Sentinel token representing a PostScript stack mark (produced by 'mark').
/// Used by cleartomark and counttomark operators.
/// </summary>
public sealed class PostScriptMark : PostScriptToken
{
    /// <summary>
    /// Gets the singleton mark instance. There is only one mark object; identity comparison is used for equality.
    /// </summary>
    public static readonly PostScriptMark Instance = new();

    private PostScriptMark()
    {
    }

    /// <summary>
    /// Returns true only when <paramref name="other"/> is the same singleton instance.
    /// </summary>
    public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

    /// <summary>
    /// Returns a hash code based on object identity.
    /// </summary>
    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

    /// <summary>
    /// Returns the diagnostic string "Mark".
    /// </summary>
    public override string ToString() => "Mark";
}
