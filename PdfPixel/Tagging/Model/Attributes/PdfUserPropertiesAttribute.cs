using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// User properties (/O /UserProperties, PDF 1.6).
/// </summary>
public sealed class PdfUserPropertiesAttribute : PdfStructureAttributeBase
{
    internal PdfUserPropertiesAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.UserProperties, revision)
    {
        Properties = ReadProperties(dictionary.GetArray(PdfTokens.UserPropertiesKey));
    }

    /// <summary>
    /// User properties (P) in array order.
    /// </summary>
    public IReadOnlyList<PdfUserProperty> Properties { get; }

    private static List<PdfUserProperty> ReadProperties(PdfArray? properties)
    {
        List<PdfUserProperty> result = [];
        if (properties == null)
        {
            return result;
        }

        for (int index = 0; index < properties.Count; index++)
        {
            PdfDictionary? property = properties.GetDictionary(index);
            PdfString? name = property?.GetString(PdfTokens.UserPropertyNameKey);
            IPdfValue? value = property?.GetValue(PdfTokens.ValueKey);
            if (property == null || name == null || value == null)
            {
                continue;
            }

            result.Add(new PdfUserProperty(
                name.Value,
                value,
                property.GetString(PdfTokens.UserPropertyFormattedValueKey),
                property.GetBoolean(PdfTokens.UserPropertyHiddenKey)));
        }

        return result;
    }
}
