using System;
using System.Collections.Generic;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Advanced
{
    /// <summary>
    /// Disposable scope to render content with an optional soft mask and transparency group applied.
    /// Usage:
    /// using (var scope = new SoftMaskDrawingScope(canvas, softMask, document, graphicsState, currentPage, layerBounds))
    /// {
    ///     scope.BeginDrawContent();
    ///     // draw page/form content here
    ///     scope.EndDrawContent();
    /// }
    /// </summary>
    public sealed class SoftMaskDrawingScope : IDisposable
    {
        private readonly SKCanvas canvas;
        private readonly PdfSoftMask softMask;
        private readonly PdfGraphicsState graphicsState;
        private readonly PdfPage currentPage;
        private readonly SKRect? layerBounds;

        private SKRect activeBounds;
        private bool began;
        private bool shouldApplyMask;
        private bool disposed;

        public SoftMaskDrawingScope(
            SKCanvas canvas,
            PdfGraphicsState graphicsState,
            PdfPage currentPage,
            SKRect? layerBounds)
        {
            this.canvas = canvas;
            this.softMask = graphicsState.SoftMask;
            this.graphicsState = graphicsState;
            this.currentPage = currentPage;
            this.layerBounds = layerBounds;
        }

        /// <summary>
        /// Begins the drawing scope. If a valid soft mask is provided, creates a layer to capture content.
        /// </summary>
        public void BeginDrawContent()
        {
            if (began)
            {
                return;
            }

            began = true;

            if (canvas == null)
            {
                return;
            }

            shouldApplyMask = softMask != null && softMask.GroupObject != null;

            if (!shouldApplyMask)
            {
                return;
            }

            activeBounds = layerBounds ?? ComputeTightMaskBounds(canvas, softMask);

            if (activeBounds.Width <= 0 || activeBounds.Height <= 0)
            {
                // nothing to mask, draw directly
                shouldApplyMask = false;
                return;
            }

            // Capture content into a layer so we can apply the soft mask at EndDrawContent
            canvas.SaveLayer(activeBounds, null);
        }

        /// <summary>
        /// Ends the drawing scope. When a soft mask is active, records the mask picture and applies it using DstIn.
        /// Ensures the canvas restore is called when a layer was created.
        /// </summary>
        public void EndDrawContent()
        {
            if (!began)
            {
                return;
            }

            if (!shouldApplyMask)
            {
                return;
            }

            try
            {
                // Record the soft mask content into a picture
                using var recorder = new SKPictureRecorder();

                var cullRect = !softMask.TransformedBounds.IsEmpty ? softMask.TransformedBounds : activeBounds;
                using var recCanvas = recorder.BeginRecording(cullRect);

                recCanvas.Save();

                // Apply Form /Matrix
                if (!softMask.FormMatrix.IsIdentity)
                {
                    recCanvas.Concat(softMask.FormMatrix);
                }

                // Clip to /BBox if provided
                if (!softMask.BBox.IsEmpty)
                {
                    recCanvas.ClipRect(softMask.BBox);
                }

                // Background for luminosity masks (BC in group color space)
                if (softMask.Subtype == SoftMaskSubtype.Luminosity)
                {
                    using var bgPaint = new SKPaint
                    {
                        Style = SKPaintStyle.Fill,
                        Color = softMask.BackgroundColor ?? SKColors.White
                    };

                    var bgRect = softMask.BBox.IsEmpty ? cullRect : softMask.BBox;
                    recCanvas.DrawRect(bgRect, bgPaint);
                }

                // Render mask content stream
                var contentData = PdfStreamDecoder.DecodeContentStream(softMask.GroupObject);
                if (!contentData.IsEmpty)
                {
                    var parseContext = new PdfParseContext(contentData);
                    var maskGs = softMask.Subtype == SoftMaskSubtype.Luminosity
                        ? SoftMaskUtilities.CreateLuminosityMaskGraphicsState()
                        : SoftMaskUtilities.CreateAlphaMaskGraphicsState();

                    // The mask group itself should not inherit an outer soft mask.
                    maskGs.SoftMask = null;

                    var formPage = new FormXObjectPageWrapper(currentPage, softMask.GroupObject);
                    PdfContentStreamRenderer.RenderContentStream(recCanvas, ref parseContext, formPage, maskGs, new HashSet<int>());
                }

                recCanvas.Restore();

                using var picture = recorder.EndRecording();
                using var maskPaint = new SKPaint { IsAntialias = true, BlendMode = SKBlendMode.DstIn };

                using var alphaFilter = softMask.Subtype == SoftMaskSubtype.Luminosity
                    ? SoftMaskUtilities.CreateAlphaFromLuminosityFilter()
                    : null;

                using var trFilter = SoftMaskUtilities.CreateTransferFunctionColorFilter(softMask);

                using var composedFilter = (alphaFilter != null && trFilter != null)
                    ? SKColorFilter.CreateCompose(alphaFilter, trFilter)
                    : null;

                var filterToApply = composedFilter ?? trFilter ?? alphaFilter;
                maskPaint.ColorFilter = filterToApply;

                canvas.DrawPicture(picture, maskPaint);
            }
            finally
            {
                // Close the layer started in BeginDrawContent
                canvas.Restore();
                shouldApplyMask = false;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            // Ensure proper teardown if the caller forgot to call EndDrawContent
            if (began && shouldApplyMask)
            {
                try
                {
                    EndDrawContent();
                }
                catch
                {
                    // Best effort; avoid throwing from Dispose
                }
            }
        }

        private static SKRect ComputeTightMaskBounds(SKCanvas canvas, PdfSoftMask softMask)
        {
            var clip = canvas.LocalClipBounds;
            if (!softMask.TransformedBounds.IsEmpty)
            {
                var intersect = SKRect.Intersect(clip, softMask.TransformedBounds);
                return intersect.IsEmpty ? clip : intersect;
            }

            return clip;
        }
    }
}
