using PdfPixel.Color;
using PdfPixel.Commands;
using SkiaSharp;
using System;

namespace PdfPixel.Rendering
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
        /// <param name="processor">Command processor to draw through.</param>
        void Render(IPdfCommandProcessor processor);

        /// <summary>
        /// Bounding rectangle of this render target in current CTM space.
        /// Used by pattern tiling to determine which tiles are needed.
        /// For path/text targets this is derived from the clip path bounds;
        /// for image targets it is the unit square [0,0,1,1].
        /// </summary>
        SKRect Bounds { get; }

        /// <summary>
        /// Returns color of the render target if applicable.
        /// </summary>
        PdfColor Color { get; }

        /// <summary>
        /// Called by pattern implementations before they emit their tile/shading commands.
        /// Path/text targets apply their clip path; image targets open a new layer.
        /// </summary>
        void BeforePatternRender(IPdfCommandProcessor processor);

        /// <summary>
        /// Called by pattern implementations after all tile/shading commands have been emitted.
        /// No-op for path/text targets; image targets apply the stencil mask and close the layer.
        /// </summary>
        void AfterPatternRender(IPdfCommandProcessor processor);
    }
}
