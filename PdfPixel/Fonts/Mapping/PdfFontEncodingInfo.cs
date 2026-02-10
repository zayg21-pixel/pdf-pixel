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
    public PdfFontEncodingInfo(PdfFontEncoding encoding, PdfString customEncoding, Dictionary<int, PdfString> differences)
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
    /// Updates the differences map from the given encoding vector.
    /// </summary>
    /// <param name="encodingVector">Encoding vector.</param>
    public void Update(PdfFontEncoding baseEncoding, PdfString[] encodingVector)
    {
        if (baseEncoding != PdfFontEncoding.Unknown)
        {
            BaseEncoding = baseEncoding;
        }

        if (encodingVector == null || encodingVector.Length == 0)
        {
            return;
        }

        for (int code = 0; code < encodingVector.Length; code++)
        {
            PdfString glyphName = encodingVector[code];

            if (!glyphName.IsEmpty && glyphName != SingleByteEncodings.UndefinedCharacter)
            {
                Differences[code] = glyphName;
            }
        }
    }
}
