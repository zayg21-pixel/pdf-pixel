using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Drawing request for user interface interactions such as text selection.
/// </summary>
public class UserInterfaceDrawingRequest : DrawingRequest
{
    /// <summary>
    /// Current pointer position in viewport coordinates, or <see langword="null"/> if pointer is not over the panel.
    /// </summary>
    public SKPoint? PointerPosition { get; set; }

    /// <summary>
    /// Current pointer button state.
    /// </summary>
    public PdfPanelButtonState PointerState { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is UserInterfaceDrawingRequest other)
        {
            return base.Equals(obj)
                && PointerPosition == other.PointerPosition
                && PointerState == other.PointerState;
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(base.GetHashCode());
        hash.Add(PointerPosition);
        hash.Add(PointerState);
        return hash.ToHashCode();
    }
}
