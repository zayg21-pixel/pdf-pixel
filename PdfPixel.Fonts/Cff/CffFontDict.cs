namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Represents a single Font DICT entry from a CID-keyed font's FDArray: a mini Top DICT (FontMatrix,
/// FontName, Private) together with its own resolved Private DICT.
/// </summary>
public class CffFontDict
{
    /// <summary>
    /// Gets or sets the parsed Font DICT (the same operator set as a Top DICT; only a subset is
    /// typically present -- FontMatrix, FontName, Private).
    /// </summary>
    public CffTopDict TopDict { get; set; } = new();

    /// <summary>
    /// Gets or sets this Font DICT's resolved Private DICT.
    /// </summary>
    public CffPrivateDict? PrivateDict { get; set; }
}
