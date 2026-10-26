using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using System.Collections.Generic;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web.Dto.Converters;

/// <summary>
/// Converts presented frames to DTOs.
/// </summary>
internal static class FrameDtoConverter
{
    /// <summary>
    /// Builds the JSON frame <c>{ panelWidth, panelHeight, hostToPanel, pages }</c> of <paramref name="frame"/>.
    /// </summary>
    public static string CreateFrame(PdfPanelFrame frame)
    {
        List<FramePageDto> pages = new(frame.Pages.Count);

        foreach (PdfPanelFramePage page in frame.Pages)
        {
            PdfSize rotatedSize = page.RotatedSize;

            FramePageDto pageDto = new()
            {
                PageNumber = page.PageNumber,
                Label = page.Label,
                Width = page.Size.Width,
                Height = page.Size.Height,
                Rotation = page.Rotation,
                RotatedWidth = rotatedSize.Width,
                RotatedHeight = rotatedSize.Height,
                PanelToContent = ToDomMatrix(page.GetPanelToPage(0))
            };

            pages.Add(pageDto);
        }

        FrameDto frameDto = new()
        {
            PanelWidth = frame.PanelSize.Width,
            PanelHeight = frame.PanelSize.Height,
            HostToPanel = ToDomMatrix(frame.HostToPanel),
            Pages = pages
        };

        return JsonSerializer.Serialize(frameDto, DtoJsonContext.Default.FrameDto);
    }

    /// <summary>
    /// Returns the <c>DOMMatrix</c> component order <c>[a, b, c, d, e, f]</c> of <paramref name="matrix"/>.
    /// </summary>
    private static float[] ToDomMatrix(in PdfMatrix matrix)
        => [matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TranslateX, matrix.TranslateY];
}
