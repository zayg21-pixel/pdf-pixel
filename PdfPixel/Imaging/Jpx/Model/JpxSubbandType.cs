namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Identifies which subband a detail coefficient belongs to within a DWT decomposition level.
/// </summary>
internal enum JpxSubbandType
{
    /// <summary>Horizontal high-pass, vertical low-pass.</summary>
    HL = 0,

    /// <summary>Horizontal low-pass, vertical high-pass.</summary>
    LH = 1,

    /// <summary>Horizontal high-pass, vertical high-pass.</summary>
    HH = 2
}
