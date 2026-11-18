namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Tile of a frame page: the id of its canvas and where it is drawn, in unrotated page content coordinates.
/// </summary>
internal sealed class FrameTileDto
{
    public int Id { get; set; }

    public required RectangleDto Destination { get; set; }
}
