using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// CCITT Group 3 mixed 1D/2D decoder (K parameter) producing packed 1-bit output.
    /// Output polarity controlled by blackIs1 parameter.
    /// </summary>
    internal static class CcittG32DDecoder
    {
        public static byte[] Decode(ReadOnlySpan<byte> data, int width, int height, bool blackIs1, int k, bool endOfLine, bool byteAlign)
        {
            var reader = new CcittBitReader(data);
            byte[] buffer = CcittRaster.CreateBuffer(width, height, blackIs1);

            var referenceChanges = new List<int>(width + 8) { width };
            var nextReferenceChanges = new List<int>(width + 8);
            var runs = new List<int>(256);

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                bool isOneDLine;
                if (k == 0)
                {
                    if (endOfLine)
                    {
                        if (!reader.TryConsumeEol())
                        {
                            throw new System.InvalidOperationException("CCITT G3 2D decode error: missing required EOL at start of 1D row " + rowIndex + ".");
                        }
                        if (byteAlign)
                        {
                            reader.AlignAfterEndOfLine(true);
                        }
                    }
                    isOneDLine = true;
                }
                else if (k < 0)
                {
                    if (endOfLine)
                    {
                        if (!reader.TryConsumeEol())
                        {
                            throw new System.InvalidOperationException("CCITT G3 2D decode error: missing required EOL at start of 2D row " + rowIndex + ".");
                        }
                        if (byteAlign)
                        {
                            reader.AlignAfterEndOfLine(true);
                        }
                    }
                    isOneDLine = false;
                }
                else
                {
                    if (!reader.TryConsumeEol())
                    {
                        throw new System.InvalidOperationException("CCITT G3 2D decode error: missing EOL in mixed mode at row " + rowIndex + ".");
                    }
                    if (byteAlign)
                    {
                        reader.AlignAfterEndOfLine(true);
                    }
                    int tagBit = reader.ReadBit();
                    if (tagBit < 0)
                    {
                        throw new System.InvalidOperationException("CCITT G3 2D decode error: unexpected end of data reading tag bit at row " + rowIndex + ".");
                    }
                    isOneDLine = tagBit == 1;
                }

                runs.Clear();
                if (isOneDLine)
                {
                    CcittG3OneDDecoder.DecodeOneDCollectRuns(ref reader, width, false, false, runs);
                }
                else
                {
                    CcittG4TwoDDecoder.DecodeTwoDLine(ref reader, width, referenceChanges, runs, nextReferenceChanges);
                }

                CcittRaster.ValidateRunLengths(runs, width, rowIndex, "CCITT G3 2D");
                CcittRaster.RasterizeRuns(buffer, runs, rowIndex, width, blackIs1);
                CcittRaster.BuildReferenceChangeList(runs, width, nextReferenceChanges);
                var tmp = referenceChanges;
                referenceChanges = nextReferenceChanges;
                nextReferenceChanges = tmp;
            }

            return buffer;
        }
    }
}
