namespace PdfPixel.PdfPanel.Web.Dto.Model;

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
