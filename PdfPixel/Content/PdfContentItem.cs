using PdfPixel.Commands;
using PdfPixel.Geometry;

namespace PdfPixel.Content;

/// <summary>
/// A content command placed on the page by one execution.
/// </summary>
public sealed class PdfContentItem : PdfContentElement
{
    /// <summary>
    /// Initializes a new <see cref="PdfContentItem"/> with the command and the total transformation matrix it executed under.
    /// </summary>
    internal PdfContentItem(PdfContentNode parent, IPdfContentCommand command, in PdfMatrix matrix)
        : base(parent)
    {
        Command = command;
        Matrix = matrix;
    }

    /// <summary>
    /// The executed content command.
    /// </summary>
    public IPdfContentCommand Command { get; }

    /// <summary>
    /// Total transformation matrix the command executed under; <see cref="IMatrixCommand.Matrix"/> of
    /// <see cref="Command"/> applies on top of it.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <inheritdoc />
    public override PdfContentElementType ElementType => PdfContentElementType.Item;
}
