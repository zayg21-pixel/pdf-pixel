namespace PdfPixel.Diagnostics;

/// <summary>
/// What a run collects about itself while it processes the pages.
/// </summary>
internal enum ProfileMode
{
    /// <summary>
    /// Nothing; the run only reports its own timings.
    /// </summary>
    None,

    /// <summary>
    /// Sampled call stacks, reported as the methods the run spent its time in.
    /// </summary>
    Cpu,

    /// <summary>
    /// Sampled managed allocations, reported as the types the run put on the heap and the methods
    /// they came from.
    /// </summary>
    Memory,

    /// <summary>
    /// A dump of the live heap taken while the document is still open, reported as the types holding
    /// the most bytes.
    /// </summary>
    Heap,
}
