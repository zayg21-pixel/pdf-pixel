using PdfPixel.Annotations.Models;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Carries serialization-friendly navigation data extracted from an annotation.
/// Constructed once when annotation popups are built and passed into <see cref="PdfAnnotationPopup"/>.
/// </summary>
public class PdfAnnotationNavigation
{
    /// <summary>
    /// Type of navigation performed when the annotation is activated.
    /// </summary>
    public PdfAnnotationNavigationType NavigationType { get; set; }

    /// <summary>
    /// Cursor to show when the pointer is over the annotation.
    /// </summary>
    public PdfAnnotationCursorType CursorType { get; set; }

    /// <summary>
    /// Target URI string. Non-null when <see cref="NavigationType"/> is <see cref="PdfAnnotationNavigationType.Uri"/>.
    /// </summary>
    public string? Uri { get; set; }

    /// <summary>
    /// Destination within the document.
    /// Non-null when <see cref="NavigationType"/> is <see cref="PdfAnnotationNavigationType.GoToDestination"/>.
    /// </summary>
    public PdfAnnotationDestination? Destination { get; set; }
}
