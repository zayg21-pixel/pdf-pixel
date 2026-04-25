using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public class UpdateContentRequest
{
    public int PageNumber { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; set; }

    public PdfRenderingParameters RenderingParameters { get; set; }

    public Action<int, ContentLocker<SKPicture>> OnPageUpdated { get; set; }
}
