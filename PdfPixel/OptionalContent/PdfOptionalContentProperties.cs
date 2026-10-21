using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent;

/// <summary>
/// Optional content properties dictionary (catalog /OCProperties).
/// </summary>
public sealed class PdfOptionalContentProperties
{
    internal PdfOptionalContentProperties(PdfDictionary? dictionary, PdfString? documentLanguage)
    {
        DocumentLanguage = documentLanguage;

        if (dictionary == null)
        {
            return;
        }

        Dictionary<PdfReference, PdfOptionalContentGroup> groups = [];
        List<PdfObject>? groupObjects = dictionary.GetObjects(PdfTokens.OptionalContentGroupsKey);

        if (groupObjects != null)
        {
            foreach (PdfObject groupObject in groupObjects)
            {
                PdfString? name = groupObject.Dictionary.GetString(PdfTokens.NameKey);
                if (groupObject.Reference != null && name != null)
                {
                    groups[groupObject.Reference.Value] = new PdfOptionalContentGroup(groupObject, groupObject.Reference.Value, name.Value);
                }
            }
        }

        Groups = groups;

        PdfDictionary? defaultConfiguration = dictionary.GetDictionary(PdfTokens.DefaultConfigKey);
        DefaultConfiguration = new PdfOptionalContentConfiguration(defaultConfiguration ?? new PdfDictionary(dictionary.Document), this, null);

        PdfArray? configurationArray = dictionary.GetArray(PdfTokens.ConfigsKey);
        if (configurationArray == null)
        {
            return;
        }

        List<PdfOptionalContentConfiguration> configurations = [];
        for (int index = 0; index < configurationArray.Count; index++)
        {
            PdfDictionary? configuration = configurationArray.GetDictionary(index);
            if (configuration != null)
            {
                configurations.Add(new PdfOptionalContentConfiguration(configuration, this, DefaultConfiguration));
            }
        }

        Configurations = configurations;
    }

    /// <summary>
    /// Optional content groups of the document (/OCGs), keyed by their indirect reference,
    /// or <see langword="null"/> when the document has no /OCProperties.
    /// </summary>
    public IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup>? Groups { get; }

    /// <summary>
    /// Configuration applied when the document is opened (/D), or <see langword="null"/> when the document has
    /// no /OCProperties.
    /// </summary>
    public PdfOptionalContentConfiguration? DefaultConfiguration { get; }

    /// <summary>
    /// Alternate configurations (/Configs), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentConfiguration>? Configurations { get; }

    /// <summary>
    /// Document /Lang, the language resolution falls back to when none is given.
    /// </summary>
    internal PdfString? DocumentLanguage { get; }
}
