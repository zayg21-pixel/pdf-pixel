using PdfPixel.Models;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// A user property (PDF 1.6) of a structure element.
/// </summary>
public sealed class PdfUserProperty
{
    internal PdfUserProperty(in PdfString name, IPdfValue value, PdfString? formattedValue, bool? hidden)
    {
        Name = name;
        Value = value;
        FormattedValue = formattedValue;
        Hidden = hidden;
    }

    /// <summary>
    /// Name of the property (N).
    /// </summary>
    public PdfString Name { get; }

    /// <summary>
    /// Value of the property (V), of any type.
    /// </summary>
    public IPdfValue Value { get; }

    /// <summary>
    /// Formatted representation of <see cref="Value"/> (F), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? FormattedValue { get; }

    /// <summary>
    /// Whether the property is hidden from user interfaces (H), or <see langword="null"/> when absent.
    /// </summary>
    public bool? Hidden { get; }
}
