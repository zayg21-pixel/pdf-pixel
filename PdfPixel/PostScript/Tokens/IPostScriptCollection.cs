using System.Collections.Generic;

namespace PdfPixel.PostScript.Tokens
{
    public interface IPostScriptCollection
    {
        IReadOnlyList<PostScriptToken> Items { get; }
    }
}
