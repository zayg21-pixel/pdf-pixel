namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// JPX work observer, notifies on long-running decoding operation.
/// </summary>
public interface IJpxExectionObserver
{
    /// <summary>
    /// Notifies that long-running operation chunk is done.
    /// </summary>
    void Notify();
}
