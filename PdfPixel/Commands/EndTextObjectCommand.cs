namespace PdfPixel.Commands;

/// <summary>
/// Ends a text object (ET), mirroring <see cref="BeginTextObjectCommand"/>.
/// </summary>
public sealed class EndTextObjectCommand : PdfCommand
{
    private EndTextObjectCommand()
    {
    }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.EndTextObject;

    /// <summary>
    /// Default instance of <see cref="EndTextObjectCommand"/>.
    /// </summary>
    public static EndTextObjectCommand Instance { get; } = new();
}
