using System;

namespace PdfPixel.Navigation;

/// <summary>
/// Style characteristics for displaying an outline item's text (/F).
/// </summary>
[Flags]
public enum PdfOutlineItemFlags
{
    /// <summary>
    /// No flag set.
    /// </summary>
    None = 0,

    /// <summary>
    /// The item's text is displayed in italic.
    /// </summary>
    Italic = 1 << 0,

    /// <summary>
    /// The item's text is displayed in bold.
    /// </summary>
    Bold = 1 << 1
}
