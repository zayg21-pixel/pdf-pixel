namespace PdfPixel.Models;

/// <summary>
/// Represents a single optional content group (layer) with its name,
/// default visibility state, and position in the UI presentation order.
/// Use <see cref="WithVisibility"/> to create a copy with a different visibility state.
/// </summary>
public class PdfOptionalContentGroup
{
    /// <summary>
    /// Initializes a new <see cref="PdfOptionalContentGroup"/> with the specified reference and name.
    /// </summary>
    internal PdfOptionalContentGroup(in PdfReference reference, in PdfString name)
    {
        Reference = reference;
        Name = name;
    }

    private PdfOptionalContentGroup(PdfOptionalContentGroup source, bool? visibility)
    {
        Reference = source.Reference;
        Name = source.Name;
        DefaultVisible = source.DefaultVisible;
        Order = source.Order;
        Visible = visibility;
    }

    /// <summary>
    /// Indirect object reference identifying this OCG.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Display name of the group (/Name entry in the OCG dictionary).
    /// </summary>
    public PdfString Name { get; }

    /// <summary>
    /// Default visibility state from the /D configuration dictionary.
    /// True = visible (listed in /ON or not in /OFF), false = hidden (listed in /OFF),
    /// null = not specified in either /ON or /OFF arrays.
    /// </summary>
    public bool? DefaultVisible { get; internal set; }

    /// <summary>
    /// Zero-based position in the /Order array of the default configuration.
    /// Null if this group does not appear in the /Order array.
    /// Used to reconstruct the UI presentation hierarchy.
    /// </summary>
    public int? Order { get; internal set; }

    /// <summary>
    /// User-defined visibility override. When set, takes precedence over <see cref="DefaultVisible"/>
    /// when determining whether content in this group should be rendered.
    /// Null means no override — fall back to <see cref="DefaultVisible"/>.
    /// </summary>
    public bool? Visible { get; private set; }

    /// <summary>
    /// Creates a new <see cref="PdfOptionalContentGroup"/> with the specified visibility override.
    /// All other properties are copied from this instance.
    /// </summary>
    public PdfOptionalContentGroup WithVisibility(bool? visibility) => new(this, visibility);
}
