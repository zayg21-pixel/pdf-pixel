using PdfPixel.Parsing;
using System;

namespace PdfPixel.Imaging.Processing;

internal sealed class NearestNeighborRowConverter : IRowConverter
{
    private readonly int _components;
    private readonly int _srcWidth;
    private readonly int _dstWidth;
    private readonly int _srcHeight;
    private readonly int _dstHeight;

    private readonly int[] _srcRowForDest;
    private readonly int[] _srcXForDest;
    private int _nextDestRowToWrite;

    public NearestNeighborRowConverter(int components, int bitsPerComponent, int srcWidth, int dstWidth, int srcHeight, int dstHeight)
    {
        _components = components;
        BitsPerComponent = bitsPerComponent;
        _srcWidth = srcWidth;
        _dstWidth = dstWidth;
        _srcHeight = srcHeight;
        _dstHeight = dstHeight;

        _srcRowForDest = PrecomputeSrcIndices(_srcHeight, _dstHeight);
        _srcXForDest = PrecomputeSrcIndices(_srcWidth, _dstWidth);
        _nextDestRowToWrite = 0;
    }

    public int BitsPerComponent { get; }

    public bool TryConvertRow(int rowIndex, ReadOnlySpan<byte> sourceRow, Span<byte> destRow)
    {
        if (_nextDestRowToWrite >= _dstHeight)
        {
            return false;
        }

        int requiredSrcRow = _srcRowForDest[_nextDestRowToWrite];
        if (rowIndex != requiredSrcRow)
        {
            return false;
        }

        destRow.Clear();

        UintBitReaderFixedLength reader = new(sourceRow, BitsPerComponent);
        UintBitWriter writer = new(destRow);

        Span<uint> sourceSamples = stackalloc uint[_components];

        int currentSourceX = (_srcXForDest.Length > 0) ? _srcXForDest[0] : -1;
        if (currentSourceX >= 0)
        {
            if (currentSourceX > 0)
            {
                reader.Advance(currentSourceX * _components);
            }

            for (int c = 0; c < _components; c++)
            {
                sourceSamples[c] = reader.Read();
            }
        }

        for (int dx = 0; dx < _dstWidth; dx++)
        {
            int sx = _srcXForDest[dx];

            if (sx != currentSourceX)
            {
                int advance = sx - currentSourceX - 1;
                if (advance > 0)
                {
                    reader.Advance(advance * _components);
                }

                for (int c = 0; c < _components; c++)
                {
                    sourceSamples[c] = reader.Read();
                }

                currentSourceX = sx;
            }

            for (int c = 0; c < _components; c++)
            {
                writer.WriteBits(BitsPerComponent, sourceSamples[c]);
            }
        }

        _nextDestRowToWrite++;
        return true;
    }

    private static int[] PrecomputeSrcIndices(int srcLength, int dstLength)
    {
        var map = new int[dstLength];
        float scale = (float)dstLength / srcLength;

        for (int d = 0; d < dstLength; d++)
        {
            float srcPos = ((d + 0.5f) / scale) - 0.5f;
            var s = (int)Math.Round(srcPos);
            if (s < 0)
            {
                s = 0;
            }

            if (s >= srcLength)
            {
                s = srcLength - 1;
            }

            map[d] = s;
        }

        for (int d = 1; d < dstLength; d++)
        {
            if (map[d] < map[d - 1])
            {
                map[d] = map[d - 1];
            }
        }

        if (dstLength > 0)
        {
            map[dstLength - 1] = Math.Max(map[dstLength - 1], srcLength - 1);
        }

        return map;
    }
}
