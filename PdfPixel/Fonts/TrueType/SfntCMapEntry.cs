using PdfPixel.Fonts.Model;

namespace PdfPixel.Fonts.TrueType;

/// <summary>
/// Represents a single cmap subtable entry in the font.
/// </summary>
public class SfntCMapEntry
{
    /// <summary>
    /// Initializes a new <see cref="SfntCMapEntry"/> with the specified cmap subtable format, byte offset, and detected encoding.
    /// </summary>
    /// <param name="format">The cmap subtable format number (e.g., 0, 4, 6) as defined in the OpenType/TrueType specification.</param>
    /// <param name="offset">The byte offset of the subtable within the cmap table data.</param>
    /// <param name="encoding">The PDF font encoding associated with this subtable, or <see langword="null"/> when the encoding could not be determined.</param>
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
