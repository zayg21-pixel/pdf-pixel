using PdfReader.Models;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Advanced
{
    public static class PdfSoftMaskProcessor
    {
        public static void DrawWithSoftMask(SKCanvas canvas, PdfSoftMask softMask, PdfDocument document, PdfGraphicsState graphicsState, PdfPage currentPage, SKRect? layerBounds, Action drawContent)
        {
            if (canvas == null || drawContent == null)
            {
                drawContent?.Invoke();
                return;
            }

            using (var scope = new SoftMaskDrawingScope(canvas, graphicsState, currentPage, layerBounds))
            {
                scope.BeginDrawContent();
                drawContent();
                scope.EndDrawContent();
            }
        }
    }
}