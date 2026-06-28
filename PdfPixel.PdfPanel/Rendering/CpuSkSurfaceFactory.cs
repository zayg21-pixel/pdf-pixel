using SkiaSharp;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// CPU-backed <see cref="ISkSurfaceFactory"/> using SkiaSharp raster surfaces.
/// Previous content is blitted onto the new surface when dimensions change.
/// </summary>
public sealed class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    private readonly SKColorType _colorType;
    private readonly SKAlphaType _alphaType;
    private SKSurface? _currentSurface;
    private int _currentWidth;
    private int _currentHeight;
    private SKSurface? _tilingSurface;
    private int _tilingWidth;
    private int _tilingHeight;

    /// <summary>
    /// Initializes a new instance with the specified pixel format.
    /// </summary>
    public CpuSkSurfaceFactory(SKColorType colorType, SKAlphaType alphaType)
    {
        _colorType = colorType;
        _alphaType = alphaType;
    }

    /// <inheritdoc />
    public void Initialize()
    {
    }

    /// <inheritdoc />
    public SKSurface GetDrawingSurface(int width, int height)
    {
        if (_currentSurface != null && _currentWidth == width && _currentHeight == height)
        {
            return _currentSurface;
        }

        SKImageInfo info = new(width, height, _colorType, _alphaType);
        SKSurface newSurface = SKSurface.Create(info);

        if (_currentSurface != null)
        {
            newSurface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
        }

        SKSurface? oldSurface = _currentSurface;
        _currentSurface = newSurface;
        _currentWidth = width;
        _currentHeight = height;

        oldSurface?.Dispose();

        return newSurface;
    }

    /// <inheritdoc />
    public SKSurface GetTilingSurface(int width, int height)
    {
        if (_tilingSurface != null && _tilingWidth == width && _tilingHeight == height)
        {
            return _tilingSurface;
        }

        _tilingSurface?.Dispose();
        SKImageInfo info = new(width, height, _colorType, _alphaType);
        _tilingSurface = SKSurface.Create(info);
        _tilingWidth = width;
        _tilingHeight = height;

        return _tilingSurface;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _currentSurface?.Dispose();
        _tilingSurface?.Dispose();
    }
}

