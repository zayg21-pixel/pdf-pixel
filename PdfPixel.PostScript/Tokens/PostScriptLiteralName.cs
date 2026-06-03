using System;

namespace PdfPixel.PostScript.Tokens;

/// <summary>
/// Literal name token (source had leading '/'). Pushed as a data object.
/// </summary>
public sealed class PostScriptLiteralName : PostScriptToken
{
    /// <summary>
    /// Initializes the literal name token with the specified name.
    /// </summary>
    /// <param name="name">The name string as it appears after the leading '/' in PostScript source.</param>
    public PostScriptLiteralName(string name) => Name = name;

    /// <summary>
    /// Gets the name string. Literal names are pushed onto the stack as data objects rather than being executed.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Returns a diagnostic string showing the literal name enclosed in quotes.
    /// </summary>
    public override string ToString() => "LiteralName: {\"" + Name + "\"}";

    /// <summary>
    /// Returns true when <paramref name="other"/> is a literal name token with an identical name (ordinal comparison).
    /// </summary>
    public override bool EqualsToken(PostScriptToken other) => other is PostScriptLiteralName n && string.Equals(Name, n.Name, StringComparison.Ordinal);

    /// <summary>
    /// Returns a hash code derived from the name string using ordinal comparison rules.
    /// </summary>
    public override int GetHashCode() => (Name == null) ? 0 : StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>
    /// Compares this token's name to another literal name's name using ordinal string ordering.
    /// Throws if <paramref name="other"/> is not a literal name token.
    /// </summary>
    /// <param name="other">The token to compare against.</param>
    /// <returns>A negative value, zero, or a positive value indicating relative ordering.</returns>
    public override int CompareToToken(PostScriptToken? other)
    {
        if (other is not PostScriptLiteralName n)
        {
            throw new InvalidOperationException("Literal name comparison requires name operand.");
        }

        return string.CompareOrdinal(Name, n.Name);
    }
}
