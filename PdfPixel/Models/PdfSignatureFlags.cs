using System;

namespace PdfPixel.Models;

/// <summary>
/// Document-level characteristics of signature fields (interactive form /SigFlags).
/// </summary>
[Flags]
public enum PdfSignatureFlags
{
    /// <summary>
    /// No flag set.
    /// </summary>
    None = 0,

    /// <summary>
    /// The document contains at least one signature field.
    /// </summary>
    SignaturesExist = 1 << 0,

    /// <summary>
    /// The document contains signatures that may be invalidated if saved in a way that alters its previous contents.
    /// </summary>
    AppendOnly = 1 << 1
}
