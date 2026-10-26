namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Action the panel does not describe, <c>{ type: 'unsupported', actionType }</c> with its PDF name.
/// </summary>
internal sealed class UnsupportedActionDto : ActionDto
{
    public required string ActionType { get; set; }
}
