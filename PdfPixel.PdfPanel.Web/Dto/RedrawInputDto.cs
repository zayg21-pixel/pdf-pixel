namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// View state JS passes to a redraw: the panel size, viewport, pending zoom and page requests, search and pointer.
/// </summary>
internal sealed class RedrawInputDto
{
    public int PanelWidth { get; set; }

    public int PanelHeight { get; set; }

    public float VerticalOffset { get; set; }

    public float HorizontalOffset { get; set; }

    public float Scale { get; set; }

    public string? AutoScaleMode { get; set; }

    public ZoomRequestDto? ZoomRequest { get; set; }

    public int ForcePageSet { get; set; }

    public string? SearchQuery { get; set; }

    public TextRangeDto? CurrentSearchResult { get; set; }

    public bool PointerInside { get; set; }

    public float PointerX { get; set; }

    public float PointerY { get; set; }

    public bool PointerPressed { get; set; }
}
