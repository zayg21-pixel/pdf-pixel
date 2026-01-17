using SkiaSharp;
using System;

namespace PdfRender.Rendering
{
    /// <summary>
    /// Represents single drawing item for composition of paint and
    /// content. Also performs required content transformations
    /// to active coordinates.
    /// </summary>
    internal interface IRenderTarget : IDisposable
    {
        /// <summary>
        /// Request to render single drawing item.
        /// </summary>
        /// <param name="canvas">Target canvas.</param>
        void Render(SKCanvas canvas);

        /// <summary>
        /// Gets the clipping path used to define the visible region of the drawing surface.
        /// </summary>
        public SKPath ClipPath { get; }

        /// <summary>
        /// Returns color of the render target if applicable.
        /// </summary>
        public SKColor Color { get; }
    }
}
