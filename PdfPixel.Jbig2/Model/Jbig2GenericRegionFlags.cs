namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Decoded flags byte for a generic region segment (ITU-T T.88 Section 7.4.6.2).
/// Bit layout:
/// <list type="table">
///   <item><term>Bit 0</term><description>MMR – use MMR coding if set, arithmetic if clear.</description></item>
///   <item><term>Bits 1–2</term><description>GB_TEMPLATE – arithmetic template ID (0–3).</description></item>
///   <item><term>Bit 3</term><description>TPGDON – typical prediction for generic direct-coded regions.</description></item>
/// </list>
/// </summary>
internal readonly struct Jbig2GenericRegionFlags
{
    private readonly byte _flags;

    /// <summary>
    /// Initialises the flags from the raw flags byte at offset 17 of the region data.
    /// </summary>
    /// <param name="flagsByte">Raw flags byte.</param>
    public Jbig2GenericRegionFlags(byte flagsByte) => _flags = flagsByte;

    /// <summary>
    /// If <see langword="true"/>, the region is coded using MMR (Group 4);
    /// if <see langword="false"/>, arithmetic coding is used.
    /// </summary>
    public bool UseMmr => (_flags & 0x01) != 0;

    /// <summary>
    /// Arithmetic template identifier (0–3, GB_TEMPLATE). Ignored when <see cref="UseMmr"/> is <see langword="true"/>.
    /// </summary>
    public int TemplateId => (_flags >> 1) & 0x03;

    /// <summary>
    /// Whether typical prediction for generic direct-coded regions (TPGDON) is enabled.
    /// Ignored when <see cref="UseMmr"/> is <see langword="true"/>.
    /// </summary>
    public bool TypicalPrediction => (_flags & 0x08) != 0;
}
