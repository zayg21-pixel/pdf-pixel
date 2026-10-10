using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Usage dictionary of an optional content group (/Usage), describing the nature of its content.
/// </summary>
public sealed class PdfOptionalContentUsage
{
    internal PdfOptionalContentUsage(PdfDictionary dictionary)
    {
        PdfDictionary? creatorInfo = dictionary.GetDictionary(PdfTokens.CreatorInfoKey);
        if (creatorInfo != null)
        {
            Creator = creatorInfo.GetString(PdfTokens.CreatorKey);
            PdfString? creatorSubtype = creatorInfo.GetName(PdfTokens.SubtypeKey);
            CreatorSubtype = creatorSubtype?.AsEnum<PdfOptionalContentCreatorSubtype>();
            RawCreatorSubtype = (CreatorSubtype == PdfOptionalContentCreatorSubtype.Raw) ? creatorSubtype : null;
        }

        PdfDictionary? language = dictionary.GetDictionary(PdfTokens.LanguageKey);
        if (language != null)
        {
            Lang = language.GetString(PdfTokens.LangKey);
            IsLanguagePreferred = ReadState(language, PdfTokens.PreferredKey);
        }

        IsExportOn = ReadState(dictionary.GetDictionary(PdfTokens.ExportKey), PdfTokens.ExportStateKey);

        PdfDictionary? zoom = dictionary.GetDictionary(PdfTokens.ZoomKey);
        if (zoom != null)
        {
            ZoomMin = zoom.GetFloat(PdfTokens.MinKey);
            ZoomMax = zoom.GetFloat(PdfTokens.MaxKey);
        }

        PdfDictionary? print = dictionary.GetDictionary(PdfTokens.PrintKey);
        if (print != null)
        {
            PdfString? printSubtype = print.GetName(PdfTokens.SubtypeKey);
            PrintSubtype = printSubtype?.AsEnum<PdfOptionalContentPrintSubtype>();
            RawPrintSubtype = (PrintSubtype == PdfOptionalContentPrintSubtype.Raw) ? printSubtype : null;
            IsPrintOn = ReadState(print, PdfTokens.PrintStateKey);
        }

        IsViewOn = ReadState(dictionary.GetDictionary(PdfTokens.ViewKey), PdfTokens.ViewStateKey);

        PdfDictionary? user = dictionary.GetDictionary(PdfTokens.UserKey);
        if (user != null)
        {
            UserType = user.GetName(PdfTokens.TypeKey)?.AsEnum<PdfOptionalContentUserType>();
            UserNames = ReadUserNames(user);
        }

        PageElement = dictionary.GetDictionary(PdfTokens.PageElementKey)?.GetName(PdfTokens.SubtypeKey)?.AsEnum<PdfOptionalContentPageElement>();
    }

    /// <summary>
    /// Application that created the group (/CreatorInfo /Creator), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Creator { get; }

    /// <summary>
    /// Type of content controlled by the group (/CreatorInfo /Subtype), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOptionalContentCreatorSubtype? CreatorSubtype { get; }

    /// <summary>
    /// /CreatorInfo /Subtype as written when <see cref="CreatorSubtype"/> is <see cref="PdfOptionalContentCreatorSubtype.Raw"/>, otherwise null.
    /// </summary>
    public PdfString? RawCreatorSubtype { get; }

    /// <summary>
    /// Language and locale of the content (/Language /Lang), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Lang { get; }

    /// <summary>
    /// Whether the group is preferred on a partial language match (/Language /Preferred), or <see langword="null"/> when absent.
    /// </summary>
    public bool? IsLanguagePreferred { get; }

    /// <summary>
    /// Recommended state on export (/Export /ExportState), or <see langword="null"/> when absent.
    /// </summary>
    public bool? IsExportOn { get; }

    /// <summary>
    /// Minimum magnification at which the group is ON (/Zoom /min), or <see langword="null"/> when absent.
    /// </summary>
    public float? ZoomMin { get; }

    /// <summary>
    /// Magnification below which the group is ON (/Zoom /max), or <see langword="null"/> when absent.
    /// </summary>
    public float? ZoomMax { get; }

    /// <summary>
    /// Kind of content controlled when printing (/Print /Subtype), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOptionalContentPrintSubtype? PrintSubtype { get; }

    /// <summary>
    /// /Print /Subtype as written when <see cref="PrintSubtype"/> is <see cref="PdfOptionalContentPrintSubtype.Raw"/>, otherwise null.
    /// </summary>
    public PdfString? RawPrintSubtype { get; }

    /// <summary>
    /// State of the group when printed (/Print /PrintState), or <see langword="null"/> when absent.
    /// </summary>
    public bool? IsPrintOn { get; }

    /// <summary>
    /// State of the group when the document is first opened (/View /ViewState), or <see langword="null"/> when absent.
    /// </summary>
    public bool? IsViewOn { get; }

    /// <summary>
    /// How <see cref="UserNames"/> are interpreted (/User /Type), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOptionalContentUserType? UserType { get; }

    /// <summary>
    /// Users the group is primarily intended for (/User /Name), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfString>? UserNames { get; }

    /// <summary>
    /// Pagination artifact the group contains (/PageElement /Subtype), or <see langword="null"/> when absent.
    /// </summary>
    public PdfOptionalContentPageElement? PageElement { get; }

    /// <summary>
    /// Reads an ON or OFF name: <see langword="true"/> for ON, <see langword="false"/> for OFF, null otherwise.
    /// </summary>
    internal static bool? ReadState(PdfDictionary? dictionary, in PdfString key)
    {
        PdfString? state = dictionary?.GetName(key);
        if (state == null)
        {
            return null;
        }

        return state.Value.ToString() switch
        {
            "ON" => true,
            "OFF" => false,
            _ => null
        };
    }

    private static List<PdfString>? ReadUserNames(PdfDictionary user)
    {
        PdfString? name = user.GetString(PdfTokens.UserNameKey);
        if (name != null)
        {
            return new List<PdfString> { name.Value };
        }

        PdfArray? names = user.GetArray(PdfTokens.UserNameKey);
        if (names == null)
        {
            return null;
        }

        List<PdfString> result = new(names.Count);
        for (int index = 0; index < names.Count; index++)
        {
            PdfString? entry = names.GetString(index);
            if (entry != null)
            {
                result.Add(entry.Value);
            }
        }

        return result;
    }
}
