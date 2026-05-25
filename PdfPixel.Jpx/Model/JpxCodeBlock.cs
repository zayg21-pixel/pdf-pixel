using System;

namespace PdfPixel.Jpx.Model;

/// <summary>
/// Represents a code-block with entropy-coded data from a packet.
/// In JPEG 2000, code-blocks are persistent per-precinct objects that accumulate
/// data across quality layers. Each layer adds coding passes and data bytes.
/// </summary>
internal sealed class JpxCodeBlock
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Accumulated entropy-coded data across all layers for this code-block.
    /// The underlying array may have excess capacity; use <see cref="DataLength"/> on persistent
    /// blocks or <see cref="Memory{T}.Length"/> on snapshots to determine valid byte count.
    /// </summary>
    public Memory<byte> Data { get; set; }

    /// <summary>
    /// Number of valid bytes currently stored in <see cref="Data"/>.
    /// On persistent blocks this tracks accumulated length as layers are appended.
    /// On snapshot blocks this equals the full accumulated data length at snapshot time.
    /// </summary>
    public int DataOffset { get; set; }

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
    /// Per-layer data bytes only (for packet-level binary comparison).
    /// Empty on persistent blocks; populated only on layer snapshots as a slice into <see cref="Data"/>.
    /// </summary>
    public Memory<byte> LayerData { get; set; }

    /// <summary>
    /// Number of coding passes for the current layer (transient, used during body parsing).
    /// After body parsing calls <see cref="AppendLayer"/>, this is added to <see cref="CodingPasses"/>.
    /// </summary>
    public int LayerCodingPasses { get; set; }

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
    /// Decoded wavelet coefficients after Tier-1 decoding (row-major, height * width).
    /// Populated by <see cref="PdfPixel.Jpx.Decoding.JpxTier1Decoder"/>.
    /// </summary>
    public int[] DecodedCoefficients { get; set; } = [];

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

    /// <summary>
    /// Creates a snapshot that shares the underlying data buffer via <see cref="Memory{T}"/> slicing.
    /// The snapshot's <see cref="Data"/> is a slice of the persistent block's buffer up to the
    /// current accumulated length, and <see cref="LayerData"/> is a slice of just this layer's bytes.
    /// No byte arrays are copied.
    /// </summary>
    /// <param name="layerDataLength">Number of bytes contributed by this layer.</param>
    /// <param name="layerCodingPasses">Number of coding passes contributed by this layer.</param>
    /// <returns>A new <see cref="JpxCodeBlock"/> with sliced memory references into the persistent buffer.</returns>
    public JpxCodeBlock CreateLayerSnapshot(int layerDataLength, int layerCodingPasses)
    {
        // Slice the accumulated data (zero-copy)
        Memory<byte> fullData = (DataOffset > 0)
            ? Data.Slice(0, DataOffset)
            : Memory<byte>.Empty;

        // Slice just this layer's contribution from the end of accumulated data (zero-copy)
        Memory<byte> layerSlice = (layerDataLength > 0 && DataOffset >= layerDataLength)
            ? Data.Slice(DataOffset - layerDataLength, layerDataLength)
            : Memory<byte>.Empty;

        return new JpxCodeBlock
        {
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            SubbandIndex = SubbandIndex,
            ResolutionLevel = ResolutionLevel,
            ZeroBitPlanes = ZeroBitPlanes,
            DataLength = layerDataLength,
            LayerCodingPasses = layerCodingPasses,
            LayerData = layerSlice,
            Data = fullData,
            DataOffset = DataOffset,
            CodingPasses = CodingPasses
        };
    }
}
