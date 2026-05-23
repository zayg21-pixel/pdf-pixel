using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Arithmetic text-region decoder (ITU-T T.88 Section 6.4).
/// All state is supplied via <see cref="Jbig2ArithmeticContext"/>; this class is stateless
/// and every method is static.
/// </summary>
internal static class Jbig2ArithmeticDecoder
{
    /// <summary>
    /// Decodes a text region using a shared arithmetic reader (e.g. from a symbol dictionary
    /// aggregate decode). The reader state advances in place; the caller retains ownership.
    /// Decoded symbol instances are appended to <paramref name="placements"/>; this method does
    /// not materialise the region bitmap.
    /// </summary>
    /// <param name="reader">Active arithmetic reader to consume bits from.</param>
    /// <param name="context">Decode context carrying probability state and placement parameters.</param>
    /// <param name="placements">Sink that records each placed symbol with its region-local coordinates.</param>
    /// <param name="symbols">Available symbol bitmaps referenced by symbol IDs.</param>
    /// <param name="numberOfSymbolInstances">Total number of symbol instances to place.</param>
    internal static void Decode(
        ref Jbig2ArithmeticReader reader,
        Jbig2ArithmeticContext context,
        Jbig2TextRegionPlacements placements,
        List<Jbig2Bitmap> symbols,
        int numberOfSymbolInstances)
    {
        context.InlineLevel++;
        if (context.InlineLevel > Jbig2ArithmeticContext.InlineLimit)
        {
            context.InlineLevel--;
            return;
        }

        // Initial IADT decode
        reader.DecodeInteger(context.Iadt, out int initialDeltaT);
        int stripT = -initialDeltaT;

        int firstS = 0;
        int instancesDecoded = 0;

        while (instancesDecoded < numberOfSymbolInstances)
        {
            if (!reader.DecodeInteger(context.Iadt, out int deltaT))
            {
                break;
            }

            stripT += deltaT;

            if (!reader.DecodeInteger(context.Iafs, out int deltaFirstS))
            {
                break;
            }

            firstS += deltaFirstS;
            int currentS = firstS;

            while (true)
            {
                int currentT = 0;
                if (context.PlacementFlags.StripSize > 1)
                {
                    reader.DecodeInteger(context.Iait, out int instanceT);
                    currentT = instanceT;
                }

                int t = context.PlacementFlags.StripSize * stripT + currentT;

                int symbolId = reader.DecodeIaid(context.IaId, context.SymbolCodeLength);

                bool applyRefinement = false;
                if (context.PlacementFlags.UseRefinement)
                {
                    reader.DecodeInteger(context.Iari, out int ri);
                    applyRefinement = (ri != 0);
                }

                if (symbolId < 0 || symbolId >= symbols.Count)
                {
                    instancesDecoded++;

                    if (!reader.DecodeInteger(context.Iads, out int _))
                    {
                        break;
                    }

                    if (instancesDecoded >= numberOfSymbolInstances)
                    {
                        break;
                    }

                    continue;
                }

                var symbolBitmap = symbols[symbolId];
                int symbolWidth = symbolBitmap.Width;
                int symbolHeight = symbolBitmap.Height;

                if (applyRefinement)
                {
                    reader.DecodeInteger(context.Iardw, out int rdw);
                    reader.DecodeInteger(context.Iardh, out int rdh);
                    reader.DecodeInteger(context.Iardx, out int rdx);
                    reader.DecodeInteger(context.Iardy, out int rdy);

                    symbolWidth += rdw;
                    symbolHeight += rdh;

                    symbolBitmap = Jbig2RefinementRegionDecoder.DecodeInline(
                        ref reader,
                        context,
                        symbolWidth,
                        symbolHeight,
                        symbolBitmap,
                        (rdw >> 1) + rdx,
                        (rdh >> 1) + rdy);
                }

                // Compute increment and adjust currentS based on reference corner
                int increment = 0;
                if (!context.PlacementFlags.Transposed)
                {
                    if (context.PlacementFlags.ReferenceCorner > 1)
                    {
                        currentS += symbolWidth - 1;
                    }
                    else
                    {
                        increment = symbolWidth - 1;
                    }
                }
                else
                {
                    if ((context.PlacementFlags.ReferenceCorner & 1) == 0)
                    {
                        currentS += symbolHeight - 1;
                    }
                    else
                    {
                        increment = symbolHeight - 1;
                    }
                }

                // Compute placement offsets.
                // For non-transposed (S=X, T=Y): TI adjusts T, SI adjusts S.
                // For transposed (S=Y, T=X): SI adjusts T (→X), TI adjusts S (→Y).
                // ITU-T T.88 Table 12: SI uses WI (pixel width), TI uses HI (pixel height).
                int offsetT;
                int offsetS;
                if (context.PlacementFlags.Transposed)
                {
                    offsetT = t - ((context.PlacementFlags.ReferenceCorner & 2) != 0 ? symbolWidth - 1 : 0);
                    offsetS = currentS - ((context.PlacementFlags.ReferenceCorner & 1) != 0 ? 0 : symbolHeight - 1);
                }
                else
                {
                    offsetT = t - ((context.PlacementFlags.ReferenceCorner & 1) != 0 ? 0 : symbolHeight - 1);
                    offsetS = currentS - ((context.PlacementFlags.ReferenceCorner & 2) != 0 ? symbolWidth - 1 : 0);
                }

                int placeX;
                int placeY;
                if (context.PlacementFlags.Transposed)
                {
                    placeX = offsetT;
                    placeY = offsetS;
                }
                else
                {
                    placeX = offsetS;
                    placeY = offsetT;
                }

                placements.Add(symbolBitmap, placeX, placeY);

                instancesDecoded++;

                // Always read IADS after each symbol placement (ITU-T T.88 Section 6.4.8).
                // OOB signals the end of the current strip. The encoder writes OOB even after the
                // last symbol instance, so we must consume it to keep the arithmetic reader
                // synchronised for any subsequent decodes from the same bitstream.
                if (!reader.DecodeInteger(context.Iads, out int deltaS))
                {
                    break;
                }

                if (instancesDecoded >= numberOfSymbolInstances)
                {
                    break;
                }

                currentS += increment + deltaS + context.PlacementFlags.SOffset;
            }
        }

        context.InlineLevel--;
    }
}
