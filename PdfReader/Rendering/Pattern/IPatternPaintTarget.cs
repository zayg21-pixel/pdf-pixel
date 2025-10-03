using SkiaSharp;

namespace PdfReader.Rendering.Pattern
{
    /// <summary>
    /// Abstraction for geometry that can receive a pattern fill (path, text outline, image mask, etc.).
    /// Responsible for applying a clip and providing device bounds for tiling iteration.
    /// </summary>
    internal interface IPatternPaintTarget
    {
        /// <summary>
        /// Apply the geometry clip to the canvas. Caller will have saved the canvas state.
        /// </summary>
        void ApplyClip(SKCanvas canvas);

        /// <summary>
        /// Return conservative device-space bounds of the clipped geometry (after current canvas transforms).
        /// Used for computing tiling coverage.
        /// </summary>
        SKRect GetDeviceBounds(SKCanvas canvas);
    }
}
