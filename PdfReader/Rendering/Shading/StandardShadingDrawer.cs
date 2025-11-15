using System;
using SkiaSharp;
using PdfReader.Models;
using Microsoft.Extensions.Logging;
using PdfReader.Shading;
using PdfReader.Shading.Model;
using PdfReader.Color.Paint;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Utilities;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Draws axial (type 2) and radial (type 3) shadings using parsed <see cref="PdfShading"/> model.
    /// Applies soft mask scope once per shading draw.
    /// </summary>
    public class StandardShadingDrawer : IShadingDrawer
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<StandardShadingDrawer> _logger;

        public StandardShadingDrawer(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<StandardShadingDrawer>();
        }

        /// <summary>
        /// Draw a shading fill described by a parsed shading model, applying soft mask if present.
        /// </summary>
        public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state, PdfPage page)
        {
            if (canvas == null)
            {
                return;
            }
            if (shading == null)
            {
                return;
            }

            using (var softMaskScope = new SoftMaskDrawingScope(canvas, state, page))
            {
                softMaskScope.BeginDrawContent();
                DrawShadingCore(canvas, shading, state, page);
                softMaskScope.EndDrawContent();
            }
        }

        /// <summary>
        /// Core shading dispatch logic without soft mask handling.
        /// Uses ToShader for both axial and radial shading. No special clipping for radial shading.
        /// </summary>
        private void DrawShadingCore(SKCanvas canvas, PdfShading shading, PdfGraphicsState state, PdfPage page)
        {
            using var shader = PdfShadingBuilder.ToShader(shading);
            if (shader == null)
            {
                _logger.LogWarning("Shading type " + shading.ShadingType + " not implemented or invalid shading data");
                return;
            }

            using (var paint = PdfPaintFactory.CreateShadingPaint(state, shader))
            {
                canvas.DrawPaint(paint);
            }
        }
    }
}
