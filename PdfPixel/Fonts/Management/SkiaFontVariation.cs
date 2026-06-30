using System;
using System.Collections.Concurrent;
using PdfPixel.Color.Paint;
using SkiaSharp;

namespace PdfPixel.Fonts.Management;

// TODO: [HIGH] implementation is incomplete, inefficient and creates "chicken and egg" problem. Also, it's per "unicode" char, and that's incorrect.
/// <summary>
/// Shared utilities for font variation axis support and glyph validation via <see cref="SKFont"/>.
/// </summary>
internal sealed class SkiaFontVariation : IDisposable
{
    private const uint WidthAxisTag = 0x77647468; // 'wdth'
    private const float WidthScaleToPercent = 100f;

    private readonly ConcurrentDictionary<nint, bool> _widthAxisSupportCache = [];
    private readonly ConcurrentDictionary<(nint TypefaceHandle, float Width), SKTypeface> _variationCache = [];

    /// <summary>
    /// Returns <see langword="true"/> when the typeface contains glyphs for all characters in <paramref name="unicode"/>.
    /// </summary>
    public static bool ContainsGlyphs(SKTypeface typeface, string? unicode)
    {
        if (unicode == null)
        {
            return true;
        }

        using SKFont skFont = PdfPaintFactory.CreateTextFont(typeface);
        ushort[] glyphs = skFont.GetGlyphs(unicode);

        for (int i = 0; i < glyphs.Length; i++)
        {
            if (glyphs[i] == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a width-variation-adjusted typeface when <paramref name="width"/> and <paramref name="unicode"/> are provided.
    /// Measures the base typeface glyph width, computes the target ratio, and clones with the <c>wdth</c> axis value.
    /// Variation clones are cached by typeface handle and target width.
    /// </summary>
    public SKTypeface ApplyWidthVariation(SKTypeface typeface, string? unicode, float? width)
    {
        if (typeface == null)
        {
            return typeface;
        }

        if (width == null || unicode == null || unicode.Length == 0)
        {
            return typeface;
        }

        if (!_widthAxisSupportCache.GetOrAdd(typeface.Handle, _ => HasWidthAxis(typeface)))
        {
            return typeface;
        }

        using SKFont skFont = PdfPaintFactory.CreateTextFont(typeface);
        float measuredWidth = skFont.MeasureText(unicode);
        if (measuredWidth <= 0)
        {
            return typeface;
        }

        float widthPercent = (width.Value / measuredWidth) * WidthScaleToPercent;
        return _variationCache.GetOrAdd((typeface.Handle, widthPercent), key => CloneWithWidth(typeface, key.Width));
    }

    private static bool HasWidthAxis(SKTypeface typeface)
    {
        SKFontVariationAxis[] axes = typeface.VariationDesignParameters;

        for (int i = 0; i < axes.Length; i++)
        {
            if (axes[i].Tag == WidthAxisTag)
            {
                return true;
            }
        }

        return false;
    }

    private static SKTypeface CloneWithWidth(SKTypeface typeface, float widthPercent)
    {
        SKFontVariationPositionCoordinate[] coordinates = [new() { Axis = WidthAxisTag, Value = widthPercent }];
        SKTypeface cloned = typeface.Clone(coordinates);
        return cloned ?? typeface;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (SKTypeface variationTypeface in _variationCache.Values)
        {
            variationTypeface.Dispose();
        }

        _variationCache.Clear();
    }
}
