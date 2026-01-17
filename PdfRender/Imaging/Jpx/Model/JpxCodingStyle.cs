namespace PdfRender.Imaging.Jpx.Model;

/// <summary>
/// Represents coding style parameters from COD marker segment.
/// </summary>
internal sealed class JpxCodingStyle
{
    /// <summary>
    /// Gets or sets the coding style (Scod parameter).
    /// Bit 0: Precinct sizes defined (0=default 32768x32768, 1=custom sizes in segment)
    /// Bit 1: SOP marker segments
    /// Bit 2: EPH marker segments
    /// Bits 3-7: Reserved (must be 0)
    /// </summary>
    public byte Style { get; set; }

    /// <summary>
    /// Gets or sets the progression order (SGcod parameter).
    /// 0 = LRCP, 1 = RLCP, 2 = RPCL, 3 = PCRL, 4 = CPRL
    /// </summary>
    public byte ProgressionOrder { get; set; }

    /// <summary>
    /// Gets or sets the number of layers (SGcod parameter).
    /// </summary>
    public ushort NumberOfLayers { get; set; }

    /// <summary>
    /// Gets or sets the multiple component transform (SGcod parameter).
    /// 0 = No MCT, 1 = ICT (irreversible), 2 = RCT (reversible)
    /// </summary>
    public byte MultiComponentTransform { get; set; }

    /// <summary>
    /// Gets or sets the number of decomposition levels (SPcod parameter).
    /// </summary>
    public byte DecompositionLevels { get; set; }

    /// <summary>
    /// Gets or sets the code-block width exponent (SPcod parameter).
    /// Actual width = 2^(width + 2), range 2-10.
    /// </summary>
    public byte CodeBlockWidthExponent { get; set; }

    /// <summary>
    /// Gets or sets the code-block height exponent (SPcod parameter).
    /// Actual height = 2^(height + 2), range 2-10.
    /// </summary>
    public byte CodeBlockHeightExponent { get; set; }

    /// <summary>
    /// Gets or sets the code-block style (SPcod parameter).
    /// Bit 0: Selective arithmetic coding bypass
    /// Bit 1: Reset context probabilities
    /// Bit 2: Termination on each coding pass
    /// Bit 3: Vertically causal context
    /// Bit 4: Predictable termination
    /// Bit 5: Segmentation symbols
    /// </summary>
    public byte CodeBlockStyle { get; set; }

    /// <summary>
    /// Gets or sets the transform (SPcod parameter).
    /// 0 = 9-7 irreversible, 1 = 5-3 reversible
    /// </summary>
    public byte Transform { get; set; }

    /// <summary>
    /// Gets or sets the precinct size exponents for each resolution level (SPcod parameter).
    /// Each byte contains width exponent (4 high bits) and height exponent (4 low bits).
    /// Actual size = 2^exponent. If null, default precinct sizes are used.
    /// Array length should be DecompositionLevels + 1.
    /// </summary>
    public byte[] PrecinctSizeExponents { get; set; }

    /// <summary>
    /// Gets a value indicating whether the entropy coder uses partitions.
    /// </summary>
    public bool HasPartitions => (Style & 0x01) != 0;

    /// <summary>
    /// Gets a value indicating whether SOP marker segments are used.
    /// </summary>
    public bool HasSopMarkers => (Style & 0x02) != 0;

    /// <summary>
    /// Gets a value indicating whether EPH marker segments are used.
    /// </summary>
    public bool HasEphMarkers => (Style & 0x04) != 0;

    /// <summary>
    /// Gets the actual code-block width (2^(CodeBlockWidthExponent + 2)).
    /// </summary>
    public int CodeBlockWidth => 1 << (CodeBlockWidthExponent + 2);

    /// <summary>
    /// Gets the actual code-block height (2^(CodeBlockHeightExponent + 2)).
    /// </summary>
    public int CodeBlockHeight => 1 << (CodeBlockHeightExponent + 2);

    /// <summary>
    /// Gets a value indicating whether the transform is reversible (5-3).
    /// </summary>
    public bool IsReversibleTransform => Transform == 1;

    /// <summary>
    /// Gets a value indicating whether precinct sizes are explicitly specified.
    /// </summary>
    public bool HasPrecinctSizes => PrecinctSizeExponents != null && PrecinctSizeExponents.Length > 0;
}