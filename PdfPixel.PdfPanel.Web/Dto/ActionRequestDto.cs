namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Requested action, <c>{ source, action }</c>.
/// </summary>
internal sealed class ActionRequestDto
{
    public required string Source { get; set; }

    public required ActionDto Action { get; set; }
}
