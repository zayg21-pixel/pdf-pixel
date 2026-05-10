using PdfPixel.Imaging.Jpx.Model;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Interface for inverse discrete wavelet transform implementations.
/// Each implementation handles a specific wavelet kernel (e.g., 5-3 reversible, 9-7 irreversible).
/// </summary>
internal interface IJpxInverseDwt
{
    /// <summary>
    /// Reconstructs a component from its subband decomposition, optionally stopping
    /// at a coarser resolution level for reduced-resolution decoding.
    /// </summary>
    /// <param name="subbands">Subband coefficient data for a single component.</param>
    /// <param name="destination">Pre-allocated buffer to receive reconstructed samples.</param>
    /// <param name="stopAtLevel">
    /// The finest decomposition level to skip. 0 means full reconstruction;
    /// higher values produce smaller output (each increment halves both dimensions).
    /// </param>
    void Transform(JpxSubbandData subbands, int[] destination, int stopAtLevel = 0);
}
