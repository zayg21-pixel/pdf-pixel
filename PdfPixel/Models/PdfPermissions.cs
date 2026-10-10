using System;

namespace PdfPixel.Models;

/// <summary>
/// User access permissions of an encrypted document (encryption dictionary /P).
/// </summary>
[Flags]
public enum PdfPermissions
{
    /// <summary>
    /// No permission granted.
    /// </summary>
    None = 0,

    /// <summary>
    /// Print the document, possibly not at the highest quality level (bit 3).
    /// </summary>
    Print = 1 << 2,

    /// <summary>
    /// Modify the contents by operations other than those controlled by the other flags (bit 4).
    /// </summary>
    Modify = 1 << 3,

    /// <summary>
    /// Copy or otherwise extract text and graphics (bit 5).
    /// </summary>
    Copy = 1 << 4,

    /// <summary>
    /// Add or modify text annotations and fill in form fields (bit 6).
    /// </summary>
    Annotate = 1 << 5,

    /// <summary>
    /// Fill in existing form fields, including signature fields (bit 9).
    /// </summary>
    FillForms = 1 << 8,

    /// <summary>
    /// Assemble the document: insert, rotate, or delete pages and create outline items or thumbnails (bit 11).
    /// </summary>
    Assemble = 1 << 10,

    /// <summary>
    /// Print to a representation from which a faithful digital copy could be generated (bit 12).
    /// </summary>
    HighQualityPrint = 1 << 11
}
