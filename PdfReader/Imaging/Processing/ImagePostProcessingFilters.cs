using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Imaging.Model;
using SkiaSharp;

namespace PdfReader.Imaging.Processing;

internal class ImagePostProcessingFilters
{
    /// <summary>
    /// Builds a composed SKColorFilter for all post-processing filters for a PDF image.
    /// This includes Decode mapping, color space conversion, matte dematting, mask alpha channel, and soft mask luminocity-to-alpha if applicable.
    /// </summary>
    /// <param name="pdfImage">The PdfImage containing image properties.</param>
    /// <param name="colorConverted">If true, color has already been converted.</param>
    /// <param name="baseFilter">An optional base SKColorFilter to compose with.</param>
    /// <returns>The composed SKColorFilter, or null if no filters are needed.</returns>
    public static SKColorFilter BuildImageFilter(PdfImage pdfImage, bool colorConverted, SKColorFilter baseFilter = null)
    {
        if (pdfImage == null)
        {
            return baseFilter;
        }

        SKColorFilter filter = baseFilter;

        if (!colorConverted)
        {
            // Step 1: Apply Decode filter if present.
            ComposeColorFilter(ref filter, BuildDecodeFilter(pdfImage.DecodeArray, pdfImage.ColorSpaceConverter.Components, pdfImage.HasImageMask));

            // Step 2: Apply Mask filter (color key mask or soft mask) if present and supported.
            if (pdfImage.MaskArray != null && pdfImage.MaskArray.Length > 0)
            {
                var maskFilter = SoftMaskFilter.BuildMaskColorFilter(
                    pdfImage.MaskArray,
                    pdfImage.ColorSpaceConverter is not IndexedConverter,
                    pdfImage.BitsPerComponent
                );
                ComposeColorFilter(ref filter, maskFilter);
            }

            // Step 3: Apply color space conversion filter if available.
            ComposeColorFilter(ref filter, pdfImage.ColorSpaceConverter.AsColorFilter(pdfImage.RenderingIntent));
        }

        // Step 4: If this image is a soft mask, apply luminocity-to-alpha filter.
        if (pdfImage.IsSoftMask)
        {
            var luminosityToAlphaFilter = SKColorFilter.CreateLumaColor();
            ComposeColorFilter(ref filter, luminosityToAlphaFilter);
        }

        // Step 5: Apply Matte dematting filter if pdfImage.MatteArray is present.
        if (pdfImage.MatteArray != null && pdfImage.MatteArray.Length > 0)
        {
            SKColor matteColor = pdfImage.ColorSpaceConverter.ToSrgb(pdfImage.MatteArray, pdfImage.RenderingIntent);
            var dematteFilter = SoftMaskFilter.BuildDematteColorFilter(matteColor);
            ComposeColorFilter(ref filter, dematteFilter);
        }

        return filter;
    }

    /// <summary>
    /// Builds the decode filter to remap sample values according to the /Decode array.
    /// </summary>
    private static SKColorFilter BuildDecodeFilter(float[] decode, int components, bool isMask)
    {
        if (isMask)
        {
            return ColorFilterDecode.BuildMaskDecodeFilter(decode);
        }
        else
        {
            return ColorFilterDecode.BuildDecodeColorFilter(decode, components);
        }
    }

    /// <summary>
    /// Composes the given color filter with the existing filter, if any.
    /// </summary>
    /// <param name="filter">The SKColorFilter to modify (by ref).</param>
    /// <param name="newFilter">The new SKColorFilter to compose.</param>
    private static void ComposeColorFilter(ref SKColorFilter filter, SKColorFilter newFilter)
    {
        if (newFilter == null)
        {
            return;
        }

        if (filter == null)
        {
            filter = newFilter;
        }
        else
        {
            filter = SKColorFilter.CreateCompose(filter, newFilter);
        }
    }
}
