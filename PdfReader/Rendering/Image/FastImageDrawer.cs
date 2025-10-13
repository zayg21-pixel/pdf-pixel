using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using System;
using Microsoft.Extensions.Logging;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Image drawer that keeps images as SKImage (immutable) and avoids pixel-buffer roundtrips.
    /// </summary>
    public class FastImageDrawer : IImageDrawer
    {
        private readonly ILoggerFactory _factory;
        private readonly ILogger<FastImageDrawer> _logger;

        public FastImageDrawer(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _factory = loggerFactory;
            _logger = loggerFactory.CreateLogger<FastImageDrawer>();
        }

        /// <summary>
        /// Draw a PDF image to the specified canvas rectangle applying image masks or soft masks when present.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="pdfImage">Image definition.</param>
        /// <param name="state">Current graphics state.</param>
        /// <param name="page">Owning page.</param>
        /// <param name="destRect">Destination rectangle.</param>
        public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, PdfPage page, SKRect destRect)
        {
            if (canvas == null)
            {
                return;
            }
            if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
            {
                return;
            }

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);
            softMaskScope.BeginDrawContent();

            var decoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
            if (decoder == null)
            {
                _logger.LogWarning("No decoder for image '{ImageName}' of type {ImageType}. Skipping.", pdfImage?.Name, pdfImage?.Type);
                return;
            }

            using var baseImage = decoder.Decode();
            if (baseImage == null)
            {
                _logger.LogWarning("Decoder returned null for image '{ImageName}'. Skipping.", pdfImage?.Name);
                return;
            }

            using var imagePaint = PdfPaintFactory.CreateImagePaint(state, page);
            var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);

            if (pdfImage.HasImageMask)
            {
                DrawImageMask(canvas, baseImage, state, page, destRect, pdfImage.Interpolate);
                return;
            }

            // Attempt direct soft mask compositing without creating an intermediate SKImage.
            bool softMaskApplied = TryDrawWithSoftMask(canvas, baseImage, pdfImage, state, page, destRect, sampling, imagePaint);
            if (!softMaskApplied)
            {
                canvas.DrawImage(baseImage, destRect, sampling, imagePaint);
            }
        }

        private void DrawImageMask(SKCanvas canvas, SKImage alphaMask, PdfGraphicsState state, PdfPage page, SKRect destRect, bool interpolate)
        {
            if (canvas == null)
            {
                return;
            }
            if (alphaMask == null)
            {
                return;
            }

            using var fillPaint = PdfPaintFactory.CreateFillPaint(state, page);
            var sampling = PdfPaintFactory.GetImageSamplingOptions(interpolate);

            canvas.SaveLayer(destRect, null);
            try
            {
                canvas.DrawImage(alphaMask, destRect, sampling);

                using var srcInPaint = new SKPaint
                {
                    BlendMode = SKBlendMode.SrcIn,
                    Color = fillPaint.Color
                };

                canvas.DrawRect(destRect, srcInPaint);
            }
            finally
            {
                canvas.Restore();
            }
        }

        /// <summary>
        /// Apply the soft mask of the image (if any) by compositing directly onto the destination canvas using a layer.
        /// Avoids creating an intermediate off-screen surface and snapshot image.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="sourceImage">Already decoded base image.</param>
        /// <param name="pdfImage">PdfImage descriptor referencing potential /SMask.</param>
        /// <param name="state">Graphics state.</param>
        /// <param name="page">Owning page.</param>
        /// <param name="destRect">Destination rectangle.</param>
        /// <param name="sampling">Sampling options (interpolation).</param>
        /// <param name="imagePaint">Paint used for drawing the base image.</param>
        /// <returns>True if a soft mask was applied; otherwise false.</returns>
        private bool TryDrawWithSoftMask(SKCanvas canvas, SKImage sourceImage, PdfImage pdfImage, PdfGraphicsState state, PdfPage page, SKRect destRect, SKSamplingOptions sampling, SKPaint imagePaint)
        {
            if (canvas == null)
            {
                return false;
            }
            if (sourceImage == null)
            {
                return false;
            }
            if (pdfImage == null)
            {
                return false;
            }

            try
            {
                var dictionary = pdfImage.SourceObject?.Dictionary;
                if (dictionary == null)
                {
                    return false;
                }
                var softMaskObject = dictionary.GetPageObject(PdfTokens.SoftMaskKey);
                if (softMaskObject == null)
                {
                    return false;
                }

                var softMaskDescriptor = PdfImage.FromXObject(softMaskObject, page, pdfImage.Name, isSoftMask: true);
                var softMaskDecoder = PdfImageDecoder.GetDecoder(softMaskDescriptor, _factory);
                using var maskImage = softMaskDecoder?.Decode();
                if (maskImage == null)
                {
                    _logger.LogWarning("Soft mask decode failed for image '{ImageName}'.", pdfImage?.Name);
                    return false;
                }

                var matteArray = softMaskObject.Dictionary?.GetArray(PdfTokens.MatteKey).GetFloatArray();
                if (matteArray != null && matteArray.Length > 0)
                {
                    // TODO: add matte support
                    _logger.LogWarning("Image '{ImageName}': /SMask has /Matte; dematting not implemented.", pdfImage?.Name);
                }

                bool maskHasAlpha = maskImage.ColorType == SKColorType.Alpha8 || maskImage.AlphaType != SKAlphaType.Opaque;
                SKColorFilter alphaFilter = null;
                if (!maskHasAlpha)
                {
                    alphaFilter = SoftMaskUtilities.CreateAlphaFromLuminosityFilter();
                }

                // Compose: layer -> draw base -> dst-in mask.
                canvas.SaveLayer(destRect, null);
                try
                {
                    canvas.DrawImage(sourceImage, destRect, sampling, imagePaint);

                    using var maskPaint = new SKPaint
                    {
                        BlendMode = SKBlendMode.DstIn,
                        ColorFilter = alphaFilter
                    };
                    var maskSampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);
                    canvas.DrawImage(maskImage, destRect, maskSampling, maskPaint);
                }
                finally
                {
                    canvas.Restore();
                    alphaFilter?.Dispose();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Soft mask application failed for image '{ImageName}'.", pdfImage?.Name);
                return false;
            }
        }
    }
}
