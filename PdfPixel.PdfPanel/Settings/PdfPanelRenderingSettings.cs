using System;

namespace PdfPixel.PdfPanel.Settings;

/// <summary>
/// Rendering quality and timing of a PDF panel.
/// </summary>
public sealed class PdfPanelRenderingSettings : IEquatable<PdfPanelRenderingSettings>
{
    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; } = true;

    /// <summary>
    /// If true - rects and image tiles are snapped to whole device pixels.
    /// </summary>
    public bool SnapToDevicePixels { get; set; } = true;

    /// <summary>
    /// Edge length of a single content tile in device pixels.
    /// </summary>
    public int TileSize { get; set; } = 512;

    /// <summary>
    /// Frames per second the renderer drives its animations at.
    /// </summary>
    public int AnimationFps { get; set; } = 60;

    /// <summary>
    /// Time a scrolled request waits before page content decoding starts.
    /// </summary>
    public TimeSpan ScrollContentUpdateDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Time a zoomed request waits before page content decoding starts.
    /// </summary>
    public TimeSpan ZoomContentUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Returns a copy of these settings.
    /// </summary>
    public PdfPanelRenderingSettings Clone() => (PdfPanelRenderingSettings)MemberwiseClone();

    /// <inheritdoc />
    public bool Equals(PdfPanelRenderingSettings? other)
    {
        if (other == null)
        {
            return false;
        }

        return Antialias == other.Antialias
            && SnapToDevicePixels == other.SnapToDevicePixels
            && TileSize == other.TileSize
            && AnimationFps == other.AnimationFps
            && ScrollContentUpdateDelay == other.ScrollContentUpdateDelay
            && ZoomContentUpdateDelay == other.ZoomContentUpdateDelay;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfPanelRenderingSettings);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Antialias, SnapToDevicePixels, TileSize, AnimationFps, ScrollContentUpdateDelay, ZoomContentUpdateDelay);
}
