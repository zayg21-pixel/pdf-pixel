namespace PdfPixel.Commands;

/// <summary>
/// Begins a text object (BT).
/// </summary>
public sealed class BeginTextObjectCommand : PdfCommand
{
    private BeginTextObjectCommand(bool knockout) => Knockout = knockout;

    /// <summary>
    /// Gets the command beginning a text object whose glyphs knock out the ones before them.
    /// </summary>
    public static BeginTextObjectCommand KnockoutInstance { get; } = new(knockout: true);

    /// <summary>
    /// Gets the command beginning a text object whose glyphs composite with one another.
    /// </summary>
    public static BeginTextObjectCommand Instance { get; } = new(knockout: false);

    /// <summary>
    /// Gets the text knockout parameter (TK) in effect at the beginning of the text object.
    /// </summary>
    public bool Knockout { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.BeginTextObject;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(BeginTextObjectCommand)}{(Knockout ? " knockout" : string.Empty)}";
}
