using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Optional content group (layer) dictionary (/OCG).
/// </summary>
public sealed class PdfOptionalContentGroup
{
    internal PdfOptionalContentGroup(PdfObject groupObject, in PdfString name)
    {
        Reference = groupObject.Reference;
        Name = name;
        Intent = PdfOptionalContentIntent.FromDictionary(groupObject.Dictionary);

        PdfDictionary? usage = groupObject.Dictionary.GetDictionary(PdfTokens.UsageKey);
        if (usage != null)
        {
            Usage = new PdfOptionalContentUsage(usage);
        }
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
    /// Intended use of the group's graphics (/Intent), or <see langword="null"/> when absent (View).
    /// </summary>
    public IReadOnlyList<PdfOptionalContentIntent>? Intent { get; }

    /// <summary>
    /// Nature of the content controlled by the group (/Usage), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOptionalContentUsage? Usage { get; }
}
