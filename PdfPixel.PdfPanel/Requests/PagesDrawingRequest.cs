using PdfPixel.Models;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Rendering request that includes page layout and visual parameters.
/// </summary>
public class PagesDrawingRequest : DrawingRequest
{
    /// <summary>
    /// Color drawn behind all pages.
    /// </summary>
    public SKColor BackgroundColor { get; set; }

    /// <summary>
    /// PDF command execution parameters such as scale factor and quality settings.
    /// </summary>
    public PdfCommandExecutionParameters CommandExecutionParameters { get; set; } = new();

    /// <summary>
    /// Parameters for PDF page rendering.
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; set; } = new();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is PagesDrawingRequest other)
        {
            return base.Equals(obj)
                && BackgroundColor == other.BackgroundColor
                && CommandExecutionParameters == other.CommandExecutionParameters;
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(base.GetHashCode());
        hash.Add(Scale);
        hash.Add(Offset);
        hash.Add(CanvasSize);
        hash.Add(RenderTarget);
        hash.Add(ActiveAnnotation);
        hash.Add(ActiveAnnotationState);
        hash.Add(BackgroundColor);
        hash.Add(CommandExecutionParameters);
        return hash.ToHashCode();
    }
}
