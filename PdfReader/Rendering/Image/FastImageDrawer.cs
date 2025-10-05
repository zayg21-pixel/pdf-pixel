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
            _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<FastImageDrawer>();
        }

        public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, PdfPage page, SKRect destRect)
        {
            if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
            {
                return;
            }

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page, destRect);
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

            using var paint = PdfPaintFactory.CreateImagePaint(state, page);
            var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);

            if (pdfImage.HasImageMask)
            {
                DrawImageMask(canvas, baseImage, state, page, destRect, pdfImage.Interpolate);
                return;
            }

            bool softMaskApplied;
            using var softMask = ApplyImageSoftMask(baseImage, pdfImage, page, out softMaskApplied);
            var finalImage = softMask ?? baseImage;

            canvas.DrawImage(finalImage, destRect, sampling, paint);

            softMaskScope.EndDrawContent();
        }

        private void DrawImageMask(SKCanvas canvas, SKImage alphaMask, PdfGraphicsState state, PdfPage page, SKRect destRect, bool interpolate)
        {
            using var fillPaint = PdfPaintFactory.CreateFillPaint(state, page);
            using var maskPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn
            };

            var sampling = PdfPaintFactory.GetImageSamplingOptions(interpolate);

            canvas.SaveLayer(destRect, null);
            try
            {
                canvas.DrawRect(destRect, fillPaint);
                canvas.DrawImage(alphaMask, destRect, sampling, maskPaint);
            }
            finally
            {
                canvas.Restore();
            }
        }

        private SKImage ApplyImageSoftMask(SKImage source, PdfImage pdfImage, PdfPage page, out bool maskApplied)
        {
            maskApplied = false;
            if (source == null)
            {
                return null;
            }

            try
            {
                var dictionary = pdfImage.ImageXObject?.Dictionary;
                var softMaskObject = dictionary?.GetPageObject(PdfTokens.SoftMaskKey);
                if (softMaskObject == null)
                {
                    return null;
                }

                var softMaskImageDescriptor = PdfImage.FromXObject(softMaskObject, page, pdfImage.Name, isSoftMask: true);
                var softMaskDecoder = PdfImageDecoder.GetDecoder(softMaskImageDescriptor, _factory);
                using var maskImage = softMaskDecoder?.Decode();
                if (maskImage == null)
                {
                    _logger.LogWarning("Soft mask decode failed for image '{ImageName}'.", pdfImage?.Name);
                    return null;
                }

                var matteArray = softMaskObject.Dictionary?.GetArray(PdfTokens.MatteKey).GetFloatArray();
                if (matteArray != null && matteArray.Length > 0)
                {
                    _logger.LogInformation("Image '{ImageName}': /SMask has /Matte; dematting not implemented.", pdfImage?.Name);
                }

                SKColorFilter alphaFilter = null;
                bool maskHasAlpha = maskImage.ColorType == SKColorType.Alpha8 || maskImage.AlphaType != SKAlphaType.Opaque;
                if (!maskHasAlpha)
                {
                    alphaFilter = SoftMaskUtilities.CreateAlphaFromLuminosityFilter();
                }

                int offWidth = Math.Max(1, source.Width);
                int offHeight = Math.Max(1, source.Height);
                using var surface = SKSurface.Create(new SKImageInfo(offWidth, offHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
                if (surface == null)
                {
                    _logger.LogWarning("Failed to create surface for soft mask composition for image '{ImageName}'.", pdfImage?.Name);
                    return null;
                }

                using var compositionCanvas = surface.Canvas;
                compositionCanvas.Clear(SKColors.Transparent);

                var baseSampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);
                compositionCanvas.DrawImage(source, new SKRect(0, 0, offWidth, offHeight), baseSampling, new SKPaint());

                using var maskPaint = new SKPaint
                {
                    BlendMode = SKBlendMode.DstIn,
                    ColorFilter = alphaFilter
                };
                var maskSampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);
                compositionCanvas.DrawImage(maskImage, new SKRect(0, 0, offWidth, offHeight), maskSampling, maskPaint);
                compositionCanvas.Flush();

                alphaFilter?.Dispose();

                maskApplied = true;
                return surface.Snapshot();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Soft mask application failed for image '{ImageName}'.", pdfImage?.Name);
                return null; // Safe to continue drawing base image without mask
            }
        }
    }
}
