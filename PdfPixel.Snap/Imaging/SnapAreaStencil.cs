using PdfPixel.Geometry;
using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Imaging;

/// <summary>
/// A stencil mask drawn by the glyph rule, as in a Type 3 glyph: its painting sample cells form one outline that is
/// antialiased at its exact position.
/// </summary>
internal sealed class SnapAreaStencil : SnapImageStencil
{
    private const int BitsPerByte = 8;

    private readonly SnapImageReader _reader;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _paintsOnZero;
    private readonly PdfMatrix _sampleToDevice;
    private readonly bool _areaCoverage;
    private readonly PdfIntegerRectangle _limits;
    private readonly SnapScratchBuffer _scratch;

    public SnapAreaStencil(
        SnapImageReader reader,
        int width,
        int height,
        bool paintsOnZero,
        in PdfMatrix unitToDevice,
        bool areaCoverage,
        in PdfIntegerRectangle limits,
        SnapScratchBuffer scratch)
    {
        _reader = reader;
        _width = width;
        _height = height;
        _paintsOnZero = paintsOnZero;
        _sampleToDevice = unitToDevice.PreConcat(PdfMatrix.CreateScale(1f / width, 1f / height));
        _areaCoverage = areaCoverage;
        _limits = limits;
        _scratch = scratch;
    }

    /// <inheritdoc />
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public override void Rasterize<TSink>(ref TSink sink)
    {
        // TODO: [MEDIUM] Rasterized at every draw; once Type 3 glyph recordings are cached, rasterize it once into a
        // SnapStencil per quarter-pixel position, as glyph outlines are.
        SnapEdgeList edges = _scratch.Edges;

        edges.Clear();

        for (int row = 0; row < _height; row++)
        {
            AddRowCells(edges, _reader.ReadRow(row), row);
        }

        _scratch.Rasterizer.Rasterize(edges, PdfPathFillType.Winding, _areaCoverage, _limits, ref sink);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void AddRowCells(SnapEdgeList edges, in ReadOnlySpan<byte> bits, int row)
    {
        ref byte bitsReference = ref MemoryMarshal.GetReference(bits);
        int runStart = -1;

        for (int column = 0; column <= _width; column++)
        {
            bool paints = column < _width && IsPainting(ref bitsReference, column);

            if (paints && runStart < 0)
            {
                runStart = column;
            }
            else if (!paints && runStart >= 0)
            {
                edges.AddQuadrilateral(
                    _sampleToDevice.MapPoint(new PdfPoint(runStart, row)),
                    _sampleToDevice.MapPoint(new PdfPoint(column, row)),
                    _sampleToDevice.MapPoint(new PdfPoint(column, row + 1)),
                    _sampleToDevice.MapPoint(new PdfPoint(runStart, row + 1)));
                runStart = -1;
            }
        }
    }

    private bool IsPainting(ref byte bits, int column)
    {
        int bit = (Unsafe.Add(ref bits, column >> 3) >> (BitsPerByte - 1 - (column & 7))) & 1;

        return (bit == 0) == _paintsOnZero;
    }

    /// <inheritdoc />
    public override void Dispose() => _reader.Dispose();
}
