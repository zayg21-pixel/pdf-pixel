using System;
using System.Collections.Concurrent;
using PdfPixel.Color.Paint;
using SkiaSharp;

namespace PdfPixel.Fonts.Management;

/// <summary>
/// Shared utilities for font variation axis support and glyph validation via <see cref="SKFont"/>.
/// </summary>
internal sealed class SkiaFontVariation : IDisposable
{
    private const uint WidthAxisTag = 0x77647468; // 'wdth'
    private const float WidthScaleToPercent = 100f;

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
    /// Returns a width-variation-adjusted typeface when <paramref name="width"/> is provided
    /// and the typeface supports the <c>wdth</c> axis. Otherwise returns the original typeface.
    /// Variation clones are cached by typeface handle and width value.
    /// </summary>
    public SKTypeface ApplyWidthVariation(SKTypeface typeface, float? width)
    {
        if (width == null)
        {
            return typeface;
        }

        return _variationCache.GetOrAdd((typeface.Handle, width.Value), key => CloneWithWidth(typeface, key.Width));
    }

    private static SKTypeface CloneWithWidth(SKTypeface typeface, float width)
    {
        SKFontVariationPositionCoordinate[] coordinates = [new() { Axis = WidthAxisTag, Value = width * WidthScaleToPercent }];
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
