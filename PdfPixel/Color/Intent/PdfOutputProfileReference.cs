using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Color.Intent;

/// <summary>
/// Information about one or more referenced ICC profiles (output intent /DestOutputProfileRef, PDF 2.0).
/// </summary>
public sealed class PdfOutputProfileReference
{
    internal PdfOutputProfileReference(PdfDictionary dictionary)
    {
        CheckSum = dictionary.GetString(PdfTokens.CheckSumKey);
        ColorantTable = ReadColorantTable(dictionary.GetArray(PdfTokens.ColorantTableKey));
        ICCVersion = dictionary.GetString(PdfTokens.ICCVersionKey);
        ProfileCS = dictionary.GetString(PdfTokens.ProfileCSKey);
        ProfileName = dictionary.GetString(PdfTokens.ProfileNameKey);
        URLs = PdfFileSpecification.FromArray(dictionary.GetArray(PdfTokens.URLsKey));
    }

    /// <summary>
    /// MD5 hash of the uncompressed ICC profile (/CheckSum), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? CheckSum { get; }

    /// <summary>
    /// Colorant names in the order of the profile's colorantTableTag (/ColorantTable),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfString>? ColorantTable { get; }

    /// <summary>
    /// ICC profile version number from bytes 8 to 11 of the profile header (/ICCVersion),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ICCVersion { get; }

    /// <summary>
    /// Four-byte color space signature of the profile (/ProfileCS), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ProfileCS { get; }

    /// <summary>
    /// Value of the profile's profileDescriptionTag (/ProfileName), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ProfileName { get; }

    /// <summary>
    /// Embedded file or URL specifications locating the profile (/URLs), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfFileSpecification>? URLs { get; }

    private static List<PdfString>? ReadColorantTable(PdfArray? array)
    {
        if (array == null)
        {
            return null;
        }

        List<PdfString> colorants = new(array.Count);
        for (int index = 0; index < array.Count; index++)
        {
            PdfString? colorant = array.GetName(index);
            if (colorant != null)
            {
                colorants.Add(colorant.Value);
            }
        }

        return colorants;
    }
}
