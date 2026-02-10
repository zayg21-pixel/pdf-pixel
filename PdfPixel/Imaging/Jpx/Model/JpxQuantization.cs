namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Represents quantization parameters from QCD marker segment.
/// </summary>
internal sealed class JpxQuantization
{
    /// <summary>
    /// Gets or sets the quantization style (Sqcd parameter).
    /// Bits 0-4: Number of guard bits
    /// Bits 5-7: Quantization type (0=no quantization, 1=scalar derived, 2=scalar expounded)
    /// </summary>
    public byte Style { get; set; }

    /// <summary>
    /// Gets or sets the quantization step sizes (SPqcd parameters).
    /// For scalar quantization, contains the step sizes for each subband.
    /// </summary>
    public ushort[] StepSizes { get; set; }

    /// <summary>
    /// Gets the number of guard bits.
    /// </summary>
    public int GuardBits => Style & 0x1F;

    /// <summary>
    /// Gets the quantization type.
    /// 0 = No quantization, 1 = Scalar derived, 2 = Scalar expounded
    /// </summary>
    public int QuantizationType => (Style >> 5) & 0x07;

    /// <summary>
    /// Gets a value indicating whether quantization is used.
    /// </summary>
    public bool HasQuantization => QuantizationType != 0;
}