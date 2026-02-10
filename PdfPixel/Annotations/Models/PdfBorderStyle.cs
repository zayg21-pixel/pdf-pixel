using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the border style dictionary for annotations.
/// </summary>
/// <remarks>
/// The border style dictionary (BS) describes the characteristics of the annotation's border.
/// This is more flexible than the older Border array entry.
/// </remarks>
public class PdfBorderStyle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfBorderStyle"/> class.
    /// </summary>
    /// <param name="width">The border width.</param>
    /// <param name="style">The border style type.</param>
    /// <param name="dashPattern">The dash pattern array for dashed borders.</param>
    private PdfBorderStyle(float width, PdfBorderStyleType style, float[] dashPattern)
    {
        Width = width;
        Style = style;
        DashPattern = dashPattern;
    }

    /// <summary>
    /// Gets the border width in default user space units.
    /// </summary>
    /// <remarks>
    /// Default value is 1.0 if not specified.
    /// </remarks>
    public float Width { get; }

    /// <summary>
    /// Gets the border style type.
    /// </summary>
    /// <remarks>
    /// Default value is Solid if not specified.
    /// </remarks>
    public PdfBorderStyleType Style { get; }

    /// <summary>
    /// Gets the dash pattern array for dashed borders.
    /// </summary>
    /// <remarks>
    /// The dash pattern is an array of numbers that specify the lengths of alternating dashes and gaps.
    /// This is only applicable when Style is Dashed. Returns null if not specified or not a dashed border.
    /// </remarks>
    public float[] DashPattern { get; }

    /// <summary>
    /// Creates a PdfBorderStyle instance from a border style dictionary and/or legacy border array.
    /// </summary>
    /// <param name="borderStyleDictionary">The border style dictionary (BS entry), or null.</param>
    /// <param name="borderArray">The legacy border array (Border entry), or null.</param>
    /// <returns>A PdfBorderStyle instance, or null if no border information is present.</returns>
    public static PdfBorderStyle FromDictionary(PdfDictionary borderStyleDictionary, PdfArray borderArray)
    {
        if (borderStyleDictionary != null)
        {
            var width = borderStyleDictionary.GetFloat(PdfTokens.WKey) ?? 1.0f;
            var style = borderStyleDictionary.GetName(PdfTokens.SKey).AsEnum<PdfBorderStyleType>();

            float[] dashPattern = null;
            var dashArray = borderStyleDictionary.GetArray(PdfTokens.DashArrayKey);
            if (dashArray != null && dashArray.Count > 0)
            {
                var rawPattern = dashArray.GetFloatArray();
                dashPattern = GraphicsStateOperators.GetDashPattern(rawPattern);
            }

            return new PdfBorderStyle(width, style, dashPattern);
        }

        if (borderArray != null && borderArray.Count >= 3)
        {
            var width = borderArray.GetFloat(2);
            var style = PdfBorderStyleType.Solid;

            float[] dashPattern = null;
            if (borderArray.Count >= 4)
            {
                var dashArrayEntry = borderArray.GetArray(3);
                if (dashArrayEntry != null && dashArrayEntry.Count > 0)
                {
                    var rawPattern = dashArrayEntry.GetFloatArray();
                    dashPattern = GraphicsStateOperators.GetDashPattern(rawPattern);
                    style = PdfBorderStyleType.Dashed;
                }
            }

            return new PdfBorderStyle(width, style, dashPattern);
        }

        return null;
    }

    /// <summary>
    /// Applies the border style effect to a paint object.
    /// </summary>
    /// <param name="paint">The SKPaint to apply the effect to.</param>
    /// <param name="borderColor">The base border color for calculating shadow colors.</param>
    /// <returns>True if an effect was applied and normal drawing should proceed, false if special geometry handling is needed (Underline).</returns>
    public bool TryApplyEffect(SKPaint paint, SKColor borderColor)
    {
        if (paint == null)
        {
            return true;
        }

        switch (Style)
        {
            case PdfBorderStyleType.Dashed:
                if (DashPattern != null && DashPattern.Length > 0)
                {
                    paint.PathEffect = SKPathEffect.CreateDash(DashPattern, 0);
                }
                return true;

            case PdfBorderStyleType.Beveled:
                var bevelShadowOffset = Width * 0.5f;
                paint.ImageFilter = SKImageFilter.CreateDropShadow(
                    dx: bevelShadowOffset,
                    dy: bevelShadowOffset,
                    sigmaX: Width * 0.3f,
                    sigmaY: Width * 0.3f,
                    color: SKColors.Black.WithAlpha(80));
                return true;

            case PdfBorderStyleType.Inset:
                var insetShadowOffset = Width * 0.5f;
                paint.ImageFilter = SKImageFilter.CreateDropShadow(
                    dx: -insetShadowOffset,
                    dy: -insetShadowOffset,
                    sigmaX: Width * 0.3f,
                    sigmaY: Width * 0.3f,
                    color: SKColors.Black.WithAlpha(80));
                return true;

            case PdfBorderStyleType.Underline:
                return false;

            case PdfBorderStyleType.Solid:
            default:
                return true;
        }
    }
}
