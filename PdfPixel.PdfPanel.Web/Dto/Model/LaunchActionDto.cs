namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Launch action, <c>{ type: 'launch', file, newWindow }</c>.
/// </summary>
internal sealed class LaunchActionDto : ActionDto
{
    public FileDto? File { get; set; }

    public bool? NewWindow { get; set; }
}
