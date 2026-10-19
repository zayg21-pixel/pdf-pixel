namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Page destination <c>{ pageNumber, fitType, left, top, right, bottom, zoom }</c> with a 1-based page number.
/// </summary>
internal sealed class PageDestinationDto
{
    public int PageNumber { get; set; }

    public required string FitType { get; set; }

    public float? Left { get; set; }

    public float? Top { get; set; }

    public float? Right { get; set; }

    public float? Bottom { get; set; }

    public float? Zoom { get; set; }
}
