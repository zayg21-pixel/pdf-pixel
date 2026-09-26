using PdfPixel.PdfPanel.Layout;
using System;

namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Settings of a PDF panel, grouped by concern.
/// </summary>
public sealed class PdfPanelSettings
{
    private PdfPanelAppearanceSettings _appearance = new();
    private PdfPanelZoomSettings _zoom = new();
    private IPdfPanelLayout _layout = new PdfPanelVerticalLayout();
    private PdfPanelInteractionSettings _interaction = new();
    private PdfPanelSearchSettings _search = new();
    private PdfPanelRenderingSettings _rendering = new();

    /// <summary>
    /// Colors and decorations of the panel.
    /// </summary>
    public PdfPanelAppearanceSettings Appearance
    {
        get => _appearance;
        set => _appearance = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Scale limits and zoom step.
    /// </summary>
    public PdfPanelZoomSettings Zoom
    {
        get => _zoom;
        set => _zoom = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Layout that positions the pages within the viewport.
    /// </summary>
    public IPdfPanelLayout Layout
    {
        get => _layout;
        set => _layout = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Pointer interaction thresholds.
    /// </summary>
    public PdfPanelInteractionSettings Interaction
    {
        get => _interaction;
        set => _interaction = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Options that control how the text search query is matched.
    /// </summary>
    public PdfPanelSearchSettings Search
    {
        get => _search;
        set => _search = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Rendering quality and timing.
    /// </summary>
    public PdfPanelRenderingSettings Rendering
    {
        get => _rendering;
        set => _rendering = value ?? throw new ArgumentNullException(nameof(value));
    }
}
