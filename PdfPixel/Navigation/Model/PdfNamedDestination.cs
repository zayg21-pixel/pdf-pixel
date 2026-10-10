using PdfPixel.Models;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// Destination referred to by name (a name object or byte string), looked up in the document's /Dests
/// dictionary or /Names /Dests name tree.
/// </summary>
public sealed class PdfNamedDestination : PdfDestination
{
    /// <summary>
    /// Initializes a destination referring to a named destination.
    /// </summary>
    /// <param name="name">Name the destination is declared under.</param>
    public PdfNamedDestination(in PdfString name) => Name = name;

    /// <summary>
    /// Name the destination is declared under.
    /// </summary>
    public PdfString Name { get; }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString() => $"Destination: {Name}";
}
