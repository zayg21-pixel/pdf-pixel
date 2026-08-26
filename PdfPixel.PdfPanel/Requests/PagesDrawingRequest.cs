using PdfPixel.Models;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Rendering request that includes page layout and visual parameters.
/// </summary>
public class PagesDrawingRequest : DrawingRequest
{
    /// <summary>
    /// Scale the page content is recorded at, so that every device-space decision taken while
    /// recording - image decode resolution, pen widths, pixel snapping - is taken for the scale
    /// the recording is displayed at.
    /// </summary>
    public float ScaleFactor { get; set; }

    /// <summary>
    /// PDF command execution quality settings.
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
                && ScaleFactor == other.ScaleFactor
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
        hash.Add(ScaleFactor);
        hash.Add(CommandExecutionParameters);
        return hash.ToHashCode();
    }
}
