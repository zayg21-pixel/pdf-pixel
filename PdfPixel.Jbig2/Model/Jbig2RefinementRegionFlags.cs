namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Decoded flags byte for a generic refinement region segment (ITU-T T.88 Section 7.4.7.2).
/// Bit layout:
/// <list type="table">
///   <item><term>Bit 0</term><description>GRTEMPLATE – refinement template (0 or 1).</description></item>
///   <item><term>Bit 1</term><description>TPGRON – typical prediction for generic refinement regions.</description></item>
/// </list>
/// </summary>
internal readonly struct Jbig2RefinementRegionFlags
{
    private readonly byte _flags;

    /// <summary>
    /// Initialises the flags from the raw flags byte at offset 17 of the refinement region data.
    /// </summary>
    /// <param name="flagsByte">Raw flags byte.</param>
    public Jbig2RefinementRegionFlags(byte flagsByte) => _flags = flagsByte;

    /// <summary>
    /// Refinement template identifier (GRTEMPLATE): 0 or 1.
    /// Determines the context model and the number of adaptive template pixels.
    /// </summary>
    public int TemplateId => _flags & 0x01;

    /// <summary>
    /// Whether typical prediction for generic refinement regions (TPGRON) is enabled.
    /// </summary>
    public bool TypicalPrediction => (_flags & 0x02) != 0;

    /// <summary>
    /// Number of adaptive template pixel pairs: 2 for template 0, 0 for template 1.
    /// </summary>
    public int AtPixelCount => (TemplateId == 0) ? 2 : 0;
}
