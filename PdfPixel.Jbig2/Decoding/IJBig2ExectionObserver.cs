namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// JBIG2 work observer, notifies on long-running decoding operation.
/// </summary>
public interface IJBig2ExectionObserver
{
    /// <summary>
    /// Notifies that long-running operation chunk is done.
    /// </summary>
    void Notify();
}
