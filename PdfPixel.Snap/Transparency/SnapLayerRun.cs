using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// A run of a layer's pixels open for change: the pixels themselves, or a converted copy written back when the run
/// ends.
/// </summary>
internal readonly ref struct SnapLayerRun
{
    private readonly SnapLayerPixels? _convertedPixels;
    private readonly int _rowIndex;
    private readonly int _firstPixel;

    /// <summary>
    /// Initializes the run of <paramref name="valueCount"/> values from <paramref name="firstValue"/> on row
    /// <paramref name="rowIndex"/> of <paramref name="samples"/>, changed in place.
    /// </summary>
    public SnapLayerRun(PdfImageSamples samples, int rowIndex, int firstValue, int valueCount)
    {
        Values = MemoryMarshal.Cast<byte, float>(samples.GetRow(rowIndex)).Slice(firstValue, valueCount);
        _convertedPixels = null;
        _rowIndex = 0;
        _firstPixel = 0;
    }

    /// <summary>
    /// Initializes a run changing the first <paramref name="valueCount"/> of <paramref name="values"/>, a converted copy
    /// of the pixels from <paramref name="firstPixel"/> on row <paramref name="rowIndex"/> of
    /// <paramref name="convertedPixels"/>, written back when the run ends.
    /// </summary>
    public SnapLayerRun(float[] values, int valueCount, SnapLayerPixels convertedPixels, int rowIndex, int firstPixel)
    {
        Values = values.AsSpan(0, valueCount);
        _convertedPixels = convertedPixels;
        _rowIndex = rowIndex;
        _firstPixel = firstPixel;
    }

    /// <summary>
    /// Gets the premultiplied components and alpha of the run's pixels.
    /// </summary>
    public Span<float> Values { get; }

    /// <summary>
    /// Ends the change of the run, writing a converted copy back into the pixels.
    /// </summary>
    public void Dispose() => _convertedPixels?.WriteBack(Values, _rowIndex, _firstPixel);
}
