using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// One intent name of an optional content group or configuration (/Intent).
/// </summary>
public readonly struct PdfOptionalContentIntent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOptionalContentIntent"/> struct.
    /// </summary>
    /// <param name="type">Intent type.</param>
    /// <param name="rawType">Intent name as written, or null for a type defined by the specification.</param>
    public PdfOptionalContentIntent(PdfOptionalContentIntentType type, PdfString? rawType = null)
    {
        Type = type;
        RawType = rawType;
    }

    /// <summary>
    /// Intent type.
    /// </summary>
    public PdfOptionalContentIntentType Type { get; }

    /// <summary>
    /// Intent name as written when <see cref="Type"/> is <see cref="PdfOptionalContentIntentType.Raw"/>, otherwise null.
    /// </summary>
    public PdfString? RawType { get; }

    /// <summary>
    /// Returns whether this intent names the same intent as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The intent to compare with.</param>
    internal bool Matches(in PdfOptionalContentIntent other)
    {
        if (Type != other.Type)
        {
            return false;
        }

        if (Type != PdfOptionalContentIntentType.Raw)
        {
            return true;
        }

        return RawType != null && other.RawType != null && RawType.Value == other.RawType.Value;
    }

    /// <summary>
    /// Reads an /Intent entry holding a name or an array of names, or returns null when absent.
    /// </summary>
    /// <param name="dictionary">Dictionary owning the /Intent entry.</param>
    internal static List<PdfOptionalContentIntent>? FromDictionary(PdfDictionary dictionary)
    {
        PdfString? name = dictionary.GetName(PdfTokens.OptionalContentIntentKey);
        if (name != null)
        {
            return new List<PdfOptionalContentIntent> { FromName(name.Value) };
        }

        PdfArray? names = dictionary.GetArray(PdfTokens.OptionalContentIntentKey);
        if (names == null)
        {
            return null;
        }

        List<PdfOptionalContentIntent> intents = new(names.Count);
        for (int index = 0; index < names.Count; index++)
        {
            PdfString? entry = names.GetName(index);
            if (entry != null)
            {
                intents.Add(FromName(entry.Value));
            }
        }

        return intents;
    }

    private static PdfOptionalContentIntent FromName(in PdfString name)
    {
        PdfOptionalContentIntentType type = name.AsEnum<PdfOptionalContentIntentType>();
        return (type == PdfOptionalContentIntentType.Raw) ? new PdfOptionalContentIntent(type, name) : new PdfOptionalContentIntent(type);
    }
}
