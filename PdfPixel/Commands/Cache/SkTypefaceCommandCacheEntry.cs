using SkiaSharp;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// Holds the <see cref="SKTypeface"/> built from an <see cref="Fonts.Model.IPdfTypeface"/>'s font
/// bytes, shared by every command that draws with that typeface during this execution's replay.
/// </summary>
internal sealed class SkTypefaceCommandCacheEntry : ICommandCacheItem
{
    public SkTypefaceCommandCacheEntry(SKTypeface typeface) => Typeface = typeface;

    public SKTypeface Typeface { get; }

    public void Dispose() => Typeface.Dispose();
}
