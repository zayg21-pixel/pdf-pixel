using System;
using PdfPixel.Ccitt;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// MMR (Modified Modified READ) decoder for JBIG2 generic regions.
/// MMR is equivalent to CCITT Group 4 two-dimensional coding (ITU-T T.6).
/// Reuses the existing CCITT Group 4 decoder infrastructure.
/// </summary>
internal static class Jbig2MmrDecoder
{
    /// <summary>
    /// Decodes an MMR-coded generic region bitmap and reports the number of bytes consumed.
    /// Use this overload when multiple bitmaps are encoded sequentially in a single stream
    /// (e.g. halftone bit planes separated by EOFB markers).
    /// </summary>
    /// <param name="data">Encoded MMR data.</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <param name="bytesConsumed">Number of bytes consumed from <paramref name="data"/>.</param>
    /// <param name="observer">Observer to notify on each decoded row.</param>
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(in ReadOnlySpan<byte> data, int width, int height, out int bytesConsumed, IJBig2ExectionObserver? observer = null)
    {
        Jbig2Bitmap bitmap = new(width, height);
        CcittBitReader reader = new(in data, 0, 0, 0);

        var referenceChanges = new int[width + 2];
        referenceChanges[0] = width;
        int changesCount = 1;

        var runs = new int[width + 1];
        int runsCount = 0;

        for (int y = 0; y < height; y++)
        {
            observer?.Notify();

            runsCount = 0;
            ReadOnlySpan<int> refChanges = new(referenceChanges, 0, changesCount);
            CcittG4TwoDDecoder.DecodeTwoDLine(ref reader, width, refChanges, runs, ref runsCount);

            ReadOnlySpan<int> decodedRuns = new(runs, 0, runsCount);
            Span<byte> row = bitmap.GetRow(y);
            CcittRaster.RasterizeRuns(row, decodedRuns, 0, width, blackIs1: true);

            changesCount = CcittRaster.BuildReferenceChangeList(decodedRuns, width, referenceChanges);
        }

        // Consume the EOFB terminator (2 consecutive EOL codes) that follows each
        // MMR bitmap in JBIG2 halftone bit plane streams.
        reader.TryConsumeEol();
        reader.TryConsumeEol();

        bytesConsumed = reader.ConsumedByteIndex;
        return bitmap;
    }
}
