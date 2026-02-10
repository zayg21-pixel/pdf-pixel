using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a URI action that resolves a uniform resource identifier.
/// </summary>
/// <remarks>
/// URI actions are typically used for hyperlinks to web resources.
/// </remarks>
public class PdfUriAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfUriAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the URI action.</param>
    public PdfUriAction(PdfDictionary actionDictionary)
        : base(actionDictionary, PdfActionType.Uri)
    {
        Uri = actionDictionary.GetString(PdfTokens.URIKey);
        IsMap = actionDictionary.GetBooleanOrDefault(PdfTokens.IsMapKey);
    }

    /// <summary>
    /// Gets the uniform resource identifier to resolve.
    /// </summary>
    public PdfString Uri { get; }

    /// <summary>
    /// Gets a value indicating whether the URI is a map coordinate.
    /// </summary>
    /// <remarks>
    /// If true, the URI is interpreted as a base URI to which a query string with
    /// x,y coordinates will be appended when activated.
    /// Default is false.
    /// </remarks>
    public bool IsMap { get; }

    /// <summary>
    /// Returns a string representation of this URI action.
    /// </summary>
    /// <returns>A string containing the URI.</returns>
    public override string ToString()
    {
        return $"URI Action: {Uri}";
    }
}
