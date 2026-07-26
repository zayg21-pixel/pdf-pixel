using PdfPixel.Fonts.Typeface;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// The source a "cmap" table's bytes are read from: the font's stream, together with the "cmap"
/// table's byte range within it. Lets a subtable's mapping be parsed on demand, reading only the
/// bytes that one subtable actually needs.
/// </summary>
public readonly struct SfntCmapSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCmapSource"/> struct.
    /// </summary>
    /// <param name="stream">The font's source stream.</param>
    /// <param name="cmapRecord">The "cmap" table's byte range within <paramref name="stream"/>.</param>
    public SfntCmapSource(ReadOnlyFontStream stream, in SfntTableRecord cmapRecord)
    {
        Stream = stream;
        CmapRecord = cmapRecord;
    }

    /// <summary>
    /// Gets the font's source stream.
    /// </summary>
    public ReadOnlyFontStream Stream { get; }

    /// <summary>
    /// Gets the "cmap" table's byte range within <see cref="Stream"/>.
    /// </summary>
    public SfntTableRecord CmapRecord { get; }
}
