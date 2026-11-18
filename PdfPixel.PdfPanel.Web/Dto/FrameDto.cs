using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Frame the view draws <c>{ panelWidth, panelHeight, hostToPanel, pageCornerRadius, pages }</c>, the matrix as
/// <c>[a, b, c, d, e, f]</c>.
/// </summary>
internal sealed class FrameDto
{
    public float PanelWidth { get; set; }

    public float PanelHeight { get; set; }

    public required float[] HostToPanel { get; set; }

    public float PageCornerRadius { get; set; }

    public required IReadOnlyList<FramePageDto> Pages { get; set; }
}
