using Microsoft.Extensions.Logging;
using PdfReader.Imaging.Decoding;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;
using System;

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

        /// <summary>
        /// Draw a PDF image to the specified canvas rectangle applying image masks or soft masks when present.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="pdfImage">Image definition.</param>
        /// <param name="state">Current graphics state.</param>
        /// <param name="page">Owning page.</param>
        /// <param name="destRect">Destination rectangle.</param>
        public void DrawImage(
            SKCanvas canvas,
            PdfImage pdfImage,
            PdfGraphicsState state,
            PdfPage page,
            SKRect destRect)
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

            if (pdfImage.HasImageMask)
            {
                DrawImageMask(canvas, pdfImage, state, page, destRect);
                return;
            }

            if (pdfImage.SoftMask != null)
            {
                DrawWithSoftMask(canvas, pdfImage, state, page, destRect);
                return;
            }

            DrawNormalImage(canvas, pdfImage, state, page, destRect);
        }

        /// <summary>
        /// Draws a normal PDF image (no image mask or soft mask) to the specified canvas.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="pdfImage">Image definition.</param>
        /// <param name="state">Current graphics state.</param>
        /// <param name="page">Owning page.</param>
        /// <param name="destRect">Destination rectangle.</param>
        private void DrawNormalImage(
            SKCanvas canvas,
            PdfImage pdfImage,
            PdfGraphicsState state,
            PdfPage page,
            SKRect destRect)
        {
            var decoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
            if (decoder == null)
            {
                _logger.LogWarning(
                    "No decoder for image '{ImageName}' of type {ImageType}. Skipping.",
                    pdfImage?.Name,
                    pdfImage?.Type);
                return;
            }

            using var baseImage = decoder.Decode();
            if (baseImage == null)
            {
                _logger.LogWarning(
                    "Decoder returned null for image '{ImageName}'. Skipping.",
                    pdfImage?.Name);
                return;
            }

            using var imagePaint = PdfPaintFactory.CreateImagePaint(state);
            ImagePostProcessingFilters.ApplyImageFilters(imagePaint, pdfImage, decoder.IsColorConverted);

            var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);
            canvas.DrawImage(baseImage, destRect, sampling, imagePaint);
        }

        /// <summary>
        /// Draws an image mask (stencil mask) to the specified canvas.
        /// Decodes the image mask, applies the decode filter, and fills using the current fill paint.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="pdfImage">The image mask definition.</param>
        /// <param name="state">Current graphics state.</param>
        /// <param name="page">Owning page.</param>
        /// <param name="destRect">Destination rectangle.</param>
        private void DrawImageMask(
            SKCanvas canvas,
            PdfImage pdfImage,
            PdfGraphicsState state,
            PdfPage page,
            SKRect destRect)
        {
            var decoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
            if (decoder == null)
            {
                _logger.LogWarning(
                    "No decoder for image mask '{ImageName}'. Skipping.",
                    pdfImage?.Name);
                return;
            }

            using var alphaMask = decoder.Decode();
            if (alphaMask == null)
            {
                _logger.LogWarning(
                    "Decoder returned null for image mask '{ImageName}'. Skipping.",
                    pdfImage?.Name);
                return;
            }

            using var fillPaint = PdfPaintFactory.CreateFillPaint(state);

            var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);

            canvas.SaveLayer(destRect, null);

            using var maskPaint = new SKPaint
            {
                IsAntialias = true,
                ColorFilter = ColorFilterDecode.BuildMaskDecodeFilter(pdfImage.DecodeArray)
            };

            canvas.DrawImage(alphaMask, destRect, sampling, maskPaint);

            using var srcInPaint = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.SrcIn,
                Color = fillPaint.Color
            };

            canvas.DrawRect(destRect, srcInPaint);

            canvas.Restore();
        }

        /// <summary>
        /// Draws a PDF image with a soft mask applied, compositing directly onto the destination canvas using a layer.
        /// If any step fails, logs a warning and does not draw.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="pdfImage">The base image definition (may have SoftMask property).</param>
        /// <param name="state">Current graphics state.</param>
        /// <param name="page">Owning page.</param>
        /// <param name="destRect">Destination rectangle.</param>
        private void DrawWithSoftMask(
            SKCanvas canvas,
            PdfImage pdfImage,
            PdfGraphicsState state,
            PdfPage page,
            SKRect destRect)
        {
            if (pdfImage.SoftMask == null)
            {
                _logger.LogWarning("No soft mask present for image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
                return;
            }

            var baseDecoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
            if (baseDecoder == null)
            {
                _logger.LogWarning("No decoder for image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
                return;
            }

            using var baseImage = baseDecoder.Decode();
            if (baseImage == null)
            {
                _logger.LogWarning("Decoder returned null for image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
                return;
            }

            var softMaskDecoder = PdfImageDecoder.GetDecoder(pdfImage.SoftMask, _factory);
            if (softMaskDecoder == null)
            {
                _logger.LogWarning("No decoder for soft mask of image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
                return;
            }

            using var maskImage = softMaskDecoder.Decode();
            if (maskImage == null)
            {
                _logger.LogWarning("Decoder returned null for soft mask of image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
                return;
            }

            using var imagePaint = PdfPaintFactory.CreateImagePaint(state);
            ImagePostProcessingFilters.ApplyImageFilters(imagePaint, pdfImage, baseDecoder.IsColorConverted);

            using var maskPaint = new SKPaint
            {
                IsAntialias = true,
                BlendMode = SKBlendMode.DstIn
            };
            ImagePostProcessingFilters.ApplyImageFilters(maskPaint, pdfImage.SoftMask, softMaskDecoder.IsColorConverted);

            var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.Interpolate);
            var maskSampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.SoftMask.Interpolate);

            using var layerPaint = new SKPaint
            {
                IsAntialias = true,
                BlendMode = imagePaint.BlendMode,
                Color = imagePaint.Color
            };

            canvas.SaveLayer(destRect, layerPaint);

            canvas.DrawImage(baseImage, destRect, sampling, imagePaint);
            canvas.DrawImage(maskImage, destRect, maskSampling, maskPaint);

            canvas.Restore();
        }
    }
}
