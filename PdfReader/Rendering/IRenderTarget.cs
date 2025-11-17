using SkiaSharp;
using System;

namespace PdfReader.Rendering
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
    }
}
