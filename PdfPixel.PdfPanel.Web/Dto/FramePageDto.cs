using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Visible page of a frame: its size and rotation, the unrotated page content to panel matrix and its inverse as
/// <c>[a, b, c, d, e, f]</c>, the page bounds snapped to whole panel pixels, the canvas id of its placeholder thumbnail,
/// and its tiles in drawing order.
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

    public required float[] ContentToPanel { get; set; }

    public required float[] PanelToContent { get; set; }

    public required RectangleDto Bounds { get; set; }

    public int? Thumbnail { get; set; }

    public required IReadOnlyList<FrameTileDto> Tiles { get; set; }
}
