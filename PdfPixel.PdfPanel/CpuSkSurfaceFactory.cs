using SkiaSharp;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Provides a factory for creating CPU-backed <see cref="SKSurface"/> instances.
/// Tracks surface dimensions internally and only recreates when the size changes.
/// </summary>
public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    private readonly SKColorType _colorType;
    private readonly SKAlphaType _alphaType;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;
    private int _currentWidth;
    private int _currentHeight;
    private int _currentThumbnailWidth;
    private int _currentThumbnailHeight;

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
    public virtual SKSurface GetDrawingSurface(int width, int height, CancellationToken token)
    {
        if (_currentSurface != null && _currentWidth == width && _currentHeight == height)
        {
            return _currentSurface;
        }

        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        var newSurface = SKSurface.Create(info);

        if (_currentSurface != null)
        {
            newSurface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
        }

        var oldSurface = _currentSurface;
        _currentSurface = newSurface;
        _currentWidth = width;
        _currentHeight = height;

        oldSurface?.Dispose();

        return newSurface;
    }

    /// <inheritdoc />
    public virtual SKSurface GetThumbnailSurface(int width, int height, CancellationToken token)
    {
        if (_currentThumbnailSurface != null && _currentThumbnailWidth == width && _currentThumbnailHeight == height)
        {
            return _currentThumbnailSurface;
        }

        var info = new SKImageInfo(width, height, _colorType, _alphaType);
        var newSurface = SKSurface.Create(info);

        var oldSurface = _currentThumbnailSurface;
        _currentThumbnailSurface = newSurface;
        _currentThumbnailWidth = width;
        _currentThumbnailHeight = height;

        oldSurface?.Dispose();

        return newSurface;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        _currentSurface?.Dispose();
        _currentThumbnailSurface?.Dispose();
    }
}
