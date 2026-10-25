using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Fonts;

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
    /// Parses font widths for a single-byte font from a PDF dictionary.
    /// All widths are stored in user space units (PDF spec: multiply by WidthToUserSpaceCoeff).
    /// </summary>
    /// <param name="fontDictionary">PDF dictionary containing the font definition.</param>
    internal PdfSingleByteFontWidths(PdfDictionary fontDictionary)
    {
        FirstChar = (uint?)fontDictionary.GetInteger(PdfTokens.FirstCharKey);
        LastChar = (uint?)fontDictionary.GetInteger(PdfTokens.LastCharKey);
        float[]? rawWidthsArray = fontDictionary.GetArray(PdfTokens.WidthsKey)?.GetFloatArray();

        if (rawWidthsArray != null)
        {
            Widths = new float?[rawWidthsArray.Length];
            for (int i = 0; i < rawWidthsArray.Length; i++)
            {
                Widths[i] = rawWidthsArray[i] * WidthToUserSpaceCoeff;
            }

            HasWidths = rawWidthsArray.Length > 0;
        }
    }

    /// <summary>
    /// First character code for single-byte fonts. Null if not defined.
    /// </summary>
    public uint? FirstChar { get; }

    /// <summary>
    /// Last character code for single-byte fonts. Null if not defined.
    /// </summary>
    public uint? LastChar { get; }

    /// <summary>
    /// Widths array for single-byte fonts. Null if not defined. An individual entry is null when that
    /// code has no assigned width, as distinct from a code whose width is genuinely zero.
    /// </summary>
    public float?[]? Widths { get; }

    /// <summary>
    /// <see langword="true"/> when at least one code in <see cref="Widths"/> has an assigned width.
    /// </summary>
    public bool HasWidths { get; }

    /// <summary>
    /// Gets the width for the given character code. Returns explicit width if defined, otherwise null.
    /// All widths are returned in user space units.
    /// </summary>
    public float? GetWidth(in PdfCharacterCode code)
    {
        uint cid = code.Value;
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
    internal void RescaleWidths(float scale)
    {
        if (Widths != null)
        {
            for (int i = 0; i < Widths.Length; i++)
            {
                Widths[i] *= scale;
            }
        }
    }
}
