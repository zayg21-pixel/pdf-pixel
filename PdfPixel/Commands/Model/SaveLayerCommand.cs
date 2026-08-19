using PdfPixel.Color.Paint;
using PdfPixel.Geometry;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Saves a new layer, with optional bounds and paint.
/// </summary>
public sealed class SaveLayerCommand : PdfCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the layer bounds and paint.
    /// </summary>
    public SaveLayerCommand(in PdfRectangle bounds, PdfPaint? paint = null)
    {
        Bounds = bounds;
        Paint = paint;
    }

    /// <summary>
    /// Gets the bounds of the layer being saved.
    /// </summary>
    public PdfRectangle Bounds { get; }

    /// <inheritdoc />
    public PdfPaint? Paint { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.SaveLayer;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(SaveLayerCommand)} {((Paint != null) ? PdfCommandFormatting.FormatPaint(Paint) : "no paint")}";
}
