using System.Collections.Generic;

namespace PdfPixel.PostScript.Tokens;

/// <summary>
/// Marks a PostScript token that holds an ordered sequence of child tokens, such as arrays and procedures.
/// </summary>
public interface IPostScriptCollection
{
    /// <summary>
    /// Gets the ordered list of tokens contained in this collection.
    /// </summary>
    IReadOnlyList<PostScriptToken> Items { get; }
}
