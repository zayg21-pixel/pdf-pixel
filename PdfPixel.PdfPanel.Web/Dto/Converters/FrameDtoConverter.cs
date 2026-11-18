using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Thumbnails;
using PdfPixel.PdfPanel.Web.Rendering;
using PdfPixel.Snap.Rasterization;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json;

namespace PdfPixel.PdfPanel.Web.Dto.Converters;

/// <summary>
/// Converts the frame of a panel to DTOs.
/// </summary>
[SupportedOSPlatform("browser")]
internal static class FrameDtoConverter
{
    /// <summary>
    /// Builds the JSON frame <c>{ panelWidth, panelHeight, hostToPanel, pageCornerRadius, pages }</c> of the visible
    /// pages of <paramref name="context"/>, with their tiles and, unless <paramref name="placeholderMode"/> is
    /// <see cref="PdfPanelThumbnailPlaceholderMode.None"/>, their placeholder thumbnails.
    /// </summary>
    public static string CreateFrame(PdfPanelContext context, PdfPanelThumbnailPlaceholderMode placeholderMode)
    {
        List<FramePageDto> pages = new(context.VisiblePages.Count);

        foreach (PdfPanelFramePage page in context.VisiblePages)
        {
            PdfSize rotatedSize = page.RotatedSize;
            PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, page.Size.Width, page.Size.Height);
            PdfRectangle snappedBounds = PdfSnapGrid.SnapToWholeDevicePixels(pageBounds, page.ContentToPanel);
            int? thumbnail = null;

            if (placeholderMode != PdfPanelThumbnailPlaceholderMode.None && context.Thumbnails.Get(page.PageNumber) is WebThumbnailResource resource)
            {
                thumbnail = resource.Id;
            }

            FramePageDto pageDto = new()
            {
                PageNumber = page.PageNumber,
                Label = page.Label,
                Width = page.Size.Width,
                Height = page.Size.Height,
                Rotation = page.Rotation,
                RotatedWidth = rotatedSize.Width,
                RotatedHeight = rotatedSize.Height,
                ContentToPanel = ToDomMatrix(page.ContentToPanel),
                PanelToContent = ToDomMatrix(page.GetPanelToPage(0)),
                Bounds = ToRectangle(snappedBounds),
                Thumbnail = thumbnail,
                Tiles = CreateTiles(context.Tiles.GetTiles(page))
            };

            pages.Add(pageDto);
        }

        FrameDto frameDto = new()
        {
            PanelWidth = context.PanelWidth,
            PanelHeight = context.PanelHeight,
            HostToPanel = ToDomMatrix(PdfMatrix.Identity),
            PageCornerRadius = context.Renderer.PageCornerRadius,
            Pages = pages
        };

        return JsonSerializer.Serialize(frameDto, DtoJsonContext.Default.FrameDto);
    }

    private static List<FrameTileDto> CreateTiles(IReadOnlyList<PdfPanelTile> tiles)
    {
        List<FrameTileDto> tileDtos = new(tiles.Count);

        foreach (PdfPanelTile tile in tiles)
        {
            if (tile.Resource is WebTileResource resource)
            {
                FrameTileDto tileDto = new()
                {
                    Id = resource.Id,
                    Destination = ToRectangle(tile.Destination)
                };

                tileDtos.Add(tileDto);
            }
        }

        return tileDtos;
    }

    private static RectangleDto ToRectangle(in PdfRectangle rectangle)
    {
        return new()
        {
            Left = rectangle.Left,
            Top = rectangle.Top,
            Right = rectangle.Right,
            Bottom = rectangle.Bottom
        };
    }

    /// <summary>
    /// Returns the <c>DOMMatrix</c> component order <c>[a, b, c, d, e, f]</c> of <paramref name="matrix"/>.
    /// </summary>
    private static float[] ToDomMatrix(in PdfMatrix matrix)
        => [matrix.ScaleX, matrix.SkewY, matrix.SkewX, matrix.ScaleY, matrix.TranslateX, matrix.TranslateY];
}
