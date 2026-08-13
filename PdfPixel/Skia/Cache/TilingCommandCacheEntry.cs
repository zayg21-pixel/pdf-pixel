using PdfPixel.Commands.Cache;
using SkiaSharp;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Holds the rasterization primitives recorded for one tiling pattern cell, shared by every command
/// instance drawing that cell under the same tint color and device matrix. Owns what it holds and
/// disposes it with the cache.
/// </summary>
internal sealed class TilingCommandCacheEntry : ICommandCacheItem
{
    public TilingCommandCacheEntry(SKPicture picture, SKShader? shader)
    {
        Picture = picture;
        Shader = shader;
    }

    /// <summary>
    /// The recorded cell, or the recorded tile when the pattern is covered by repeating.
    /// </summary>
    public SKPicture Picture { get; }

    /// <summary>
    /// Shader repeating <see cref="Picture"/> across the tiling area. Null when the pattern is drawn
    /// by replaying the cell once per grid position.
    /// </summary>
    public SKShader? Shader { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Picture.Dispose();
        Shader?.Dispose();
    }
}
