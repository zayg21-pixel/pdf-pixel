namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Named action, <c>{ type: 'named', name }</c>.
/// </summary>
internal sealed class NamedActionDto : ActionDto
{
    public string? Name { get; set; }
}
