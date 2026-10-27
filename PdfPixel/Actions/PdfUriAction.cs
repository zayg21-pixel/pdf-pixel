using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Actions;

/// <summary>
/// Represents a URI action that resolves a uniform resource identifier.
/// </summary>
public class PdfUriAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUriAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the URI action.</param>
    internal PdfUriAction(PdfDictionary actionDictionary)
        : base(PdfActionType.Uri)
    {
        Uri = actionDictionary.GetString(PdfTokens.URIKey);
        IsMap = actionDictionary.GetBooleanOrDefault(PdfTokens.IsMapKey);
    }

    /// <summary>
    /// Gets the uniform resource identifier to resolve.
    /// </summary>
    public PdfString? Uri { get; }

    /// <summary>
    /// Gets a value indicating whether the URI is a map coordinate.
    /// </summary>
    public bool IsMap { get; }

    /// <summary>
    /// Returns a string representation of this URI action.
    /// </summary>
    /// <returns>A string containing the URI.</returns>
    public override string ToString() => $"URI Action: {Uri}";
}
