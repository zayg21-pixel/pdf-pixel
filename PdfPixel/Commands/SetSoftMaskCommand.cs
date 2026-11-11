using System;

namespace PdfPixel.Commands;

/// <summary>
/// Sets the soft mask of the graphics state, saved and restored with it.
/// </summary>
public sealed class SetSoftMaskCommand : PdfCommand
{
    /// <summary>
    /// Initializes the command with the soft mask to set.
    /// </summary>
    /// <param name="mask">The soft mask.</param>
    public SetSoftMaskCommand(PdfSoftMaskSource mask) => Mask = mask ?? throw new ArgumentNullException(nameof(mask));

    /// <summary>
    /// Gets the soft mask.
    /// </summary>
    public PdfSoftMaskSource Mask { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.SetSoftMask;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(SetSoftMaskCommand)} {Mask}";
}
