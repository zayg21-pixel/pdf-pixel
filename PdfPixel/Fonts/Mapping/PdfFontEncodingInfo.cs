using System.Collections.Generic;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using PdfPixel.Models;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Holds encoding information parsed from a PDF font dictionary.
/// </summary>
public class PdfFontEncodingInfo
{
    /// <summary>
    /// Initializes a new <see cref="PdfFontEncodingInfo"/> instance with the specified base encoding, optional custom encoding name, and optional differences.
    /// </summary>
    /// <param name="encoding">The resolved base encoding, or <see cref="PdfEncoding.Unknown"/> when not present in the font dictionary.</param>
    /// <param name="customEncoding">The raw encoding name for custom or unrecognised encodings; empty when a standard encoding is used.</param>
    /// <param name="differences">
    /// A map from character code to glyph name representing the /Differences array; may be <see langword="null"/>, in which case an empty dictionary is used.
    /// </param>
    public PdfFontEncodingInfo(PdfEncoding encoding, in PdfString customEncoding, Dictionary<int, PdfString>? differences)
    {
        BaseEncoding = encoding;
        CustomEncoding = customEncoding;
        Differences = differences ?? new Dictionary<int, PdfString>();
    }

    /// <summary>
    /// The resolved base encoding enum, or Unknown if not present.
    /// </summary>
    public PdfEncoding BaseEncoding { get; private set; }

    /// <summary>
    /// Custom encoding name (when Encoding == Custom). For name-based encodings not recognized.
    /// </summary>
    public PdfString CustomEncoding { get; }

    /// <summary>
    /// Differences array parsed from /Encoding dictionary as a code -> glyph name map.
    /// Empty for name-based encodings or when not present. Empty otherwise.
    /// </summary>
    public Dictionary<int, PdfString> Differences { get; }

    /// <summary>
    /// Updates the base encoding without modifying differences.
    /// </summary>
    /// <param name="baseEncoding">Base encoding to apply.</param>
    public void UpdateEncoding(PdfEncoding baseEncoding) => BaseEncoding = baseEncoding;

    /// <summary>
    /// Gets the glyph name for a code, consulting <see cref="Differences"/> first and falling back
    /// to the base encoding's standard glyph table.
    /// </summary>
    /// <param name="code">The single-byte code to look up.</param>
    /// <returns>The glyph name for the code, or <see cref="PdfFontString.Empty"/> if not found.</returns>
    public PdfFontString GetNameByCode(byte code)
    {
        if (Differences.TryGetValue(code, out PdfString differenceName) && !differenceName.IsEmpty)
        {
            return differenceName.ToPdfFontString();
        }

        return SingleByteEncodings.GetNameByCode(code, BaseEncoding.ToPdfFontEncoding());
    }

    /// <summary>
    /// Gets the glyph name for a code, consulting <see cref="Differences"/> first and falling back
    /// to the base encoding's standard glyph table, or <see cref="SingleByteEncodings.UndefinedCharacter"/> if not found.
    /// </summary>
    /// <param name="code">The single-byte code to look up.</param>
    /// <returns>The glyph name for the code, or <see cref="SingleByteEncodings.UndefinedCharacter"/> if not found.</returns>
    public PdfFontString GetNameByCodeOrUndefined(byte code)
    {
        PdfFontString result = GetNameByCode(code);
        return (result.IsEmpty) ? SingleByteEncodings.UndefinedCharacter : result;
    }

    /// <summary>
    /// Merges glyph names from the encoding vector into differences, adding only codes that are
    /// not already resolved by <see cref="Differences"/> or by <see cref="BaseEncoding"/>.
    /// </summary>
    /// <param name="encodingVector">Encoding vector to merge.</param>
    public void MergeCodeToName(PdfFontString[]? encodingVector)
    {
        if (encodingVector == null || encodingVector.Length == 0)
        {
            return;
        }

        PdfFontEncoding baseEncoding = BaseEncoding.ToPdfFontEncoding();

        for (int code = 0; code < encodingVector.Length; code++)
        {
            if (Differences.ContainsKey(code))
            {
                continue;
            }

            if (!SingleByteEncodings.GetNameByCode((byte)code, baseEncoding).IsEmpty)
            {
                continue;
            }

            PdfFontString glyphName = encodingVector[code];
            if (!glyphName.IsEmpty && glyphName != SingleByteEncodings.UndefinedCharacter)
            {
                Differences[code] = glyphName.ToPdfString();
            }
        }
    }
}
