using PdfPixel.Commands.Cache;
using SkiaSharp;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Holds the <see cref="SKTypeface"/> built from an <see cref="PdfPixel.Fonts.Model.IPdfTypeface"/>'s font
/// bytes, shared by every command of the document that draws with that typeface.
/// </summary>
internal sealed class SkTypefaceCommandCacheEntry : ICommandCacheItem
{
    public SkTypefaceCommandCacheEntry(SKTypeface typeface) => Typeface = typeface;

    public SKTypeface Typeface { get; }

    public void Dispose() => Typeface.Dispose();
}
