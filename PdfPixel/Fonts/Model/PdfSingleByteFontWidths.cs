using PdfPixel.Models;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.Management;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Font width information for single-byte fonts (Type1, TrueType, MMType1, Type3).
/// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
/// </summary>
public class PdfSingleByteFontWidths
{
    /// <summary>
    /// Coefficient to convert PDF font units to user space units.
    /// </summary>
    public const float WidthToUserSpaceCoeff = 0.001f;

    /// <summary>
    /// First character code for single-byte fonts. Null if not defined.
    /// </summary>
    public uint? FirstChar { get; private set; }

    /// <summary>
    /// Last character code for single-byte fonts. Null if not defined.
    /// </summary>
    public uint? LastChar { get; private set; }

    /// <summary>
    /// Widths array for single-byte fonts. Null if not defined. An individual entry is null when that
    /// code has no assigned width, as distinct from a code whose width is genuinely zero.
    /// </summary>
    public float?[]? Widths { get; private set; }

    /// <summary>
    /// <see langword="true"/> when at least one code in <see cref="Widths"/> has an assigned width.
    /// Computed once by <see cref="Parse"/> alongside the array itself.
    /// </summary>
    public bool HasWidths { get; private set; }

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
    internal static PdfSingleByteFontWidths Parse(PdfDictionary fontDictionary)
    {
        var firstChar = (uint?)fontDictionary.GetInteger(PdfTokens.FirstCharKey);
        var lastChar = (uint?)fontDictionary.GetInteger(PdfTokens.LastCharKey);
        float[]? rawWidthsArray = fontDictionary.GetArray(PdfTokens.WidthsKey)?.GetFloatArray();

        float?[]? widthsArray = null;
        var hasWidths = false;
        if (rawWidthsArray != null)
        {
            widthsArray = new float?[rawWidthsArray.Length];
            for (int i = 0; i < rawWidthsArray.Length; i++)
            {
                widthsArray[i] = rawWidthsArray[i] * WidthToUserSpaceCoeff;
                hasWidths = true;
            }
        }

        return new PdfSingleByteFontWidths
        {
            FirstChar = firstChar,
            LastChar = lastChar,
            Widths = widthsArray,
            HasWidths = hasWidths
        };
    }
}
