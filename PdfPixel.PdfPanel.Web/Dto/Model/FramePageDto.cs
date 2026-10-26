namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Visible page of a presented frame, the panel to unrotated page content matrix as <c>[a, b, c, d, e, f]</c>.
/// </summary>
internal sealed class FramePageDto
{
    public int PageNumber { get; set; }

    public required string Label { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public int Rotation { get; set; }

    public float RotatedWidth { get; set; }

    public float RotatedHeight { get; set; }

    public required float[] PanelToContent { get; set; }
}
