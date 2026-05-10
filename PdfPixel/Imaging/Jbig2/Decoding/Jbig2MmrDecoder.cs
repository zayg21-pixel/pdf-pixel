using System;
using System.Collections.Generic;
using PdfPixel.Imaging.Ccitt;
using PdfPixel.Imaging.Jbig2.Model;

namespace PdfPixel.Imaging.Jbig2.Decoding;

/// <summary>
/// MMR (Modified Modified READ) decoder for JBIG2 generic regions.
/// MMR is equivalent to CCITT Group 4 two-dimensional coding (ITU-T T.6).
/// Reuses the existing CCITT Group 4 decoder infrastructure.
/// </summary>
internal static class Jbig2MmrDecoder
{
    /// <summary>
    /// Decodes an MMR-coded generic region bitmap.
    /// </summary>
    /// <param name="data">Encoded MMR data.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(ReadOnlySpan<byte> data, int width, int height)
    {
        return Decode(data, width, height, out _);
    }

    /// <summary>
    /// Decodes an MMR-coded generic region bitmap and reports the number of bytes consumed.
    /// Use this overload when multiple bitmaps are encoded sequentially in a single stream
    /// (e.g. halftone bit planes separated by EOFB markers).
    /// </summary>
    /// <param name="data">Encoded MMR data.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <param name="bytesConsumed">Number of bytes consumed from <paramref name="data"/>.</param>
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(ReadOnlySpan<byte> data, int width, int height, out int bytesConsumed)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        var reader = new CcittBitReader(data, 0, 0, 0, msbFirst: true);

        var referenceChanges = new int[width + 2];
        referenceChanges[0] = width;
        int changesCount = 1;

        var runs = new List<int>(64);

        for (int y = 0; y < height; y++)
        {
            CcittG4TwoDDecoder.DecodeTwoDLine(ref reader, width, new Span<int>(referenceChanges, 0, changesCount), runs);

            // Rasterize runs directly into the bitmap row (JBIG2 uses blackIs1=true)
            Span<byte> row = bitmap.GetRow(y);
            CcittRaster.RasterizeRuns(row, runs, 0, width, blackIs1: true);

            changesCount = CcittRaster.BuildReferenceChangeList(runs, width, referenceChanges);
        }

        // Consume the EOFB terminator (2 consecutive EOL codes) that follows each
        // MMR bitmap in JBIG2 halftone bit plane streams.
        reader.TryConsumeEol();
        reader.TryConsumeEol();

        bytesConsumed = reader.ByteIndex;
        return bitmap;
    }
}
