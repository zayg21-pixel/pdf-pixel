using PdfPixel.Snap.Clipping;
using PdfPixel.Snap.Rasterization;
using System;

namespace PdfPixel.Snap;

/// <summary>
/// The reusable working rows, edge list, rasterizer and clip buffer of a surface, and the memory pool its layers are
/// drawn in.
/// </summary>
internal sealed class SnapScratchBuffer
{
    /// <summary>
    /// Initializes the buffers of a surface drawing its layers in <paramref name="memoryPool"/>.
    /// </summary>
    public SnapScratchBuffer(PdfSnapMemoryPool memoryPool) => MemoryPool = memoryPool ?? throw new ArgumentNullException(nameof(memoryPool));

    private readonly ScratchRow<float> _sourceTerms = new();
    private readonly ScratchRow<float> _backdropColors = new();
    private readonly ScratchRow<float> _groupResults = new();
    private readonly ScratchRow<float> _sourceColors = new();
    private readonly ScratchRow<float> _groupColors = new();
    private readonly ScratchRow<float> _sourceAlphas = new();
    private readonly ScratchRow<float> _rowColors = new();
    private readonly ScratchRow<float> _rowShapes = new();
    private readonly ScratchRow<float> _rowObjectShapes = new();
    private readonly ScratchRow<float> _rowAlphas = new();
    private readonly ScratchRow<float> _rowMask = new();
    private readonly ScratchRow<SnapCoverageRun> _stencilRuns = new();

    public PdfSnapMemoryPool MemoryPool { get; }

    public SnapEdgeList Edges { get; } = new();

    public SnapRasterizer Rasterizer { get; } = new();

    public SnapClipBuffer ClipBuffer { get; } = new();

    public Span<float> GetSourceTerms(int length) => _sourceTerms.Get(length);

    public Span<float> GetBackdropColors(int length) => _backdropColors.Get(length);

    public Span<float> GetGroupResults(int length) => _groupResults.Get(length);

    public Span<float> GetSourceColors(int length) => _sourceColors.Get(length);

    /// <summary>
    /// Returns a row for a closed group's colors in its own color space, before conversion.
    /// </summary>
    public Span<float> GetGroupColors(int length) => _groupColors.Get(length);

    public Span<float> GetSourceAlphas(int length) => _sourceAlphas.Get(length);

    public Span<float> GetRowColors(int length) => _rowColors.Get(length);

    public Span<float> GetRowShapes(int length) => _rowShapes.Get(length);

    /// <summary>
    /// Returns a row for an element's own shapes, coverage excluded.
    /// </summary>
    public Span<float> GetRowObjectShapes(int length) => _rowObjectShapes.Get(length);

    public Span<float> GetRowAlphas(int length) => _rowAlphas.Get(length);

    public Span<float> GetRowMask(int length) => _rowMask.Get(length);

    public Span<SnapCoverageRun> GetStencilRuns(int length) => _stencilRuns.Get(length);

    private sealed class ScratchRow<T>
    {
        private T[] _values = [];

        public Span<T> Get(int length)
        {
            if (_values.Length < length)
            {
                _values = new T[length];
            }

            return _values.AsSpan(0, length);
        }
    }
}
