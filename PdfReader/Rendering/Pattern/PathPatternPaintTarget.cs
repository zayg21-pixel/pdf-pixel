using SkiaSharp;

namespace PdfReader.Rendering.Pattern
{
    /// <summary>
    /// Pattern paint target for an SKPath. A geometry (clip) path is constructed once using the provided
    /// paint (stroke or fill) so that pattern logic uniformly consumes a filled outline.
    /// Caller passes an SKPaint configured with stroke parameters when stroking; for fill operations
    /// the original path is used directly.
    /// </summary>
    internal sealed class PathPatternPaintTarget : IPatternPaintTarget
    {
        private readonly SKPath _clipPath; // Filled outline used for clipping and bounds

        public PathPatternPaintTarget(SKPath sourcePath, SKPaint paintForGeometry)
        {
            if (sourcePath == null || sourcePath.IsEmpty)
            {
                _clipPath = null;
                return;
            }

            if (paintForGeometry == null || paintForGeometry.Style == SKPaintStyle.Fill)
            {
                // Use original path (clone to avoid external mutation while we hold reference)
                _clipPath = new SKPath(sourcePath);
            }
            else
            {
                // Convert stroke (or stroke-and-fill) to a fill outline.
                var outline = new SKPath();
                if (paintForGeometry.GetFillPath(sourcePath, outline))
                {
                    _clipPath = outline;
                }
                else
                {
                    outline.Dispose();
                    _clipPath = new SKPath(sourcePath); // Fallback
                }
            }
        }

        public void ApplyClip(SKCanvas canvas)
        {
            if (_clipPath == null || _clipPath.IsEmpty)
            {
                return;
            }
            canvas.ClipPath(_clipPath, antialias: true);
        }

        public SKRect GetDeviceBounds(SKCanvas canvas)
        {
            if (_clipPath == null || _clipPath.IsEmpty)
            {
                return SKRect.Empty;
            }
            return canvas.TotalMatrix.MapRect(_clipPath.Bounds);
        }
    }
}
