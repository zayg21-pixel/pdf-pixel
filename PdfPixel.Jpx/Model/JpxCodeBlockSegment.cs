namespace PdfPixel.Jpx.Model;

/// <summary>
/// One codeword segment of a code-block. A segment is the run of coding passes the encoder
/// terminated the arithmetic coder on, so each one is decoded from its own starting state
/// (ITU-T T.800 D.4). Without the bypass or terminate-all coding styles a code-block has a
/// single segment covering every pass.
/// </summary>
internal struct JpxCodeBlockSegment
{
    /// <summary>
    /// Coding passes this segment can hold before the next one has to start.
    /// </summary>
    public int MaximumPasses;

    /// <summary>
    /// Coding passes accumulated into this segment across every layer parsed so far.
    /// </summary>
    public int Passes;

    /// <summary>
    /// Bytes accumulated into this segment across every layer parsed so far.
    /// </summary>
    public int Length;

    /// <summary>
    /// Coding passes the layer currently being parsed adds to this segment.
    /// </summary>
    public int NewPasses;

    /// <summary>
    /// Bytes the layer currently being parsed adds to this segment.
    /// </summary>
    public int NewLength;
}
