namespace PdfPixel.Jbig2;

/// <summary>
/// JBIG2 work observer, notifies on long-running decoding operation.
/// </summary>
public interface IJbig2ExecutionObserver
{
    /// <summary>
    /// Notifies that long-running operation chunk is done.
    /// </summary>
    void Notify();
}
