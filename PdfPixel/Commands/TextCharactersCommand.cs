using PdfPixel.TextExtraction;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Carries extracted text characters in pre-CTM space. During execution, each character's
/// bounding box is mapped through <see cref="PdfCommandExecutionFrames.TotalMatrix"/> to
/// produce page-space coordinates, then appended to <see cref="PdfCommandExecutionContext.Characters"/>.
/// </summary>
public sealed class TextCharactersCommand : PdfCommand
{
    private readonly PdfCharacter[] _characters;

    /// <summary>
    /// Initializes the command with the captured characters whose bounding boxes are in pre-CTM space.
    /// </summary>
    public TextCharactersCommand(PdfCharacter[] characters) => _characters = characters;

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        SKMatrix matrix = executionContext.Frames.TotalMatrix;

        for (int i = 0; i < _characters.Length; i++)
        {
            //executionContext.Canvas.DrawRect(_characters[i].BoundingBox, new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Red });

            SKRect pageRect = matrix.MapRect(_characters[i].BoundingBox).Standardized;
            if (pageRect.Width != 0)
            {
                executionContext.Characters.Add(new PdfCharacter(_characters[i].Text, pageRect));
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
