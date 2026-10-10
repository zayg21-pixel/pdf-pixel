using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Developer extensions dictionary, an entry of the catalog /Extensions dictionary.
/// </summary>
public sealed class PdfDeveloperExtension
{
    internal PdfDeveloperExtension(PdfDictionary dictionary)
    {
        PdfString? baseVersion = dictionary.GetName(PdfTokens.BaseVersionKey);
        BaseVersion = (baseVersion == null) ? null : PdfVersion.Parse(baseVersion.Value.Value.Span);
        ExtensionLevel = dictionary.GetInteger(PdfTokens.ExtensionLevelKey);
        Url = dictionary.GetString(PdfTokens.UrlKey);
        ExtensionRevision = dictionary.GetString(PdfTokens.ExtensionRevisionKey);
    }

    /// <summary>
    /// PDF version the extension applies to (/BaseVersion), or <see langword="null"/> when absent or malformed.
    /// </summary>
    public PdfVersion? BaseVersion { get; }

    /// <summary>
    /// Developer-defined extension level (/ExtensionLevel), or <see langword="null"/> when absent.
    /// </summary>
    public int? ExtensionLevel { get; }

    /// <summary>
    /// URL of the extension documentation (/URL, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Url { get; }

    /// <summary>
    /// Additional revision information of the extension level (/ExtensionRevision, PDF 2.0),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ExtensionRevision { get; }

    /// <summary>
    /// Reads the catalog /Extensions dictionary, keyed by developer prefix name.
    /// </summary>
    /// <param name="extensions">The /Extensions dictionary, or null when absent.</param>
    internal static Dictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>> FromExtensions(PdfDictionary? extensions)
    {
        Dictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>> result = [];

        if (extensions == null)
        {
            return result;
        }

        foreach (PdfString prefix in extensions.RawValues.Keys)
        {
            if (prefix == PdfTokens.TypeKey)
            {
                continue;
            }

            List<PdfDeveloperExtension> developerExtensions = [];
            PdfArray? array = extensions.GetArray(prefix);

            if (array != null)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    PdfDictionary? entry = array.GetDictionary(index);
                    if (entry != null)
                    {
                        developerExtensions.Add(new PdfDeveloperExtension(entry));
                    }
                }
            }
            else
            {
                PdfDictionary? entry = extensions.GetDictionary(prefix);
                if (entry != null)
                {
                    developerExtensions.Add(new PdfDeveloperExtension(entry));
                }
            }

            if (developerExtensions.Count > 0)
            {
                result[prefix] = developerExtensions;
            }
        }

        return result;
    }
}
