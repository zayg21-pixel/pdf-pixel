namespace PdfPixel.PdfPanel.Text;

/// <summary>
/// A run of consecutive characters on a page, as indexes into the page's extracted characters.
/// </summary>
public readonly struct PdfPanelTextRange
{
    /// <summary>
    /// Initializes a range of <paramref name="length"/> characters starting at <paramref name="startIndex"/>
    /// on the given 1-based page number.
    /// </summary>
    public PdfPanelTextRange(int pageNumber, int startIndex, int length)
    {
        PageNumber = pageNumber;
        StartIndex = startIndex;
        Length = length;
    }

    /// <summary>
    /// 1-based page number the range is on.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Index of the first character of the range in the page's characters.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// Number of characters in the range.
    /// </summary>
    public int Length { get; }
}
