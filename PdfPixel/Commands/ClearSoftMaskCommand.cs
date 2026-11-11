namespace PdfPixel.Commands;

/// <summary>
/// Removes the soft mask from the graphics state (/SMask /None).
/// </summary>
public sealed class ClearSoftMaskCommand : PdfCommand
{
    private ClearSoftMaskCommand()
    {
    }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.ClearSoftMask;

    /// <summary>
    /// Default instance of <see cref="ClearSoftMaskCommand"/>.
    /// </summary>
    public static ClearSoftMaskCommand Instance { get; } = new();
}
