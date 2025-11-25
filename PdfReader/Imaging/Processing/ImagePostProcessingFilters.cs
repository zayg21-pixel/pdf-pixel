using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Imaging.Decoding;
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
    /// <param name="decodingResult">The result of the image decoding process.</param>
    /// <param name="baseFilter">An optional base SKColorFilter to compose with.</param>
    /// <returns>The composed SKColorFilter, or null if no filters are needed.</returns>
    public static SKColorFilter BuildImageFilter(PdfImage pdfImage, PdfImageDecodingResult decodingResult, SKColorFilter baseFilter = null)
    {
        if (pdfImage == null)
        {
            return baseFilter;
        }

        SKColorFilter filter = baseFilter;

        // Step 1: Apply Decode filter if present.
        if (!decodingResult.DecodeApplied && !pdfImage.HasImageMask)
        {
            ComposeColorFilter(ref filter, MatrixColorFilters.BuildDecodeColorMatrix(pdfImage.DecodeArray, pdfImage.ColorSpaceConverter.Components));
        }

        // Step 2: Apply Mask filter (color key mask or soft mask) if present and supported.
        if (!decodingResult.MaskRemoved)
        {
            var maskFilter = SoftMaskFilter.BuildMaskColorFilter(
                pdfImage.MaskArray,
                pdfImage.ColorSpaceConverter is not IndexedConverter,
                pdfImage.BitsPerComponent
            );
            ComposeColorFilter(ref filter, maskFilter);
        }

        // Step 3: Apply color space conversion filter if available.
        if (!decodingResult.ColorConverted)
        {
            ComposeColorFilter(ref filter, pdfImage.ColorSpaceConverter.AsColorFilter(pdfImage.RenderingIntent));
        }

        // Step 4: If this image is a soft mask, apply luminocity-to-alpha filter.
        if (!decodingResult.AlphaSet)
        {
            if (pdfImage.IsSoftMask)
            {
                ComposeColorFilter(ref filter, MatrixColorFilters.BuildGrayAlphaColorMatrix(inverse: false));
            }
            else if (pdfImage.HasImageMask)
            {
                bool inverse = pdfImage.DecodeArray == null || (pdfImage.DecodeArray.Length == 2 && pdfImage.DecodeArray[0] < pdfImage.DecodeArray[1]);
                ComposeColorFilter(ref filter, MatrixColorFilters.BuildGrayAlphaColorMatrix(inverse));
            }
        }

        // Step 5: Apply Matte dematting filter if pdfImage.MatteArray is present.
        if (!decodingResult.MatteRemoved && pdfImage.MatteArray != null)
        {
            SKColor matteColor = pdfImage.ColorSpaceConverter.ToSrgb(pdfImage.MatteArray, pdfImage.RenderingIntent);
            var dematteFilter = SoftMaskFilter.BuildDematteColorFilter(matteColor);
            ComposeColorFilter(ref filter, dematteFilter);
        }

        return filter;
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
