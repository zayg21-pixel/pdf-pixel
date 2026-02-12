using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Provides a factory for creating CPU-backed <see cref="SKSurface"/> instances.
/// </summary>
public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    private readonly SKColorType _colorType;
    private readonly SKAlphaType _alphaType;

    public CpuSkSurfaceFactory(SKColorType colorType, SKAlphaType alphaType)
    {
        _colorType = colorType;
        _alphaType = alphaType;
    }

    /// <inheritdoc />
    public SKSurface GetSurface(int width, int height)
    {
        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        return SKSurface.Create(info);
    }
}
