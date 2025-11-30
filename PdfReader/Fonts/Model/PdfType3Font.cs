using PdfReader.Fonts.Mapping;
using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;
using System;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Type 3 fonts (user-defined fonts)
/// Contains custom glyph definitions in PDF content streams
/// Each character is defined by a PDF content stream that draws the glyph
/// Limited to 256 characters (single-byte encoding)
/// </summary>
public class PdfType3Font : PdfSingleByteFont
{
    /// <summary>
    /// Constructor for Type3 fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    public PdfType3Font(PdfDictionary fontDictionary) : base(fontDictionary)
    {
        if (Type != PdfFontSubType.Type3)
            throw new ArgumentException("Font dictionary must be Type3");

        // Get CharProcs dictionary - essential for Type3 fonts
        CharProcs = Dictionary.GetDictionary(PdfTokens.CharProcsKey);

        // Get FontMatrix (required for Type3 fonts)
        FontMatrix = Dictionary.GetArray(PdfTokens.FontMatrixKey).GetFloatArray();

        if (FontMatrix == null || FontMatrix.Length != 6)
        {
            // Default font matrix if not specified or incorrect length
            FontMatrix = [0.001f, 0, 0, 0.001f, 0, 0];
        }

        // Get Resources dictionary (may be needed for character procedures)
        Resources = Dictionary.GetDictionary(PdfTokens.ResourcesKey);
    }

    internal protected override SKTypeface Typeface => null;
    
    /// <summary>
    /// Character procedures dictionary containing glyph definitions
    /// Each entry maps a character name to a content stream that draws the glyph
    /// Set during construction - lightweight operation
    /// </summary>
    public PdfDictionary CharProcs { get; }

    /// <summary>
    /// Font transformation matrix (required for Type3 fonts)
    /// Maps from glyph space to text space
    /// </summary>
    public float[] FontMatrix { get; }

    /// <summary>
    /// Resources dictionary for character procedures
    /// May contain fonts, color spaces, patterns, etc. used by glyph definitions
    /// </summary>
    public PdfDictionary Resources { get; }

    /// <summary>
    /// Type3 fonts don't have embedded font files in the traditional sense
    /// They define glyphs using PDF content streams
    /// </summary>
    public override bool IsEmbedded => false;
    
    /// <summary>
    /// Check if character procedures are available
    /// This should always be true for valid Type3 fonts
    /// </summary>
    public bool HasCharProcs => CharProcs != null;

    /// <summary>
    /// Get the content stream for a specific character
    /// Returns null if the character is not defined
    /// </summary>
    /// <param name="charCode">Character code to get glyph for</param>
    /// <returns>PDF object containing the glyph definition, or null if not found</returns>
    public PdfObject GetCharacterProcedure(byte charCode)
    {
        if (CharProcs == null)
            return null;

        // Convert character code to character name
        // This may need encoding-specific logic
        var charName = GetCharacterName(charCode);
        if (charName.IsEmpty)
            return null;

        return CharProcs.GetObject(charName);
    }

    /// <summary>
    /// Get all available character names in this Type3 font
    /// </summary>
    /// <returns>Array of character names, or empty array if CharProcs is null</returns>
    public PdfString[] GetAvailableCharacterNames()
    {
        if (CharProcs == null)
            return [];

        var names = new PdfString[CharProcs.Count];
        var i = 0;
        foreach (var key in CharProcs.RawValues.Keys)
        {
            names[i++] = key;
        }

        return names;
    }

    /// <summary>
    /// Convert character code to character name based on encoding
    /// </summary>
    private PdfString GetCharacterName(byte charCode)
    {
        return SingleByteEncodings.GetNameByCode(charCode, Encoding.BaseEncoding, Encoding.Differences);
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code in a Type3 font.
    /// Type3 fonts do not use GIDs; always returns 0.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>Always 0 for Type3 fonts.</returns>
    public override ushort GetGid(PdfCharacterCode code)
    {
        return 0;
    }
}