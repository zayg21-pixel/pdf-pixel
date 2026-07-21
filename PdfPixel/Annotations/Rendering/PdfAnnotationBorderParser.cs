using PdfPixel.Annotations.Models;
using PdfPixel.Color.Paint;
using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Parses annotation border style (BS dictionary / legacy Border array) and border effect
/// (BE dictionary) entries.
/// </summary>
internal static class PdfAnnotationBorderParser
{
    /// <summary>
    /// Creates a <see cref="PdfAnnotationBorderStyle"/> from a border style dictionary and/or legacy
    /// border array.
    /// </summary>
    /// <param name="borderStyleDictionary">The border style dictionary (BS entry), or null.</param>
    /// <param name="borderArray">The legacy border array (Border entry), or null.</param>
    /// <returns>A <see cref="PdfAnnotationBorderStyle"/>, or null if no border information is present.</returns>
    public static PdfAnnotationBorderStyle? ParseBorderStyle(PdfDictionary? borderStyleDictionary, PdfArray? borderArray)
    {
        if (borderStyleDictionary != null)
        {
            float width = borderStyleDictionary.GetFloat(PdfTokens.WKey) ?? 1.0f;
            PdfAnnotationBorderStyleType style = borderStyleDictionary.GetName(PdfTokens.SKey).AsEnum<PdfAnnotationBorderStyleType>();

            float[]? dashPattern = null;
            PdfArray? dashArray = borderStyleDictionary.GetArray(PdfTokens.DashArrayKey);
            if (dashArray?.Count > 0)
            {
                float[] rawPattern = dashArray.GetFloatArray();
                dashPattern = GraphicsStateOperators.GetDashPattern(rawPattern);
            }

            PdfStrokeStyle strokeStyle = new(lineWidth: width, dashPattern: dashPattern);
            return new PdfAnnotationBorderStyle(style, strokeStyle);
        }

        if (borderArray?.Count >= 3)
        {
            float width = borderArray.GetFloatOrDefault(2);
            var style = PdfAnnotationBorderStyleType.Solid;

            float[]? dashPattern = null;
            if (borderArray.Count >= 4)
            {
                PdfArray? dashArrayEntry = borderArray.GetArray(3);
                if (dashArrayEntry?.Count > 0)
                {
                    float[] rawPattern = dashArrayEntry.GetFloatArray();
                    dashPattern = GraphicsStateOperators.GetDashPattern(rawPattern);
                    style = PdfAnnotationBorderStyleType.Dashed;
                }
            }

            PdfStrokeStyle strokeStyle = new(lineWidth: width, dashPattern: dashPattern);
            return new PdfAnnotationBorderStyle(style, strokeStyle);
        }

        return null;
    }

    /// <summary>
    /// Parses a border effect dictionary (BE entry).
    /// </summary>
    /// <returns>The parsed border effect, or the default (Solid, intensity 0) if <paramref name="borderEffectDictionary"/> is null.</returns>
    public static PdfAnnotationBorderEffect ParseBorderEffect(PdfDictionary? borderEffectDictionary)
    {
        if (borderEffectDictionary == null)
        {
            return default;
        }

        return new PdfAnnotationBorderEffect(
            borderEffectDictionary.GetName(PdfTokens.SKey).AsEnum<PdfAnnotationBorderEffectType>(),
            borderEffectDictionary.GetFloat(PdfTokens.IntensityKey) ?? 0f);
    }
}
