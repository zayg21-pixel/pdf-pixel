using SkiaSharp;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Pointer position mapped to a specific page's content coordinate space.
/// </summary>
public readonly struct PointerPagePosition
{
    /// <summary>
    /// Initializes a new <see cref="PointerPagePosition"/> with the given page number, content-space position, and button state.
    /// </summary>
    public PointerPagePosition(int pageNumber, SKPoint position, PdfPanelButtonState state)
    {
        PageNumber = pageNumber;
        Position = position;
        State = state;
    }

    /// <summary>
    /// 1-based page number the pointer is over.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Pointer position in the page's content coordinate space.
    /// </summary>
    public SKPoint Position { get; }

    /// <summary>
    /// Pointer button state at the time of this position snapshot.
    /// </summary>
    public PdfPanelButtonState State { get; }
}
