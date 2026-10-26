namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Text range <c>{ pageNumber, startIndex, length }</c>.
/// </summary>
internal sealed class TextRangeDto
{
    public int PageNumber { get; set; }

    public int StartIndex { get; set; }

    public int Length { get; set; }
}
