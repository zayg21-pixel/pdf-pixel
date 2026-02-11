using SkiaSharp;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Request to draw pages on rendering panel.
/// </summary>
public class PagesDrawingRequest : DrawingRequest
{
    public PdfPanelPageCollection Pages { get; set; }

    public VisiblePageInfo[] VisiblePages { get; set; }

    public SKColor BackgroundColor { get; set; }

    public int MaxThumbnailSize { get; set; }

    public override bool Equals(object obj)
    {
        if (obj is PagesDrawingRequest parameters)
        {
            return base.Equals(obj) &&
                BackgroundColor == parameters.BackgroundColor &&
                MaxThumbnailSize == parameters.MaxThumbnailSize &&
                Pages == parameters.Pages &&
                VisiblePages.SequenceEqual(parameters.VisiblePages);
        }
        else
        {
            return false;
        }
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}