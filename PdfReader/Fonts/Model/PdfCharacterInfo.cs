using PdfReader.Fonts.Mapping;
using SkiaSharp;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Holds all resolved information for a single PDF character code.
/// </summary>
public struct PdfCharacterInfo
{
    /// <summary>
    /// Creates a PdfCharacterInfo for a single Unicode, GID, and width.
    /// </summary>
    public PdfCharacterInfo(
        PdfCharacterCode characterCode,
        string unicode,
        ushort[] gid,
        float[] width,
        VerticalMetric displacement)
    {
        CharacterCode = characterCode;
        Unicode = unicode;
        Gid = gid;
        Width = width;
        Displacement = displacement;
    }

    /// <summary>
    /// The original character code.
    /// </summary>
    public PdfCharacterCode CharacterCode { get; }

    /// <summary>
    /// The Unicode string for this character code.
    /// </summary>
    public string Unicode { get; }

    /// <summary>
    /// The glyph ID collection for this character code.
    /// </summary>
    public ushort[] Gid { get; }

    /// <summary>
    /// The width collection for current code.
    /// </summary>
    public float[] Width { get; }

    /// <summary>
    /// Displacement metric for vertical character.
    /// </summary>
    public VerticalMetric Displacement { get; }
}
