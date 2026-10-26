using System.Collections.Generic;

namespace PdfPixel.Jpeg;

/// <summary>
/// Describes a single JPEG scan header (SOS) without the entropy payload.
/// </summary>
public sealed class JpegScanSpec
{
    /// <summary>
    /// Gets the list of scan component specifications for this scan.
    /// </summary>
    public List<JpegScanComponentSpec> Components { get; } = [];

    /// <summary>
    /// Gets or sets the spectral selection start index (Ss) for progressive JPEG scans.
    /// </summary>
    public int SpectralStart { get; internal set; }

    /// <summary>
    /// Gets or sets the spectral selection end index (Se) for progressive JPEG scans.
    /// </summary>
    public int SpectralEnd { get; internal set; }

    /// <summary>
    /// Gets or sets the high bit position for successive approximation (Ah).
    /// </summary>
    public int SuccessiveApproxHigh { get; internal set; }

    /// <summary>
    /// Gets or sets the low bit position for successive approximation (Al).
    /// </summary>
    public int SuccessiveApproxLow { get; internal set; }
}
