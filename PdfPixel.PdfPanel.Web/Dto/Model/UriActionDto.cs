namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// URI action, <c>{ type: 'uri', uri }</c>.
/// </summary>
internal sealed class UriActionDto : ActionDto
{
    public string? Uri { get; set; }
}
