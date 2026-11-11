using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Clipping;
using PdfPixel.Snap.Imaging;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void PlaceStencil<TSink>(SnapStencil stencil, int left, int top, in PdfIntegerRectangle limits, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        int firstRow = Math.Max(0, limits.Top - top);
        int endRow = Math.Min(stencil.Height, limits.Bottom - top);

        Span<SnapCoverageRun> placedRuns = Scratch.GetStencilRuns(stencil.Width);

        for (int row = firstRow; row < endRow; row++)
        {
            int runCount = 0;

            foreach (SnapCoverageRun run in stencil.GetRowRuns(row))
            {
                int runLeft = Math.Max(left + run.Left, limits.Left);
                int runRight = Math.Min(left + run.Left + run.Count, limits.Right);

                if (runRight > runLeft)
                {
                    placedRuns[runCount] = new SnapCoverageRun(runLeft, runRight - runLeft, run.IsFull);
                    runCount++;
                }
            }

            if (runCount > 0)
            {
                SnapCoverageRow coverageRow = new(top + row, left, placedRuns.Slice(0, runCount), stencil.GetRowValues(row));
                sink.AddRow(coverageRow);
            }
        }
    }

    /// <summary>
    /// Returns the stencil mask (ISO 32000-2 8.9.6.2), by the glyph rule in Type 3 glyph content and the image rule
    /// otherwise, or <see langword="null"/> when its samples are not one bit.
    /// </summary>
    private SnapImageStencil? CreateImageStencil(IPdfImageDecoder decoder, in SnapImageArea area)
    {
        PdfImageSampleInfo samples = SelectImageTiles(decoder, area).Samples;
        bool paintsOnZero = StencilPaintsOnZero(decoder.Decode);

        if (samples.ComponentCount * samples.BitsPerComponent != 1)
        {
            ReportUnsupported(PdfCommandKind.DrawImage);
            return null;
        }

        if (IsType3GlyphContent())
        {
            return CreateAreaStencil(decoder, samples, area, paintsOnZero);
        }
        else if (area.IsAligned)
        {
            return CreateAlignedStencil(decoder, samples, area, paintsOnZero);
        }
        else
        {
            SnapImageSourceSteps stencilSteps = SnapImageSourceSteps.ForStencil(samples);
            SnapImagePlacement coverage = CreatePlacement(decoder, readsAlpha: false, invertsSamples: paintsOnZero, samples, stencilSteps, area);

            return new SnapPlacedStencil(coverage, area.Footprint, GetDrawLimits(), Scratch);
        }
    }

    private SnapAreaStencil CreateAreaStencil(IPdfImageDecoder decoder, PdfImageSampleInfo samples, in SnapImageArea area, bool paintsOnZero)
    {
        PdfIntegerSize sampleSize = samples.Grid.ImageSize;
        SnapImageReader reader = new(decoder, readsAlpha: false, invertsSamples: false, samples, _executionContext.ExecutionObserver, 0, sampleSize.Width, Scratch.MemoryPool);

        return new SnapAreaStencil(reader, sampleSize.Width, sampleSize.Height, paintsOnZero, area.UnitToDevice, _executionContext.Parameters.Antialias, GetDrawLimits(), Scratch);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private SnapAlignedStencil CreateAlignedStencil(IPdfImageDecoder decoder, PdfImageSampleInfo samples, in SnapImageArea area, bool paintsOnZero)
    {
        PdfIntegerSize sampleSize = samples.Grid.ImageSize;
        PdfMatrix unitToDevice = area.UnitToDevice;
        PdfIntegerRectangle visible = area.Visible;
        PdfImageRowAxisMapping horizontal = PdfSnapGrid.GetImageColumnMapping(sampleSize.Width, visible, unitToDevice);
        PdfImageRowAxisMapping vertical = PdfSnapGrid.GetImageRowMapping(sampleSize.Height, visible, unitToDevice);
        bool mirrors = unitToDevice.ScaleX < 0;
        int width = visible.Width;
        var columnStarts = new int[width];
        var columnEnds = new int[width];
        int windowStart = sampleSize.Width;
        int windowEnd = 0;

        for (int column = 0; column < width; column++)
        {
            int destinationColumn = mirrors ? width - 1 - column : column;
            PdfImageRowSampleRange footprint = horizontal.GetFootprint(destinationColumn);

            columnStarts[column] = footprint.Start;
            columnEnds[column] = footprint.End;
            windowStart = Math.Min(windowStart, footprint.Start);
            windowEnd = Math.Max(windowEnd, footprint.End);
        }

        for (int column = 0; column < width; column++)
        {
            columnStarts[column] -= windowStart;
            columnEnds[column] -= windowStart;
        }

        SnapImageReader reader = new(decoder, readsAlpha: false, invertsSamples: false, samples, _executionContext.ExecutionObserver, windowStart, windowEnd - windowStart, Scratch.MemoryPool);

        return new SnapAlignedStencil(reader, vertical, visible, readsBottomUp: unitToDevice.ScaleY < 0, paintsOnZero, columnStarts, columnEnds);
    }

    private void DrawStencil<TPainter>(SnapImageStencil stencil, TPainter painter)
        where TPainter : ISnapRowPainter
    {
        SnapShapeRowSink<TPainter> rowSink = new(painter, Scratch);

        if (_state.Clip == null || _state.Clip.IsRectangle)
        {
            stencil.Rasterize(ref rowSink);
        }
        else
        {
            SnapClippedCoverageSink<SnapShapeRowSink<TPainter>> clippedSink = new(_state.Clip, Scratch.ClipBuffer, rowSink);
            stencil.Rasterize(ref clippedSink);
        }
    }

    private readonly struct SnapImageStencilGeometry : ISnapGeometry
    {
        private readonly PdfSnapSurface _surface;
        private readonly SnapImageStencil _stencil;

        public SnapImageStencilGeometry(PdfSnapSurface surface, SnapImageStencil stencil)
        {
            _surface = surface;
            _stencil = stencil;
        }

        /// <inheritdoc />
        public void Draw<TPainter>(TPainter painter)
            where TPainter : ISnapRowPainter
        {
            _surface.DrawStencil(_stencil, painter);
        }
    }
}
