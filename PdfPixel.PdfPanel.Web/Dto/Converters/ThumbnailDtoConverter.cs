using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Web.Dto.Model;
using System.Collections.Generic;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web.Dto.Converters;

/// <summary>
/// Converts the thumbnails of a panel to DTOs.
/// </summary>
internal static class ThumbnailDtoConverter
{
    /// <summary>
    /// Builds the JSON thumbnail layout <c>[{ pageNumber, width, height, rotation }]</c> of the pages of <paramref name="context"/>
    /// that have a thumbnail, in page order.
    /// </summary>
    public static string CreateLayout(PdfPanelContext context)
    {
        IReadOnlyList<PdfPanelPage> pages = context.Document.Pages;
        List<ThumbnailLayoutDto> layout = new(pages.Count);

        foreach (PdfPanelPage page in pages)
        {
            PdfSize? size = context.Thumbnails.GetSize(page.PageNumber);

            if (size == null)
            {
                continue;
            }

            ThumbnailLayoutDto pageLayout = new()
            {
                PageNumber = page.PageNumber,
                Width = size.Value.Width,
                Height = size.Value.Height,
                Rotation = page.Rotation
            };

            layout.Add(pageLayout);
        }

        return JsonSerializer.Serialize(layout, DtoJsonContext.Default.ListThumbnailLayoutDto);
    }
}
