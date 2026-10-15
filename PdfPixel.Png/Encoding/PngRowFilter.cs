using PdfPixel.Png.Decoding;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Png.Encoding;

/// <summary>
/// Applies the PNG row filters of filter method 0 to consecutive rows.
/// </summary>
internal sealed class PngRowFilter
{
    private const int FilterTypeCount = 5;

    private readonly bool _isAdaptive;
    private readonly int _bytesPerPixel;

    /// <summary>
    /// Output of each filter type: the filter type byte, then the filtered row.
    /// </summary>
    private readonly byte[][] _candidates;

    private readonly byte[] _previousRow;

    /// <summary>
    /// Initializes the filter for rows of up to <paramref name="maxRowBytes"/> bytes.
    /// </summary>
    /// <param name="isAdaptive">True to pick per row the filter type whose output has the smallest sum of absolute
    /// values read as signed bytes; false for filter type 0 (None) on every row.</param>
    /// <param name="bytesPerPixel">Bytes per complete pixel, rounded up to 1.</param>
    /// <param name="maxRowBytes">Byte length of the longest row to filter.</param>
    public PngRowFilter(bool isAdaptive, int bytesPerPixel, int maxRowBytes)
    {
        _isAdaptive = isAdaptive;
        _bytesPerPixel = bytesPerPixel;

        _candidates = new byte[FilterTypeCount][];
        for (int filterType = 0; filterType < FilterTypeCount; filterType++)
        {
            _candidates[filterType] = new byte[maxRowBytes + 1];
            _candidates[filterType][0] = (byte)filterType;
        }

        _previousRow = new byte[maxRowBytes];
    }

    /// <summary>
    /// Starts a new image or Adam7 pass: the row above the next row is all zeros.
    /// </summary>
    public void Reset() => Array.Clear(_previousRow, 0, _previousRow.Length);

    /// <summary>
    /// Filters <paramref name="rowBytes"/> bytes of <paramref name="row"/> and returns the buffer holding the
    /// filter type byte followed by the filtered row.
    /// </summary>
    public byte[] Filter(byte[] row, int rowBytes)
    {
        byte[] chosen = Apply(0, row, rowBytes);

        if (_isAdaptive)
        {
            long bestSum = SumAbsoluteSigned(chosen, rowBytes);

            for (int filterType = 1; filterType < FilterTypeCount; filterType++)
            {
                byte[] candidate = Apply(filterType, row, rowBytes);
                long sum = SumAbsoluteSigned(candidate, rowBytes);

                if (sum < bestSum)
                {
                    bestSum = sum;
                    chosen = candidate;
                }
            }
        }

        Buffer.BlockCopy(row, 0, _previousRow, 0, rowBytes);

        return chosen;
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private byte[] Apply(int filterType, byte[] row, int rowBytes)
    {
        byte[] output = _candidates[filterType];
        byte[] above = _previousRow;
        int bytesPerPixel = _bytesPerPixel;

        switch (filterType)
        {
            case 0:
            {
                Buffer.BlockCopy(row, 0, output, 1, rowBytes);
                break;
            }
            case 1:
            {
                for (int index = 0; index < rowBytes; index++)
                {
                    int left = (index >= bytesPerPixel) ? row[index - bytesPerPixel] : 0;
                    output[index + 1] = (byte)(row[index] - left);
                }

                break;
            }
            case 2:
            {
                for (int index = 0; index < rowBytes; index++)
                {
                    output[index + 1] = (byte)(row[index] - above[index]);
                }

                break;
            }
            case 3:
            {
                for (int index = 0; index < rowBytes; index++)
                {
                    int left = (index >= bytesPerPixel) ? row[index - bytesPerPixel] : 0;
                    output[index + 1] = (byte)(row[index] - ((left + above[index]) >> 1));
                }

                break;
            }
            default:
            {
                for (int index = 0; index < rowBytes; index++)
                {
                    int left = (index >= bytesPerPixel) ? row[index - bytesPerPixel] : 0;
                    int upLeft = (index >= bytesPerPixel) ? above[index - bytesPerPixel] : 0;
                    output[index + 1] = (byte)(row[index] - PngFilterUndo.Paeth(left, above[index], upLeft));
                }

                break;
            }
        }

        return output;
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static long SumAbsoluteSigned(byte[] candidate, int rowBytes)
    {
        long sum = 0;
        for (int index = 1; index <= rowBytes; index++)
        {
            sum += Math.Abs((int)(sbyte)candidate[index]);
        }

        return sum;
    }
}
