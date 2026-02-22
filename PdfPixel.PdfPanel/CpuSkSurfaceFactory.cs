using SkiaSharp;
using System.Threading;

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
    public void Initialize()
    {
        // CPU factory doesn't need initialization
    }

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height, CancellationToken token)
    {
        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        var newSurface = SKSurface.Create(info);

        if (_currentSurface != null)
        {
            newSurface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
        }

        var oldSurface = _currentSurface;
        _currentSurface = newSurface;

        oldSurface?.Dispose();

        return newSurface;
    }

    /// <inheritdoc />
    public SKSurface CreateThumbnailSurface(int width, int height, CancellationToken token)
    {
        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        var newSurface = SKSurface.Create(info);

        var oldSurface = _currentThumbnailSurface;
        _currentThumbnailSurface = newSurface;

        oldSurface?.Dispose();

        return newSurface;
    }

    /// <inheritdoc />
    /// <remarks>CPU-backed surfaces do not require context switching.</remarks>
    public void SetCurrentSurface(SKSurface surface)
    {
        // No-op for CPU surfaces
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentSurface?.Dispose();
        _currentThumbnailSurface?.Dispose();
    }
}
