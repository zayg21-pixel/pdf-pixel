namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Describes the type of navigation an annotation performs when activated.
/// </summary>
public enum PdfAnnotationNavigationType
{
    /// <summary>
    /// The annotation has no navigation target.
    /// </summary>
    None,

    /// <summary>
    /// Opens an external URI.
    /// </summary>
    Uri,

    /// <summary>
    /// Navigates to a destination within the current document.
    /// </summary>
    GoToDestination,

    /// <summary>
    /// Navigates to a destination in a remote (external) document.
    /// </summary>
    GoToRemote
}
