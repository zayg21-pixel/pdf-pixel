using PdfReader.PostScript.Tokens;
using System;
using System.Collections.Generic;

namespace PdfReader.PostScript
{
    public partial class PostScriptEvaluator
    {
        /// <summary>
        /// Handles file-related operators needed for font headers. Currently provides no-op stubs only.
        /// Upstream font parsing code already splits clear / encrypted sections around the 'currentfile eexec' boundary.
        /// Implementations here intentionally avoid real stream I/O.
        /// </summary>
        /// <param name="name">Operator name.</param>
        /// <param name="stack">Operand stack.</param>
        /// <returns>True if the operator was recognized (even if it was a no-op), otherwise false.</returns>
        public bool TryExecuteIOOperator(string name, Stack<PostScriptToken> stack)
        {
            switch (name)
            {
                case "currentfile":
                {
                    // no op, no real file handling;
                    return true;
                }
                case "closefile":
                {
                    // no op, no real file handling;
                    return true;
                }
                case "eexec":
                {
                    // Stub: encryption boundary already handled externally. No-op here.
                    return true;
                }
            }

            return false;
        }
    }
}
