using PdfPixel.Models;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Management;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Font width information for single-byte fonts (Type1, TrueType, MMType1, Type3).
/// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
/// </summary>
public class SingleByteFontWidths
{
    /// <summary>
    /// Coefficient to convert PDF font units to user space units.
    /// </summary>
    public const float WidthToUserSpaceCoeff = 0.001f;

    /// <summary>
    /// First character code for single-byte fonts. Null if not defined.
    /// </summary>
    public uint? FirstChar { get; set; }

    /// <summary>
    /// Last character code for single-byte fonts. Null if not defined.
    /// </summary>
    public uint? LastChar { get; set; }

    /// <summary>
    /// Widths array for single-byte fonts. Null if not defined.
    /// </summary>
    public float[]? Widths { get; set; }

    /// <summary>
    /// Gets the width for the given character code. Returns explicit width if defined, otherwise null.
    /// All widths are returned in user space units.
    /// </summary>
    public float? GetWidth(PdfCharacterCode code)
    {
        var cid = (uint)code;
        if (Widths != null && FirstChar.HasValue && LastChar.HasValue && cid >= FirstChar.Value && cid <= LastChar.Value)
        {
            uint index = cid - FirstChar.Value;
            if (index < Widths.Length)
            {
                return Widths[index];
            }
        }

        return null;
    }

    /// <summary>
    /// Applies a scaling factor to all defined widths.
    /// </summary>
    /// <param name="scale">Scale factor to apply.</param>
    public void RescaleWidths(float scale)
    {
        if (Widths != null)
        {
            for (int i = 0; i < Widths.Length; i++)
            {
                Widths[i] *= scale;
            }
        }
    }

    /// <summary>
    /// Parses font widths for a single-byte font from a PDF dictionary.
    /// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
    /// </summary>
    /// <param name="fontDictionary">PDF dictionary containing the font definition.</param>
    /// <returns>Parsed SingleByteFontWidths instance.</returns>
    internal static SingleByteFontWidths Parse(PdfDictionary fontDictionary)
    {
        var firstChar = (uint?)fontDictionary.GetInteger(PdfTokens.FirstCharKey);
        var lastChar = (uint?)fontDictionary.GetInteger(PdfTokens.LastCharKey);
        float[]? widthsArray = fontDictionary.GetArray(PdfTokens.WidthsKey)?.GetFloatArray();

        if (widthsArray != null)
        {
            for (int i = 0; i < widthsArray.Length; i++)
            {
                widthsArray[i] *= WidthToUserSpaceCoeff;
            }
        }

        return new SingleByteFontWidths
        {
            FirstChar = firstChar,
            LastChar = lastChar,
            Widths = widthsArray
        };
    }

    /// <summary>
    /// Builds the default (code 0-255) width table for a Standard 14 font, from the embedded AFM width
    /// resources. Used when a PDF font dictionary omits <c>/Widths</c>.
    /// </summary>
    /// <param name="fontName">The Standard 14 font family.</param>
    /// <param name="bold">Whether to resolve the bold style variant.</param>
    /// <param name="italic">Whether to resolve the italic/oblique style variant.</param>
    /// <param name="encoding">The font's actual encoding, matching what the glyph-ID resolution path uses.</param>
    /// <returns>The resolved widths, or <see langword="null"/> if the family or style variant is unknown.</returns>
    internal static SingleByteFontWidths? FromStandardFont(PdfStandardFontName fontName, bool bold, bool italic, PdfFontEncoding encoding)
    {
        int[]? widths = Standard14Metrics.GetWidths(fontName, bold, italic, encoding);
        if (widths == null)
        {
            return null;
        }

        var widthsArray = new float[widths.Length];
        for (int i = 0; i < widths.Length; i++)
        {
            widthsArray[i] = widths[i] * WidthToUserSpaceCoeff;
        }

        return new SingleByteFontWidths
        {
            FirstChar = 0,
            LastChar = (uint)(widths.Length - 1),
            Widths = widthsArray
        };
    }
}
