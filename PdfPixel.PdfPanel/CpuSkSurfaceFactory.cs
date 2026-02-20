using SkiaSharp;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Provides a factory for creating CPU-backed <see cref="SKSurface"/> instances.
/// </summary>
public class CpuSkSurfaceFactory : ISkSurfaceFactory
{
    private readonly object _lock = new object();
    private readonly SKColorType _colorType;
    private readonly SKAlphaType _alphaType;
    private SKSurface _currentSurface;
    private SKSurface _currentThumbnailSurface;
    private bool _disposed;

    public CpuSkSurfaceFactory(SKColorType colorType, SKAlphaType alphaType)
    {
        _colorType = colorType;
        _alphaType = alphaType;
    }

    /// <inheritdoc />
    public Task<SKSurface> GetDrawingSurfaceAsync(int width, int height)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var info = new SKImageInfo(width, height, _colorType, _alphaType);
            var newSurface = SKSurface.Create(info);

            if (_currentSurface != null)
            {
                newSurface.Canvas.DrawSurface(_currentSurface, SKPoint.Empty);
            }

            var oldSurface = _currentSurface;
            _currentSurface = newSurface;

            oldSurface?.Dispose();

            return Task.FromResult(newSurface);
        }
    }

    /// <inheritdoc />
    public Task<SKSurface> CreateThumbnailSurfaceAsync(int width, int height)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var info = new SKImageInfo(width, height, _colorType, _alphaType);
            var newSurface = SKSurface.Create(info);

            var oldSurface = _currentThumbnailSurface;
            _currentThumbnailSurface = newSurface;

            oldSurface?.Dispose();

            return Task.FromResult(newSurface);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _currentSurface?.Dispose();
            _currentSurface = null;

            _currentThumbnailSurface?.Dispose();
            _currentThumbnailSurface = null;
        }
    }
}
