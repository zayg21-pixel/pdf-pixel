using PdfReader.Fonts.Mapping;

namespace PdfReader.Fonts.Types;

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
        ushort gid,
        float width)
    {
        CharacterCode = characterCode;
        Unicode = unicode;
        Gid = gid;
        Width = width;
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
    /// The glyph ID for this character code.
    /// </summary>
    public ushort Gid { get; }

    /// <summary>
    /// The width for current glyph.
    /// </summary>
    public float Width { get; }
}
