using System.Collections.Generic;

namespace PdfRender.PostScript.Tokens
{
    public interface IPostScriptCollection
    {
        IReadOnlyList<PostScriptToken> Items { get; }
    }
}
