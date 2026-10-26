using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Annotation message, <c>{ title, contents, creationDate, replies }</c>.
/// </summary>
internal sealed class AnnotationMessageDto
{
    public string? Title { get; set; }

    public required string Contents { get; set; }

    public string? CreationDate { get; set; }

    public required IReadOnlyList<AnnotationMessageDto> Replies { get; set; }
}
