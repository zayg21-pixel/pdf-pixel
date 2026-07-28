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
    /// Length of code-block data for the current layer (used during packet body parsing).
    /// This is a transient value set by the packet header parser for each layer.
    /// </summary>
    public int DataLength { get; set; }

    /// <summary>
    /// Number of coding passes for the current layer (transient, used during body parsing).
    /// After body parsing calls <see cref="AppendLayer"/>, this is added to <see cref="CodingPasses"/>.
    /// </summary>
    public int LayerCodingPasses { get; set; }

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
    /// Appends a layer's contribution to this code-block.
    /// Copies the layer data into the internal buffer and adds coding passes to the total.
    /// </summary>
    /// <param name="layerData">Entropy-coded data bytes from this layer.</param>
    /// <param name="layerPasses">Number of coding passes contributed by this layer.</param>
    public void AppendLayer(in ReadOnlySpan<byte> layerData, int layerPasses)
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
        CodingPasses += layerPasses;
    }
}
