using PdfPixel.Geometry;
using PdfPixel.TextExtraction;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Carries extracted text characters in pre-CTM space. During execution, each character's
/// bounding box is mapped through <see cref="Matrix"/> concatenated onto
/// <see cref="PdfCommandExecutionFrames.TotalMatrix"/> to produce page-space coordinates,
/// then appended to the current text block in <see cref="PdfCommandExecutionContext.MarkedContent"/>.
/// </summary>
public sealed class TextCharactersCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with the matrix to apply and the captured characters whose bounding boxes are in pre-CTM space.
    /// </summary>
    public TextCharactersCommand(in PdfMatrix matrix, PdfCharacter[] characters)
    {
        Matrix = matrix;
        Characters = characters;
    }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the captured characters, with bounding boxes in pre-CTM space.
    /// </summary>
    public PdfCharacter[] Characters { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.TextCharacters;

    /// <inheritdoc />
    public override string ToString()
    {
        string text = string.Concat(Characters.Select(character => character.Text));
        return $"{nameof(TextCharactersCommand)} {CommandHelpers.FormatMatrix(Matrix)} \"{text}\"";
    }
}
