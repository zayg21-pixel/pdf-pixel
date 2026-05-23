namespace PdfPixel.Jpx.Model;

/// <summary>
/// Represents quantization parameters from QCD marker segment.
/// </summary>
public sealed class JpxQuantization
{
    /// <summary>
    /// Gets or sets the quantization style (Sqcd parameter).
    /// Bits 0-4: Quantization type (0=no quantization, 1=scalar derived, 2=scalar expounded)
    /// Bits 5-7: Number of guard bits
    /// </summary>
    public byte Style { get; set; }

    /// <summary>
    /// Gets or sets the quantization step sizes (SPqcd parameters).
    /// For scalar quantization, contains the step sizes for each subband.
    /// </summary>
    public ushort[] StepSizes { get; set; }

    /// <summary>
    /// Gets the number of guard bits (bits 5-7 of Sqcd per ITU-T T.800 Table A.28).
    /// </summary>
    public int GuardBits => (Style >> 5) & 0x07;

    /// <summary>
    /// Gets the quantization type (bits 0-4 of Sqcd per ITU-T T.800 Table A.28).
    /// 0 = No quantization, 1 = Scalar derived, 2 = Scalar expounded
    /// </summary>
    public int QuantizationType => Style & 0x1F;

    /// <summary>
    /// Gets a value indicating whether quantization is used.
    /// </summary>
    public bool HasQuantization => QuantizationType != 0;
}