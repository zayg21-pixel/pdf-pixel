namespace PdfPixel.Jpx;

/// <summary>
/// JPX work observer, notifies on long-running decoding operation.
/// </summary>
public interface IJpxExecutionObserver
{
    /// <summary>
    /// Notifies that long-running operation chunk is done.
    /// </summary>
    void Notify();
}
