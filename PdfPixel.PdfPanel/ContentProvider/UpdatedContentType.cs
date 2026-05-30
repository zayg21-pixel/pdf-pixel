namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Indicates which layer of a page was updated after background decoding.
/// </summary>
public enum UpdatedContentType
{
    /// <summary>
    /// Main page content picture was updated.
    /// </summary>
    Content,

    /// <summary>
    /// Annotation layer picture was updated.
    /// </summary>
    Annotations
}
