using System.Collections.Generic;

namespace PdfReader.PostScript.Tokens
{
    public interface IPostScriptCollection
    {
        IReadOnlyList<PostScriptToken> Items { get; }
    }
}
