using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Holds the QM-coder probability context arrays for one symbol dictionary arithmetic decode
/// session (ITU-T T.88 Section 6.5). Each array is a probability state vector for a named
/// integer-coding procedure defined by the specification.
/// </summary>
/// <remarks>
/// Mirrors the role of <see cref="Jbig2ArithmeticContext"/> for text regions.
/// Context arrays persist across the entire height-class loop so the probability model
/// stays synchronised with the encoder.
/// </remarks>
internal sealed class Jbig2SymbolArithmeticContext
{
    /// <summary>
    /// IADH – height-class delta-height integer coder context (512 states).
    /// </summary>
    internal readonly byte[] HeightContexts;

    /// <summary>
    /// IADW – symbol delta-width integer coder context (512 states).
    /// </summary>
    internal readonly byte[] WidthContexts;

    /// <summary>
    /// IAEX – export-flag run-length integer coder context (512 states).
    /// </summary>
    internal readonly byte[] ExportContexts;

    /// <summary>
    /// IAAI – aggregate instance-count integer coder context (512 states).
    /// </summary>
    internal readonly byte[] IaaiContexts;

    /// <summary>
    /// Generic region bitmap context. Size is <c>1 &lt;&lt; 16</c> for template 0,
    /// <c>1 &lt;&lt; 13</c> for template 1, <c>1 &lt;&lt; 10</c> for templates 2 and 3.
    /// </summary>
    internal readonly byte[] GenericContexts;

    /// <summary>
    /// Refinement/aggregate sub-context shared across all symbols in the height-class loop.
    /// All context arrays persist across inline aggregate decodes within the same session.
    /// </summary>
    internal readonly Jbig2ArithmeticContext AggregateContext;

    /// <summary>
    /// Symbol ID code length in bits. Matches the value used to construct
    /// <see cref="AggregateContext"/> and is kept here to avoid threaded parameter passing.
    /// </summary>
    internal readonly int SymbolCodeLength;

    /// <summary>
    /// The immutable parsed segment header this context was built from.
    /// Stored here so the static decoder does not need it as a separate parameter.
    /// </summary>
    internal readonly Jbig2SymbolDictionarySegmentInfo SegmentInfo;

    /// <summary>
    /// Initialises all context arrays and the aggregate sub-context from the supplied
    /// segment info and pre-computed symbol code length.
    /// </summary>
    /// <param name="segmentInfo">Parsed segment header (flags, AT pixels, symbol counts).</param>
    /// <param name="symbolCodeLength">
    /// IAID code length; determines <see cref="Jbig2ArithmeticContext.IaId"/> array size
    /// inside <see cref="AggregateContext"/>.
    /// </param>
    public Jbig2SymbolArithmeticContext(
        in Jbig2SymbolDictionarySegmentInfo segmentInfo,
        int symbolCodeLength)
    {
        SegmentInfo = segmentInfo;
        SymbolCodeLength = symbolCodeLength;

        HeightContexts = new byte[512];
        WidthContexts = new byte[512];
        ExportContexts = new byte[512];
        IaaiContexts = new byte[512];

        int genericContextSize = segmentInfo.Flags.Template switch
        {
            0 => 1 << 16,
            1 => 1 << 13,
            2 => 1 << 10,
            3 => 1 << 10,
            _ => 1 << 16
        };

        GenericContexts = new byte[genericContextSize];

        AggregateContext = new Jbig2ArithmeticContext(
            symbolCodeLength,
            segmentInfo.Flags.RefinementTemplate,
            segmentInfo.RefinementAtPixels?.AtX,
            segmentInfo.RefinementAtPixels?.AtY);
    }
}
