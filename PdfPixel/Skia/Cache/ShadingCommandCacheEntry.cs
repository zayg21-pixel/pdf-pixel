using PdfPixel.Commands.Cache;
using SkiaSharp;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Holds the rasterization primitives built for one shading content, shared by every command instance
/// drawing that content. Only the members matching the shading's type are populated; the rest stay
/// null. Owns what it holds and disposes it with the cache.
/// </summary>
internal sealed class ShadingCommandCacheEntry : ICommandCacheItem
{
    public ShadingCommandCacheEntry(SKImage? image, SKShader? shader, SKShader? innerShader, SKVertices? vertices)
    {
        Image = image;
        Shader = shader;
        InnerShader = innerShader;
        Vertices = vertices;
    }

    /// <summary>
    /// Sampled image of a function-based (Type 1) shading.
    /// </summary>
    public SKImage? Image { get; }

    /// <summary>
    /// Gradient shader of an axial (Type 2) shading, or the outer-cone shader of a radial (Type 3) shading.
    /// </summary>
    public SKShader? Shader { get; }

    /// <summary>
    /// Inner-cone shader of a radial (Type 3) shading.
    /// </summary>
    public SKShader? InnerShader { get; }

    /// <summary>
    /// Tessellated triangles of a mesh (Type 4 to Type 7) shading.
    /// </summary>
    public SKVertices? Vertices { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Image?.Dispose();
        Shader?.Dispose();
        InnerShader?.Dispose();
        Vertices?.Dispose();
    }
}
