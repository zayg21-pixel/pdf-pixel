using PdfPixel.PdfPanel.Input;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Drawing request for user interface interactions such as text selection.
/// </summary>
public class UserInterfaceDrawingRequest : DrawingRequest
{
    /// <summary>
    /// Current pointer position, or <see langword="null"/> if the pointer is not over the panel.
    /// </summary>
    public PdfPanelPointerPosition? PointerPosition { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is UserInterfaceDrawingRequest other)
        {
            return base.Equals(obj)
                && PointerPosition == other.PointerPosition;
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(base.GetHashCode());
        hash.Add(PointerPosition);
        return hash.ToHashCode();
    }
}
