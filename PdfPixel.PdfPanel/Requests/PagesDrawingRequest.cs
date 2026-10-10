using PdfPixel.Color;
using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Rendering request that includes page layout and visual parameters.
/// </summary>
internal sealed class PagesDrawingRequest : DrawingRequest
{
    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; }

    /// <summary>
    /// If true - rects and image tiles are snapped to whole device pixels.
    /// </summary>
    public bool SnapToDevicePixels { get; set; }

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfColor BackgroundColor { get; set; }

    /// <summary>
    /// Corner radius for page rendering in unscaled page space.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Draws an animated placeholder over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation { get; set; }

    /// <summary>
    /// ON/OFF state of the optional content groups content is rendered with, or null for the document's default configuration.
    /// </summary>
    public IReadOnlyDictionary<PdfReference, bool>? OptionalContentStates { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is PagesDrawingRequest other)
        {
            return base.Equals(obj)
                && Antialias == other.Antialias
                && SnapToDevicePixels == other.SnapToDevicePixels
                && BackgroundColor.Equals(other.BackgroundColor)
                && PageCornerRadius == other.PageCornerRadius
                && ShowPageLoadingAnimation == other.ShowPageLoadingAnimation
                && AreOptionalContentStatesEqual(OptionalContentStates, other.OptionalContentStates);
        }

        return false;
    }

    /// <summary>
    /// Returns whether two optional content state snapshots hold the same state for every group.
    /// </summary>
    public static bool AreOptionalContentStatesEqual(IReadOnlyDictionary<PdfReference, bool>? left, IReadOnlyDictionary<PdfReference, bool>? right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (KeyValuePair<PdfReference, bool> entry in left)
        {
            if (!right.TryGetValue(entry.Key, out bool isOn) || isOn != entry.Value)
            {
                return false;
            }
        }

        return true;
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
        hash.Add(Antialias);
        hash.Add(SnapToDevicePixels);
        hash.Add(BackgroundColor);
        hash.Add(PageCornerRadius);
        hash.Add(ShowPageLoadingAnimation);
        return hash.ToHashCode();
    }
}
