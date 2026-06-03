using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.PostScript.Tokens;

/// <summary>
/// Procedure ( { ... } ) token; holds nested token list.
/// Access level currently unused (future: executeonly / noaccess for hiding contents).
/// </summary>
public sealed class PostScriptProcedure : PostScriptToken, IPostScriptCollection
{
    /// <summary>
    /// Initializes the procedure token with the given list of body tokens.
    /// </summary>
    /// <param name="tokens">The ordered list of tokens that form the procedure body.</param>
    public PostScriptProcedure(List<PostScriptToken> tokens) => Tokens = tokens;

    /// <summary>
    /// Gets the mutable list of tokens that make up the body of this procedure.
    /// </summary>
    public List<PostScriptToken> Tokens { get; }

    /// <summary>
    /// Gets the procedure body as a read-only list, satisfying <see cref="IPostScriptCollection"/>.
    /// </summary>
    public IReadOnlyList<PostScriptToken> Items => Tokens;

    /// <summary>
    /// Returns a diagnostic string showing the token count and current access level.
    /// </summary>
    public override string ToString()
    {
        int count = (Tokens?.Count) ?? 0;
        return "Procedure(count=" + count + ", access=" + AccessLevel + ")";
    }

    /// <summary>
    /// Returns true only when <paramref name="other"/> is the same object instance, matching PostScript procedure identity semantics.
    /// </summary>
    public override bool EqualsToken(PostScriptToken other) => ReferenceEquals(this, other);

    /// <summary>
    /// Returns a hash code based on object identity.
    /// </summary>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
