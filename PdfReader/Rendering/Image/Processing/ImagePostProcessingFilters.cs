using PdfReader.Rendering.Advanced;
using PdfReader.Rendering.Color;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;

namespace PdfReader.Rendering.Image.Processing
{
    internal class ImagePostProcessingFilters
    {
        /// <summary>
        /// Applies all post-processing filters for a PDF image to the specified SKPaint.
        /// This includes Decode mapping, color space conversion, matte dematting, mask alpha channel, and soft mask luminocity-to-alpha if applicable.
        /// </summary>
        /// <param name="paint">The SKPaint to apply filters to.</param>
        /// <param name="pdfImage">The PdfImage containing image properties.</param>
        /// <param name="colorConverted">If true, color has already been converted.</param>
        public static void ApplyImageFilters(SKPaint paint, PdfImage pdfImage, bool colorConverted)
        {
            // Fail fast if input is invalid.
            if (paint == null)
            {
                return;
            }

            if (pdfImage == null)
            {
                return;
            }

            if (!colorConverted)
            {
                // Step 1: Apply Decode filter if present.
                // This remaps decoded sample values according to the /Decode array.
                // PDF spec: decoding must occur before color conversion.
                ApplyDecodeFilter(paint, pdfImage.DecodeArray, pdfImage.ColorSpaceConverter.Components);

                // Step 2: Apply Mask filter (color key mask or soft mask) if present and supported.
                // Masking must be applied before color space conversion per PDF spec.
                if (pdfImage.MaskArray != null && pdfImage.MaskArray.Length > 0)
                {
                    using var maskFilter = SoftMaskFilter.BuildMaskColorFilter(
                        pdfImage.MaskArray,
                        pdfImage.ColorSpaceConverter is not IndexedConverter,
                        pdfImage.BitsPerComponent
                    );
                    ComposeColorFilter(paint, maskFilter);
                }

                // Step 3: Apply color space conversion filter if available.
                // Converts decoded samples to sRGB using the image's color space.
                // PDF spec: color conversion must occur after decoding and before masking.
                ComposeColorFilter(paint, pdfImage.ColorSpaceConverter.AsColorFilter(pdfImage.RenderingIntent));
            }

            // Step 4: If this image is a soft mask, apply luminocity-to-alpha filter.
            // Converts grayscale values to alpha for soft-masked images.
            // PDF spec: soft mask must be applied after color conversion.
            if (pdfImage.IsSoftMask)
            {
                ApplyLuminocityToAlphaFilter(paint);
            }

            // Step 5: Apply Matte dematting filter if pdfImage.MatteArray is present.
            // The matte color is used to 'dematte' the image, removing the effect of pre-blended background.
            // See PDF 2.0 spec §11.9.6 for dematting algorithm.
            // This requires both the decoded pixel color and the alpha mask.
            // Dematting must occur after color conversion and before masking, as it operates on color and alpha.
            if (pdfImage.MatteArray != null && pdfImage.MatteArray.Length > 0)
            {
                SKColor matteColor = pdfImage.ColorSpaceConverter.ToSrgb(pdfImage.MatteArray, pdfImage.RenderingIntent);
                using var dematteFilter = SoftMaskFilter.BuildDematteColorFilter(matteColor);
                ComposeColorFilter(paint, dematteFilter);
            }
        }

        /// <summary>
        /// Applies the decode filter to remap sample values according to the /Decode array.
        /// </summary>
        private static void ApplyDecodeFilter(SKPaint paint, float[] decode, int components)
        {
            if (decode == null || decode.Length != components * 2)
            {
                return;
            }

            using var decodeFilter = ColorFilterDecode.BuildDecodeColorFilter(decode, components);
            ComposeColorFilter(paint, decodeFilter);
        }

        /// <summary>
        /// Applies a filter that converts grayscale values to alpha for soft-masked images.
        /// </summary>
        private static void ApplyLuminocityToAlphaFilter(SKPaint paint)
        {
            using var luminosityToAlphaFilter = SoftMaskUtilities.CreateAlphaFromLuminosityFilter();
            ComposeColorFilter(paint, luminosityToAlphaFilter);
        }

        /// <summary>
        /// Composes the given color filter with the existing filter on the paint, if any.
        /// </summary>
        /// <param name="paint">The SKPaint to modify.</param>
        /// <param name="newFilter">The new SKColorFilter to compose.</param>
        private static void ComposeColorFilter(SKPaint paint, SKColorFilter newFilter)
        {
            if (newFilter == null)
            {
                return;
            }

            if (paint.ColorFilter == null)
            {
                paint.ColorFilter = newFilter;
            }
            else
            {
                using var composedFilter = SKColorFilter.CreateCompose(paint.ColorFilter, newFilter);
                paint.ColorFilter = composedFilter;
            }
        }
    }
}
