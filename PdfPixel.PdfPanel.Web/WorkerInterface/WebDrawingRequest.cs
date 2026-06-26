using PdfPixel.Models;
using PdfPixel.PdfPanel.Requests;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// JSON-serializable representation of <see cref="PagesDrawingRequest"/>.
/// </summary>
public class WebDrawingRequest
{
    public float Scale { get; set; }

    public float OffsetX { get; set; }

    public float OffsetY { get; set; }

    public float CanvasWidth { get; set; }

    public float CanvasHeight { get; set; }

    public List<WebVisiblePageInfo> VisiblePages { get; set; }

    public float? ScaleFactor { get; set; }

    public bool Antialias { get; set; } = true;

    public int DefaultFunctionSamples { get; set; } = 64;

    public int MaxTessellationVertices { get; set; } = 32;

    public int ImageTileSize { get; set; } = 1024;

    public PagesDrawingRequest ToPagesDrawingRequest()
    {
        return new PagesDrawingRequest
        {
            Scale = Scale,
            Offset = new SkiaSharp.SKPoint(OffsetX, OffsetY),
            CanvasSize = new SkiaSharp.SKSize(CanvasWidth, CanvasHeight),
            VisiblePages = VisiblePages
                .Select(page => page.ToVisiblePageInfo())
                .ToArray(),
            CommandExecutionParameters = new PdfCommandExecutionParameters
            {
                ScaleFactor = ScaleFactor,
                Antialias = Antialias,
                DefaultFunctionSamples = DefaultFunctionSamples,
                MaxTessellationVertices = MaxTessellationVertices,
                ImageTileSize = ImageTileSize
            }
        };
    }

    public static WebDrawingRequest FromPagesDrawingRequest(PagesDrawingRequest request)
    {
        return new WebDrawingRequest
        {
            Scale = request.Scale,
            OffsetX = request.Offset.X,
            OffsetY = request.Offset.Y,
            CanvasWidth = request.CanvasSize.Width,
            CanvasHeight = request.CanvasSize.Height,
            VisiblePages = request.VisiblePages
                .Select(WebVisiblePageInfo.FromVisiblePageInfo)
                .ToList(),
            ScaleFactor = request.CommandExecutionParameters.ScaleFactor,
            Antialias = request.CommandExecutionParameters.Antialias,
            DefaultFunctionSamples = request.CommandExecutionParameters.DefaultFunctionSamples,
            MaxTessellationVertices = request.CommandExecutionParameters.MaxTessellationVertices,
            ImageTileSize = request.CommandExecutionParameters.ImageTileSize
        };
    }
}
