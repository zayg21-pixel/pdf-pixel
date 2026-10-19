namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Static info of a page, <c>{ label, cropBox, rotation }</c>.
/// </summary>
internal sealed class PageDto
{
    public required string Label { get; set; }

    public required RectangleDto CropBox { get; set; }

    public int Rotation { get; set; }
}
