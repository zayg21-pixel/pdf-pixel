using PdfPixel.Geometry;
using PdfPixel.Text;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Carries the content of one painting operation together with its bounds in pre-CTM space.
/// </summary>
/// <typeparam name="T">Type of the painted content.</typeparam>
public sealed class PdfContentCommand<T> : PdfCommand, IPdfContentCommand
{
    /// <summary>
    /// Initializes the command with the kind of content, the matrix to apply, the bounds in pre-CTM space and the content.
    /// </summary>
    public PdfContentCommand(PdfContentKind contentKind, in PdfMatrix matrix, in PdfRectangle bounds, T content)
    {
        ContentKind = contentKind;
        Matrix = matrix;
        Bounds = bounds;
        Content = content;
    }

    /// <inheritdoc />
    public PdfContentKind ContentKind { get; }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <inheritdoc />
    public PdfRectangle Bounds { get; }

    /// <summary>
    /// The painted content.
    /// </summary>
    public T Content { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.Content;

    /// <inheritdoc />
    public override string ToString()
    {
        if (Content is PdfCharacter[] characters)
        {
            string text = string.Concat(characters.Select(character => character.Text));
            return $"PdfContentCommand {ContentKind} {PdfCommandFormatting.FormatMatrix(Matrix)} \"{text}\"";
        }

        return $"PdfContentCommand {ContentKind} {PdfCommandFormatting.FormatMatrix(Matrix)} {Bounds}";
    }
}
