namespace PdfReader.Imaging.Sampling;

/// <summary>
/// Defines a contract for upsample a row of image data into required format.
/// </summary>
internal interface IRowUpsampler
{
    /// <summary>
    /// Upsamples a row of image data from the source buffer into the destination buffer.
    /// </summary>
    /// <param name="source">Reference to the first byte of the source row data.</param>
    /// <param name="destination">Reference to the first byte of the destination buffer.</param>
    void Upsample(ref byte source, ref byte destination);
}
