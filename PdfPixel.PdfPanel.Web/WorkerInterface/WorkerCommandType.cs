namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// Command type to send to worker.
/// </summary>
public enum WorkerCommandType
{
    Initialize,
    SetFont,
    SetDocument,
    UpdateContent,
    PageContentReady
}
