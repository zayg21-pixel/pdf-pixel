namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Static info of a page, <c>{ label, width, height, rotation }</c>.
/// </summary>
internal sealed class PageDto
{
    public required string Label { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public int Rotation { get; set; }
}
