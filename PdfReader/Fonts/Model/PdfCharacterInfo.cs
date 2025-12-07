using PdfReader.Fonts.Mapping;
using SkiaSharp;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Holds all resolved information for a single PDF character code.
/// </summary>
public struct PdfCharacterInfo
{
    /// <summary>
    /// Creates a PdfCharacterInfo for a single Unicode, GID, width, etc..
    /// </summary>
    public PdfCharacterInfo(
        PdfCharacterCode characterCode,
        SKTypeface typeface,
        string unicode,
        ushort[] gid,
        float originalWidth,
        float[] width,
        VerticalMetric displacement)
    {
        CharacterCode = characterCode;
        Typeface = typeface;
        Unicode = unicode;
        Gid = gid;
        OriginalWidth = originalWidth;
        Width = width;
        Displacement = displacement;
    }

    /// <summary>
    /// The original character code.
    /// </summary>
    public PdfCharacterCode CharacterCode { get; }

    /// <summary>
    /// Typeface used to shape this character.
    /// </summary>
    public SKTypeface Typeface { get; set; }

    /// <summary>
    /// The Unicode string for this character code.
    /// </summary>
    public string Unicode { get; }

    /// <summary>
    /// The glyph ID collection for this character code.
    /// </summary>
    public ushort[] Gid { get; }

    /// <summary>
    /// Original character width defined in PDF font.
    /// </summary>
    public float OriginalWidth { get; }

    /// <summary>
    /// Shaped width collection for current code.
    /// </summary>
    public float[] Width { get; }

    /// <summary>
    /// Displacement metric for vertical character.
    /// </summary>
    public VerticalMetric Displacement { get; }
}
