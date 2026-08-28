namespace PdfPixel.Imaging.Model;

/// <summary>
/// Describes the pixel layout of a <see cref="PdfDecodedImage"/>.
/// </summary>
public enum PdfImageColorFormat
{
    /// <summary>
    /// One byte per pixel, a single gray channel.
    /// </summary>
    Gray,

    /// <summary>
    /// Four bytes per pixel, in R, G, B, A order.
    /// </summary>
    Rgba,

    /// <summary>
    /// One byte per pixel, a single alpha channel.
    /// </summary>
    Alpha
}
