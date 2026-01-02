using PdfReader.Color.ColorSpace;
using PdfReader.Color.Structures;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Rendering.State;
using PdfReader.Streams;
using SkiaSharp;

namespace PdfReader.Imaging.Png;

/// <summary>
/// Decodes PNG images using SkiaSharp with an fast path for compatible FlateDecode + PNG predictor images.
/// </summary>
internal class PngSkiaDecoder
{
    /// <summary>
    /// Decodes a PDF image as PNG using PngImageBuilder for both FlateDecode and PNG predictor images.
    /// Returns null when incompatibilities are detected.
    /// </summary>
    public static SKImage DecodeAsPng(PdfImage image, PdfGraphicsState state)
    {
        if (image == null)
        {
            return null;
        }

        if (PdfImageRowProcessor.ShouldConvertColor(image))
        {
            return null;
        }

        var filters = PdfStreamDecoder.GetFilters(image.SourceObject);
        int? predictor = image.DecodeParms?.Predictor;

        // Return early if predictor is present but not PNG-compatible (10-15)
        if (!predictor.HasValue || predictor.Value < 10 || predictor.Value > 15)
        {
            return null;
        }

        if (filters.Count != 1 || filters[0] != PdfFilterType.FlateDecode)
        {
            return null;
        }

        int width = image.Width;
        int height = image.Height;
        int bpc = image.BitsPerComponent;
        RgbaPacked[] palette = null;

        var converter = image.ColorSpaceConverter;

        if (converter is IndexedConverter indexed)
        {
            palette = indexed.BuildPackedPalette(image.RenderingIntent, state.FullTransferFunction);
        }
        if (converter.Components == 1 && bpc <= 8)
        {
            palette = PdfImageRowProcessor.BuildSingleChannelPalette(image, state);
        }

        byte[] iccBytes = null;
        if (image.ColorSpaceConverter is IccBasedConverter iccBased)
        {
            iccBytes = iccBased.Profile?.Bytes;
        }

        using var builder = new PngImageBuilder(
            converter.Components,
            bpc,
            width,
            height);

        builder.Init(palette, iccBytes);

        if (filters.Count == 1 && filters[0] == PdfFilterType.FlateDecode)
        {
            using var rawEncoded = image.SourceObject.GetRawStream();
            builder.SetPngImageBytes(rawEncoded);
        }
        else
        {
            return null;
        }

        return builder.Build();
    }
}
