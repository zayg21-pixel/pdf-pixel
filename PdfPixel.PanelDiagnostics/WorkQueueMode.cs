namespace PdfPixel.PanelDiagnostics;

/// <summary>
/// The work queue the panel decodes pages with.
/// </summary>
internal enum WorkQueueMode
{
    /// <summary>
    /// Every page starts decoding on the panel thread as soon as it is requested, yielding the thread between commands.
    /// </summary>
    Immediate,

    /// <summary>
    /// Pages decode one at a time, in order.
    /// </summary>
    Sequential
}
