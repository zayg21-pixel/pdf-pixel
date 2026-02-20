using SkiaSharp;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Provides a factory for creating CPU-backed <see cref="SKSurface"/> instances.
/// </summary>
public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    private readonly SKColorType _colorType;
    private readonly SKAlphaType _alphaType;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;

    public CpuSkSurfaceFactory(SKColorType colorType, SKAlphaType alphaType)
    {
        _colorType = colorType;
        _alphaType = alphaType;
    }

    /// <inheritdoc />
    public Task<SKSurface> GetDrawingSurfaceAsync(int width, int height)
    {
        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        var surface = SKSurface.Create(info);

        if (_currentSurface != null)
        {
            surface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
        }

        _currentSurface?.Dispose();
        _currentSurface = surface;
        return Task.FromResult(surface);
    }

    /// <inheritdoc />
    public Task<SKSurface> CreateThumbnailSurfaceAsync(int width, int height)
    {
        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        var surface = SKSurface.Create(info);

        _currentThumbnailSurface?.Dispose();
        _currentThumbnailSurface = surface;
        return Task.FromResult(surface);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentSurface?.Dispose();
        _currentSurface = null;
        _currentThumbnailSurface?.Dispose();
        _currentThumbnailSurface = null;
    }
}
