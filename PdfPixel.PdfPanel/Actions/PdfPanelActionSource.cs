namespace PdfPixel.PdfPanel.Actions;

/// <summary>
/// What triggered an action performed by <see cref="PdfPanelActions"/>.
/// </summary>
public enum PdfPanelActionSource
{
    /// <summary>
    /// The document's open action (catalog /OpenAction).
    /// </summary>
    DocumentOpen,

    /// <summary>
    /// The page became the current page (page /AA /O).
    /// </summary>
    PageOpen,

    /// <summary>
    /// The page stopped being the current page (page /AA /C).
    /// </summary>
    PageClose,

    /// <summary>
    /// A link annotation activated by the pointer.
    /// </summary>
    Annotation
}
