using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using System;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Image drawer that keeps images as SKImage (immutable) and avoids pixel-buffer roundtrips.
    /// Only paint- and layer-based operations are applied. Any feature that requires direct pixel
    /// manipulation is left as a TODO placeholder.
    ///
    /// Spec notes (PDF ISO 32000):
    /// - Processing order (ideal): decode samples (/Decode) -> color space conversion -> masking (color key, ImageMask, SMask) -> paint.
    /// - In the new pipeline, decoders are responsible for /Decode and color management where applicable.
    /// - Image mask (/ImageMask true) is handled as a stencil that paints with current nonstroking color.
    /// - Color-key masking (/Mask array) is performed inside raw decoder when supported.
    /// - 16 bpc support is pending inside decoders.
    /// </summary>
    public class FastImageDrawer : IImageDrawer
    {
        public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, PdfPage page, SKRect destRect)
        {
            if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
            {
                return;
            }

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page, destRect);
            softMaskScope.BeginDrawContent();

            var decoder = PdfImageDecoder.GetDecoder(pdfImage);
            if (decoder == null)
            {
                Console.Error.WriteLine($"[PdfReader] No decoder for image '{pdfImage?.Name}' of type {pdfImage?.Type}. Skipping.");
                return;
            }

            using var baseImage = decoder.Decode();
            if (baseImage == null)
            {
                Console.Error.WriteLine($"[PdfReader] Decoder returned null for image '{pdfImage?.Name}'. Skipping.");
                return;
            }

            using var paint = PdfPaintFactory.CreateImagePaint(state, page);
            if (pdfImage.Interpolate)
            {
                paint.FilterQuality = SKFilterQuality.High;
            }

            // 0) Image mask path first
            if (pdfImage.HasImageMask)
            {
                DrawImageMask(canvas, baseImage, state, page, destRect, pdfImage.Interpolate);
                return;
            }

            // 1) Apply soft mask if present (decoders handle /Decode and color conversions now)
            bool smApplied;
            using var sm = ApplyImageSoftMask(baseImage, pdfImage, page, out smApplied);
            var finalImage = sm ?? baseImage;

            // 2) Draw final image
            canvas.DrawImage(finalImage, destRect, paint);

            softMaskScope.EndDrawContent();
        }

        private void DrawImageMask(SKCanvas canvas, SKImage alphaMask, PdfGraphicsState state, PdfPage page, SKRect destRect, bool interpolate)
        {
            using var fillPaint = PdfPaintFactory.CreateFillPaint(state, page);
            using var maskPaint = new SKPaint
            {
                BlendMode = SKBlendMode.DstIn,
                FilterQuality = interpolate ? SKFilterQuality.High : SKFilterQuality.None
            };

            canvas.SaveLayer(destRect, null);
            try
            {
                canvas.DrawRect(destRect, fillPaint);
                canvas.DrawImage(alphaMask, destRect, maskPaint);
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
                var dict = pdfImage.ImageXObject?.Dictionary;
                var smObj = dict?.GetPageObject(PdfTokens.SoftMaskKey);
                if (smObj == null)
                {
                    return null;
                }

                var smImageDesc = PdfImage.FromXObject(smObj, page, pdfImage.Name, isSoftMask: true);

                var smDecoder = PdfImageDecoder.GetDecoder(smImageDesc);
                using var maskImage = smDecoder?.Decode();
                if (maskImage == null)
                {
                    return null;
                }

                var matte = smObj.Dictionary?.GetArray(PdfTokens.MatteKey);
                if (matte != null && matte.Count > 0)
                {
                    Console.WriteLine($"[PdfReader] Image '{pdfImage?.Name}': /SMask has /Matte; dematting is not implemented in FastImageDrawer.");
                }

                // Decoders (including JPEG) are assumed to have applied /Decode already.
                // If the soft mask image already carries alpha (Alpha8 or non-opaque alpha), we can use it directly.
                // Otherwise, derive alpha from luminance.
                SKColorFilter alphaFilter = null;
                bool maskHasAlpha = maskImage.ColorType == SKColorType.Alpha8 || maskImage.AlphaType != SKAlphaType.Opaque;
                if (!maskHasAlpha)
                {
                    alphaFilter = SoftMaskUtilities.CreateAlphaFromLuminosityFilter();
                }

                int offW = Math.Max(1, source.Width);
                int offH = Math.Max(1, source.Height);
                using var surface = SKSurface.Create(new SKImageInfo(offW, offH, SKColorType.Rgba8888, SKAlphaType.Premul));
                if (surface == null)
                {
                    return null;
                }

                using var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                // Draw source first
                canvas.DrawImage(source, new SKRect(0, 0, offW, offH), new SKPaint());

                // Apply mask using DstIn; set filter quality to respect interpolation flag
                using var maskPaint = new SKPaint
                {
                    BlendMode = SKBlendMode.DstIn,
                    ColorFilter = alphaFilter,
                    FilterQuality = pdfImage.Interpolate ? SKFilterQuality.High : SKFilterQuality.None
                };
                canvas.DrawImage(maskImage, new SKRect(0, 0, offW, offH), maskPaint);
                canvas.Flush();

                alphaFilter?.Dispose();

                maskApplied = true;
                return surface.Snapshot();
            }
            catch
            {
                // Swallow and fallback to base image; upstream will draw without soft mask.
                return null;
            }
        }
    }
}
