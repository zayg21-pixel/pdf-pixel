using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Represents a single cmap subtable entry in the font.
/// </summary>
public class SfntCMapEntry
{
    public SfntCMapEntry(ushort format, int offset, PdfFontEncoding? encoding)
    {
        Format = format;
        Offset = offset;
        Encoding = encoding;
    }

    /// <summary>
    /// The format number of the cmap subtable (e.g., 0, 4, 6).
    /// </summary>
    public ushort Format { get; }

    /// <summary>
    /// The offset to the subtable in the cmap table data.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// The encoding for this subtable, if detected; otherwise null.
    /// </summary>
    public PdfFontEncoding? Encoding { get; }
}
