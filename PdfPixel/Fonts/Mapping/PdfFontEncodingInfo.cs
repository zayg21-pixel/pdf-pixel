using System.Collections.Generic;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Holds encoding information parsed from a PDF font dictionary.
/// </summary>
public class PdfFontEncodingInfo
{
    /// <summary>
    /// Initializes a new <see cref="PdfFontEncodingInfo"/> instance with the specified base encoding, optional custom encoding name, and optional differences.
    /// </summary>
    /// <param name="encoding">The resolved base encoding, or <see cref="PdfFontEncoding.Unknown"/> when not present in the font dictionary.</param>
    /// <param name="customEncoding">The raw encoding name for custom or unrecognised encodings; empty when a standard encoding is used.</param>
    /// <param name="differences">
    /// A map from character code to glyph name representing the /Differences array; may be <see langword="null"/>, in which case an empty dictionary is used.
    /// </param>
    public PdfFontEncodingInfo(PdfFontEncoding encoding, in PdfString customEncoding, Dictionary<int, PdfString>? differences)
    {
        BaseEncoding = encoding;
        CustomEncoding = customEncoding;
        Differences = differences ?? new Dictionary<int, PdfString>();
    }

    /// <summary>
    /// The resolved base encoding enum, or Unknown if not present.
    /// </summary>
    public PdfFontEncoding BaseEncoding { get; private set; }

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
    public void UpdateEncoding(PdfFontEncoding baseEncoding) => BaseEncoding = baseEncoding;

    /// <summary>
    /// Merges glyph names from the encoding vector into differences, adding only codes that are
    /// not already resolved by <see cref="Differences"/> or by <see cref="BaseEncoding"/>.
    /// </summary>
    /// <param name="encodingVector">Encoding vector to merge.</param>
    public void MergeCodeToName(PdfString[]? encodingVector)
    {
        if (encodingVector == null || encodingVector.Length == 0)
        {
            return;
        }

        for (int code = 0; code < encodingVector.Length; code++)
        {
            if (Differences.ContainsKey(code))
            {
                continue;
            }

            if (!SingleByteEncodings.GetNameByCode((byte)code, BaseEncoding).IsEmpty)
            {
                continue;
            }

            PdfString glyphName = encodingVector[code];
            if (!glyphName.IsEmpty && glyphName != SingleByteEncodings.UndefinedCharacter)
            {
                Differences[code] = glyphName;
            }
        }
    }
}
