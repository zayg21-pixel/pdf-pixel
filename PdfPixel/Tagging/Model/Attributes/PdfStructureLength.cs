namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// A layout length given either as a number or as a named mode.
/// </summary>
public readonly struct PdfStructureLength
{
    internal PdfStructureLength(float value)
        => Value = value;

    internal PdfStructureLength(PdfStructureLengthMode mode)
        => Mode = mode;

    /// <summary>
    /// Length in default user space units, or <see langword="null"/> when given as <see cref="Mode"/>.
    /// </summary>
    public float? Value { get; }

    /// <summary>
    /// Named mode, or <see langword="null"/> when given as <see cref="Value"/>.
    /// </summary>
    public PdfStructureLengthMode? Mode { get; }
}
