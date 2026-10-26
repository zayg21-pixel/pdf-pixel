namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Remote go-to action, <c>{ type: 'goToRemote', file, newWindow, destination }</c>; the destination is a
/// <see cref="PageDestinationDto"/>, a <see cref="NamedDestinationDto"/> or null.
/// </summary>
internal sealed class GoToRemoteActionDto : ActionDto
{
    public FileDto? File { get; set; }

    public bool? NewWindow { get; set; }

    public object? Destination { get; set; }
}
