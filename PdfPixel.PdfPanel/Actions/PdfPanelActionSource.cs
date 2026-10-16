namespace PdfPixel.PdfPanel.Actions;

/// <summary>
/// What triggered an action performed by <see cref="PdfPanelActions"/>.
/// </summary>
public enum PdfPanelActionSource
{
    /// <summary>
    /// The document was opened.
    /// </summary>
    DocumentOpen,

    /// <summary>
    /// The page became the current page.
    /// </summary>
    PageOpen,

    /// <summary>
    /// The page stopped being the current page.
    /// </summary>
    PageClose,

    /// <summary>
    /// A link annotation activated by the pointer.
    /// </summary>
    LinkAnnotationClick,

    /// <summary>
    /// An outline item (bookmark) activated by the user.
    /// </summary>
    Outline
}
