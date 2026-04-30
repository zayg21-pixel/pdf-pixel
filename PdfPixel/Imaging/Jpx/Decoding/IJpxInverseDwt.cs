using PdfPixel.Imaging.Jpx.Model;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Interface for inverse discrete wavelet transform implementations.
/// Each implementation handles a specific wavelet kernel (e.g., 5-3 reversible, 9-7 irreversible).
/// </summary>
internal interface IJpxInverseDwt
{
    /// <summary>
    /// Reconstructs a full-resolution component from its subband decomposition,
    /// writing results directly into the provided destination buffer.
    /// </summary>
    /// <param name="subbands">Subband coefficient data for a single component.</param>
    /// <param name="destination">Pre-allocated buffer to receive reconstructed samples.</param>
    void Transform(JpxSubbandData subbands, int[] destination);
}
