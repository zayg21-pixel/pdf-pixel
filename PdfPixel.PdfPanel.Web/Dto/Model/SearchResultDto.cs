namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Search result, <c>{ pageNumber, startIndex, length, bounds }</c>.
/// </summary>
internal sealed class SearchResultDto
{
    public int PageNumber { get; set; }

    public int StartIndex { get; set; }

    public int Length { get; set; }

    public required RectangleDto Bounds { get; set; }
}
