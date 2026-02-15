using PdfPixel.Parsing;
using System;

namespace PdfPixel.Imaging.Processing;

internal sealed class NearestNeighborRowConverter : IRowConverter
{
    private readonly int _components;
    private readonly int _bitsPerComponent;
    private readonly int _srcWidth;
    private readonly int _dstWidth;
    private readonly int _srcHeight;
    private readonly int _dstHeight;

    private readonly int[] _srcRowForDest;
    private readonly int[] _srcXForDest;
    private int _nextDestRowToWrite;

    public int BitsPerComponent => _bitsPerComponent;

    public NearestNeighborRowConverter(int components, int bitsPerComponent, int srcWidth, int dstWidth, int srcHeight, int dstHeight)
    {
        _components = components;
        _bitsPerComponent = bitsPerComponent;
        _srcWidth = srcWidth;
        _dstWidth = dstWidth;
        _srcHeight = srcHeight;
        _dstHeight = dstHeight;

        _srcRowForDest = PrecomputeSrcIndices(_srcHeight, _dstHeight);
        _srcXForDest = PrecomputeSrcIndices(_srcWidth, _dstWidth);
        _nextDestRowToWrite = 0;
    }

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

        var reader = new UintBitReader(sourceRow);
        var writer = new UintBitWriter(destRow);

        Span<uint> sourceSamples = stackalloc uint[_components];

        int currentSourceX = _srcXForDest.Length > 0 ? _srcXForDest[0] : -1;
        if (currentSourceX >= 0)
        {
            for (int pre = 0; pre < currentSourceX; pre++)
            {
                for (int c = 0; c < _components; c++)
                {
                    reader.ReadBits(_bitsPerComponent);
                }
            }
            for (int c = 0; c < _components; c++)
            {
                sourceSamples[c] = reader.ReadBits(_bitsPerComponent);
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
                    for (int skip = 0; skip < advance; skip++)
                    {
                        for (int c = 0; c < _components; c++)
                        {
                            reader.ReadBits(_bitsPerComponent);
                        }
                    }
                }
                for (int c = 0; c < _components; c++)
                {
                    sourceSamples[c] = reader.ReadBits(_bitsPerComponent);
                }
                currentSourceX = sx;
            }

            for (int c = 0; c < _components; c++)
            {
                writer.WriteBits(_bitsPerComponent, sourceSamples[c]);
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
            int s = (int)Math.Round(srcPos);
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
