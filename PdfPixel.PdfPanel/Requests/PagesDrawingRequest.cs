using PdfPixel.PdfPanel.Settings;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Rendering request that includes page layout and visual parameters.
/// </summary>
internal sealed class PagesDrawingRequest : DrawingRequest
{
    /// <summary>
    /// Rendering quality the page content is decoded with.
    /// </summary>
    public PdfPanelRenderingSettings Rendering { get; set; } = new();

    /// <summary>
    /// Appearance the pages are drawn with.
    /// </summary>
    public PdfPanelAppearanceSettings Appearance { get; set; } = new();

    /// <summary>
    /// Text options the text highlights are drawn with.
    /// </summary>
    public PdfPanelTextSettings Text { get; set; } = new();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is PagesDrawingRequest other)
        {
            return base.Equals(obj)
                && Rendering.Equals(other.Rendering)
                && Appearance.Equals(other.Appearance)
                && Text.Equals(other.Text);
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
        hash.Add(PanelSize);
        hash.Add(RenderTarget);
        hash.Add(ActiveAnnotation);
        hash.Add(ActiveAnnotationState);
        hash.Add(Rendering);
        hash.Add(Appearance);
        hash.Add(Text);
        return hash.ToHashCode();
    }
}
