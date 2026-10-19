using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Annotation popup, <c>{ isInteractive, messages }</c>.
/// </summary>
internal sealed class AnnotationPopupDto
{
    public bool IsInteractive { get; set; }

    public required IReadOnlyList<AnnotationMessageDto> Messages { get; set; }
}
