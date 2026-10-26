namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Thumbnail layout of a page, <c>{ pageNumber, width, height, rotation }</c>.
/// </summary>
internal sealed class ThumbnailLayoutDto
{
    public int PageNumber { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public int Rotation { get; set; }
}
