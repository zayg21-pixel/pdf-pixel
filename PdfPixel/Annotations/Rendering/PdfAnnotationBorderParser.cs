using PdfPixel.Color.Paint;
using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Parses annotation border style (BS dictionary / legacy Border array) and border effect
/// (BE dictionary) entries into a <see cref="PdfStrokeStyle"/>.
/// </summary>
internal static class PdfAnnotationBorderParser
{
    /// <summary>
    /// Creates a <see cref="PdfStrokeStyle"/> from a border style dictionary and/or legacy border array.
    /// </summary>
    /// <param name="borderStyleDictionary">The border style dictionary (BS entry), or null.</param>
    /// <param name="borderArray">The legacy border array (Border entry), or null.</param>
    /// <returns>A <see cref="PdfStrokeStyle"/>, or null if no border information is present.</returns>
    public static PdfStrokeStyle? ParseBorderStyle(PdfDictionary? borderStyleDictionary, PdfArray? borderArray)
    {
        if (borderStyleDictionary != null)
        {
            float width = borderStyleDictionary.GetFloat(PdfTokens.WKey) ?? 1.0f;
            PdfBorderStyleType style = borderStyleDictionary.GetName(PdfTokens.SKey).AsEnum<PdfBorderStyleType>();

            float[]? dashPattern = null;
            PdfArray? dashArray = borderStyleDictionary.GetArray(PdfTokens.DashArrayKey);
            if (dashArray?.Count > 0)
            {
                float[] rawPattern = dashArray.GetFloatArray();
                dashPattern = GraphicsStateOperators.GetDashPattern(rawPattern);
            }

            return new PdfStrokeStyle
            {
                LineWidth = width,
                BorderStyleType = style,
                DashPattern = dashPattern
            };
        }

        if (borderArray?.Count >= 3)
        {
            float width = borderArray.GetFloatOrDefault(2);
            var style = PdfBorderStyleType.Solid;

            float[]? dashPattern = null;
            if (borderArray.Count >= 4)
            {
                PdfArray? dashArrayEntry = borderArray.GetArray(3);
                if (dashArrayEntry?.Count > 0)
                {
                    float[] rawPattern = dashArrayEntry.GetFloatArray();
                    dashPattern = GraphicsStateOperators.GetDashPattern(rawPattern);
                    style = PdfBorderStyleType.Dashed;
                }
            }

            return new PdfStrokeStyle
            {
                LineWidth = width,
                BorderStyleType = style,
                DashPattern = dashPattern
            };
        }

        return null;
    }

    /// <summary>
    /// Applies a border effect dictionary (BE entry) onto an existing border stroke style in place.
    /// Does nothing if <paramref name="strokeStyle"/> or <paramref name="borderEffectDictionary"/> is null.
    /// </summary>
    public static void ApplyBorderEffect(PdfStrokeStyle? strokeStyle, PdfDictionary? borderEffectDictionary)
    {
        if (strokeStyle == null || borderEffectDictionary == null)
        {
            return;
        }

        strokeStyle.BorderEffectType = borderEffectDictionary.GetName(PdfTokens.SKey).AsEnum<PdfBorderEffectType>();
        strokeStyle.BorderEffectIntensity = borderEffectDictionary.GetFloat(PdfTokens.IntensityKey) ?? 0f;
    }
}
