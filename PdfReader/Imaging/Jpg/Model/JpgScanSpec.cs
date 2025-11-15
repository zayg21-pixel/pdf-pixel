using System.Collections.Generic;

namespace PdfReader.Imaging.Jpg.Model;

/// <summary>
/// Describes a single JPEG scan header (SOS) without the entropy payload.
/// </summary>
internal sealed class JpgScanSpec
{
    /// <summary>
    /// Gets the list of scan component specifications for this scan.
    /// </summary>
    public List<JpgScanComponentSpec> Components { get; } = new List<JpgScanComponentSpec>();

    /// <summary>
    /// Gets or sets the spectral selection start index (Ss) for progressive JPEG scans.
    /// </summary>
    public int SpectralStart { get; set; }

    /// <summary>
    /// Gets or sets the spectral selection end index (Se) for progressive JPEG scans.
    /// </summary>
    public int SpectralEnd { get; set; }

    /// <summary>
    /// Gets or sets the high bit position for successive approximation (Ah).
    /// </summary>
    public int SuccessiveApproxHigh { get; set; }

    /// <summary>
    /// Gets or sets the low bit position for successive approximation (Al).
    /// </summary>
    public int SuccessiveApproxLow { get; set; }
}
