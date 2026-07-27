namespace PdfPixel.Commands;

/// <summary>
/// Base class for all PDF drawing commands.
/// </summary>
public abstract class PdfCommand : IPdfCommand
{
    /// <inheritdoc />
    public abstract PdfCommandKind Kind { get; }

    /// <inheritdoc />
    public virtual PdfCommandFeatures Features => PdfCommandFeatures.None;
}
