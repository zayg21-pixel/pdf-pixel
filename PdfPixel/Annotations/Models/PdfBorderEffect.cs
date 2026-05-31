using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents the border effect dictionary (BE) for annotations.
/// </summary>
/// <remarks>
/// The border effect describes a visual treatment applied on top of the border.
/// Currently only Solid (no effect) and Cloudy are defined by the spec.
/// Cloudy stamps semicircle bumps along the border path at regular intervals using
/// <see cref="SKPathEffect.Create1DPath"/>. Any existing path effect (e.g. dash) is
/// preserved by composing via <see cref="SKPathEffect.CreateCompose"/>, with the cloud
/// stamps as the outer pass so dash spacing is respected.
/// </remarks>
public sealed class PdfBorderEffect
{
    private PdfBorderEffect(PdfBorderEffectType style, float intensity)
    {
        Style = style;
        Intensity = intensity;
    }

    /// <summary>
    /// Gets the border effect type.
    /// </summary>
    public PdfBorderEffectType Style { get; }

    /// <summary>
    /// Gets the intensity of the effect in the range 0–2. Only meaningful for <see cref="PdfBorderEffectType.Cloudy"/>.
    /// </summary>
    public float Intensity { get; }

    /// <summary>
    /// Creates a <see cref="PdfBorderEffect"/> from a border effect dictionary, or returns null if the
    /// dictionary is absent or specifies no effect.
    /// </summary>
    public static PdfBorderEffect? FromDictionary(PdfDictionary? dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        PdfBorderEffectType style = dictionary.GetName(PdfTokens.SKey).AsEnum<PdfBorderEffectType>();
        float intensity = dictionary.GetFloat(PdfTokens.IntensityKey) ?? 0f;

        if (style == PdfBorderEffectType.Solid)
        {
            return null;
        }

        return new PdfBorderEffect(style, intensity);
    }

    /// <summary>
    /// Applies the border effect to a paint object. For <see cref="PdfBorderEffectType.Cloudy"/>,
    /// stamps outward-facing semicircle bumps along the stroke path. Any existing
    /// <see cref="SKPaint.PathEffect"/> (e.g. dash) is composed as the inner pass so it is preserved.
    /// </summary>
    /// <param name="paint">The paint to modify.</param>
    /// <param name="borderWidth">The stroke width, used to scale bump size and spacing.</param>
    public void TryApplyEffect(SKPaint paint, float borderWidth)
    {
        if (paint == null || Style != PdfBorderEffectType.Cloudy)
        {
            return;
        }

        float bumpRadius = borderWidth * (1.5f + Intensity * 1.0f);
        float advance = bumpRadius * 1.6f;

        using SKPath bump = new();
        bump.AddArc(new SKRect(-bumpRadius, -bumpRadius, bumpRadius, bumpRadius), 0, -180);

        SKPathEffect cloud = SKPathEffect.Create1DPath(bump, advance, 0, SKPath1DPathEffectStyle.Rotate);

        paint.PathEffect = (paint.PathEffect != null)
            ? SKPathEffect.CreateCompose(cloud, paint.PathEffect)
            : cloud;
    }
}
