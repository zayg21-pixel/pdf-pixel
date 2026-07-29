using System;

namespace PdfPixel.Jpx.Model;

/// <summary>
/// Represents a code-block with entropy-coded data from a packet.
/// In JPEG 2000, code-blocks are persistent per-precinct objects that accumulate
/// data across quality layers. Each layer adds coding passes and data bytes.
/// A code-block is entropy-decoded once, after every layer has been accumulated.
/// </summary>
internal sealed class JpxCodeBlock
{
    /// <summary>
    /// Coding passes the first segment holds when the arithmetic coder is bypassed, covering
    /// every pass of the first four bit-planes before the first switch to raw coding.
    /// </summary>
    private const int PassesBeforeFirstBypass = 10;

    /// <summary>
    /// Coding passes one segment holds when the coding style never terminates early, which is
    /// more than the most bit-planes a subband can carry can produce.
    /// </summary>
    private const int AllPassesInOneSegment = 109;

    /// <summary>
    /// Column of the code-block's first sample within its subband.
    /// </summary>
    public int SubbandX { get; set; }

    /// <summary>
    /// Row of the code-block's first sample within its subband.
    /// </summary>
    public int SubbandY { get; set; }

    /// <summary>
    /// Width of the code-block in samples. Blocks on a partition or precinct edge are narrower
    /// than the nominal code-block width.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the code-block in samples.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Buffer holding the entropy-coded data accumulated across all layers.
    /// The buffer may have excess capacity; <see cref="CodedData"/> exposes the valid bytes.
    /// </summary>
    public Memory<byte> Data { get; set; }

    /// <summary>
    /// Number of valid bytes currently stored in <see cref="Data"/>,
    /// tracking accumulated length as layers are appended.
    /// </summary>
    public int DataOffset { get; set; }

    /// <summary>
    /// Entropy-coded bytes accumulated across every layer this code-block appears in.
    /// </summary>
    public ReadOnlySpan<byte> CodedData => Data.Span.Slice(0, DataOffset);

    /// <summary>
    /// Number of zero bit-planes to skip before decoding.
    /// Set on first inclusion and does not change across layers.
    /// </summary>
    public int ZeroBitPlanes { get; set; }

    /// <summary>
    /// Total number of coding passes accumulated across all layers.
    /// </summary>
    public int CodingPasses { get; set; }

    /// <summary>
    /// Codeword segments in codestream order. Only the first <see cref="SegmentCount"/>
    /// entries hold data.
    /// </summary>
    private JpxCodeBlockSegment[] _segments = [];

    /// <summary>
    /// Number of codeword segments this code-block has been split into so far.
    /// </summary>
    public int SegmentCount { get; private set; }

    /// <summary>
    /// Index of the first segment the layer currently being parsed contributes to.
    /// </summary>
    public int LayerFirstSegment { get; private set; }

    /// <summary>
    /// Accesses a segment by index so its accumulated and per-layer counts can be updated in place.
    /// </summary>
    public ref JpxCodeBlockSegment SegmentAt(int index) => ref _segments[index];

    /// <summary>
    /// Selects the segment this layer's first coding pass belongs to, starting a new one when
    /// the code-block has no segments yet or the last one is already full, and returns its index.
    /// </summary>
    public int BeginLayerSegment(JpxCodingStyle codingStyle)
    {
        int index;

        if (SegmentCount == 0)
        {
            index = 0;
            StartSegment(index, codingStyle);
        }
        else
        {
            index = SegmentCount - 1;

            if (_segments[index].Passes == _segments[index].MaximumPasses)
            {
                index++;
                StartSegment(index, codingStyle);
            }
        }

        LayerFirstSegment = index;

        return index;
    }

    /// <summary>
    /// Starts the segment at <paramref name="index"/>, sizing it by the coding style's
    /// termination rules per ITU-T T.800 D.4.
    /// </summary>
    public void StartSegment(int index, JpxCodingStyle codingStyle)
    {
        if (_segments.Length <= index)
        {
            int capacity = Math.Max(index + 1, Math.Max(_segments.Length * 2, 4));
            var grown = new JpxCodeBlockSegment[capacity];
            _segments.AsSpan(0, SegmentCount).CopyTo(grown);
            _segments = grown;
        }

        _segments[index] = new JpxCodeBlockSegment { MaximumPasses = GetSegmentMaximumPasses(codingStyle, index) };

        SegmentCount = index + 1;
    }

    /// <summary>
    /// Commits the layer's contribution to <paramref name="index"/> once its bytes have been read.
    /// </summary>
    public void CommitSegment(int index)
    {
        ref JpxCodeBlockSegment segment = ref _segments[index];
        segment.Length += segment.NewLength;
        segment.Passes += segment.NewPasses;
        CodingPasses += segment.NewPasses;
    }

    /// <summary>
    /// Number of coding passes a segment holds. Terminating on every pass gives one pass per
    /// segment; bypassing the arithmetic coder gives ten passes before the first switch and
    /// then alternates between the two raw passes and the single arithmetic one.
    /// </summary>
    private int GetSegmentMaximumPasses(JpxCodingStyle codingStyle, int index)
    {
        if (codingStyle.UsesTerminationOnEachPass)
        {
            return 1;
        }

        if (codingStyle.UsesArithmeticBypass)
        {
            if (index == 0)
            {
                return PassesBeforeFirstBypass;
            }

            int previousMaximum = _segments[index - 1].MaximumPasses;

            return (previousMaximum == 1 || previousMaximum == PassesBeforeFirstBypass) ? 2 : 1;
        }

        return AllPassesInOneSegment;
    }

    /// <summary>
    /// Component this code-block belongs to.
    /// </summary>
    public int Component { get; set; }

    /// <summary>
    /// Subband index within the resolution level.
    /// For resolution 0: always 0 (LL).
    /// For resolution > 0: 0 = HL, 1 = LH, 2 = HH.
    /// </summary>
    public int SubbandIndex { get; set; }

    /// <summary>
    /// Resolution level this code-block belongs to (0 = lowest).
    /// </summary>
    public int ResolutionLevel { get; set; }

    /// <summary>
    /// Length indicator for coded data (ITU-T T.800 B.10.5). Initially 3.
    /// Incremented as additional length bits are signalled in packet headers.
    /// </summary>
    public int Lblock { get; set; } = 3;

    /// <summary>
    /// Appends one segment's bytes for the layer being parsed. Segments are stored end to end,
    /// so each one occupies the run of <see cref="CodedData"/> that follows the previous one.
    /// </summary>
    /// <param name="layerData">Entropy-coded data bytes from this segment.</param>
    public void AppendSegmentData(in ReadOnlySpan<byte> layerData)
    {
        if (layerData.Length == 0)
        {
            return;
        }

        int requiredCapacity = DataOffset + layerData.Length;

        if (Data.Length < requiredCapacity)
        {
            // Grow with doubling strategy to amortize allocations
            int newCapacity = Math.Max(requiredCapacity, Data.Length * 2);
            newCapacity = Math.Max(newCapacity, 64);
            var newBuffer = new byte[newCapacity];
            if (DataOffset > 0)
            {
                Data.Span.Slice(0, DataOffset).CopyTo(newBuffer);
            }

            Data = newBuffer;
        }

        layerData.CopyTo(Data.Span.Slice(DataOffset));
        DataOffset += layerData.Length;
    }
}
