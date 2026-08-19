namespace PdfPixel.Commands.Model;

/// <summary>
/// Restores the most recently saved layer from the state stack, mirroring <see cref="SaveLayerCommand"/>.
/// </summary>
public sealed class RestoreLayerCommand : PdfCommand
{
    private RestoreLayerCommand()
    {
    }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.RestoreLayer;

    /// <summary>
    /// Default instance of <see cref="RestoreLayerCommand"/>.
    /// </summary>
    public static RestoreLayerCommand Instance { get; } = new();
}
