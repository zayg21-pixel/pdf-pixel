using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Usage application dictionary, an entry of a configuration's /AS array.
/// </summary>
public sealed class PdfOptionalContentUsageApplication
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOptionalContentUsageApplication"/> class.
    /// </summary>
    /// <param name="event">Situation in which the application is used.</param>
    /// <param name="groups">Groups whose states are managed.</param>
    /// <param name="category">Usage dictionary entries consulted.</param>
    public PdfOptionalContentUsageApplication(PdfOptionalContentEvent @event, IReadOnlyList<PdfReference> groups, IReadOnlyList<PdfOptionalContentUsageCategory> category)
    {
        Event = @event;
        Groups = groups;
        Category = category;
    }

    /// <summary>
    /// Situation in which this usage application is used (/Event).
    /// </summary>
    public PdfOptionalContentEvent Event { get; }

    /// <summary>
    /// Groups whose states are managed by their usage dictionaries (/OCGs).
    /// </summary>
    public IReadOnlyList<PdfReference> Groups { get; }

    /// <summary>
    /// Usage dictionary entries consulted to compute the recommended state (/Category).
    /// </summary>
    public IReadOnlyList<PdfOptionalContentUsageCategory> Category { get; }

    /// <summary>
    /// Reads an /AS array, or returns null when absent.
    /// </summary>
    /// <param name="applications">The /AS array.</param>
    internal static List<PdfOptionalContentUsageApplication>? FromArray(PdfArray? applications)
    {
        if (applications == null)
        {
            return null;
        }

        List<PdfOptionalContentUsageApplication> result = new(applications.Count);
        for (int index = 0; index < applications.Count; index++)
        {
            PdfDictionary? application = applications.GetDictionary(index);
            if (application == null)
            {
                continue;
            }

            PdfOptionalContentEvent @event = application.GetNameOrDefault(PdfTokens.EventKey).AsEnum<PdfOptionalContentEvent>();
            List<PdfReference> groups = PdfOptionalContentConfiguration.ReadReferences(application.GetArray(PdfTokens.OptionalContentGroupsKey)) ?? [];
            List<PdfOptionalContentUsageCategory> category = ReadCategory(application.GetArray(PdfTokens.CategoryKey));
            result.Add(new PdfOptionalContentUsageApplication(@event, groups, category));
        }

        return result;
    }

    private static List<PdfOptionalContentUsageCategory> ReadCategory(PdfArray? names)
    {
        List<PdfOptionalContentUsageCategory> category = [];
        if (names == null)
        {
            return category;
        }

        for (int index = 0; index < names.Count; index++)
        {
            PdfString? name = names.GetName(index);
            if (name != null)
            {
                category.Add(name.Value.AsEnum<PdfOptionalContentUsageCategory>());
            }
        }

        return category;
    }
}
