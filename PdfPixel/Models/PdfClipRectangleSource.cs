namespace PdfPixel.Models;

/// <summary>
/// Where a rectangular clip takes its rectangle from.
/// </summary>
public enum PdfClipRectangleSource
{
    /// <summary>
    /// The rectangle carried by the command, in current user space.
    /// </summary>
    Rectangle,

    /// <summary>
    /// The visible region of the page, resolved when the command executes. Clips nothing when the
    /// whole page is visible.
    /// </summary>
    Region
}
