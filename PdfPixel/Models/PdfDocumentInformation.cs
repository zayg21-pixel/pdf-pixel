using PdfPixel.Parsing;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Document information dictionary (trailer /Info).
/// </summary>
public sealed class PdfDocumentInformation
{
    internal PdfDocumentInformation(PdfDictionary dictionary)
    {
        Title = dictionary.GetString(PdfTokens.InformationTitleKey);
        Author = dictionary.GetString(PdfTokens.AuthorKey);
        Subject = dictionary.GetString(PdfTokens.InformationSubjectKey);
        Keywords = dictionary.GetString(PdfTokens.KeywordsKey);
        Creator = dictionary.GetString(PdfTokens.CreatorKey);
        Producer = dictionary.GetString(PdfTokens.ProducerKey);
        CreationDate = PdfDateParser.ParsePdfDate(dictionary.GetString(PdfTokens.CreationDateKey));
        ModificationDate = PdfDateParser.ParsePdfDate(dictionary.GetString(PdfTokens.ModDateKey));
        IsTrapped = ReadTrapped(dictionary.GetName(PdfTokens.TrappedKey));
        CustomEntries = ReadCustomEntries(dictionary);
    }

    /// <summary>
    /// Document title (/Title), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Title { get; }

    /// <summary>
    /// Name of the person who created the document (/Author), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Author { get; }

    /// <summary>
    /// Subject of the document (/Subject), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Subject { get; }

    /// <summary>
    /// Keywords associated with the document (/Keywords), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Keywords { get; }

    /// <summary>
    /// Application that created the original document (/Creator), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Creator { get; }

    /// <summary>
    /// Application that converted the document to PDF (/Producer), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Producer { get; }

    /// <summary>
    /// Creation date (/CreationDate), or <see langword="null"/> when absent.
    /// </summary>
    public DateTime? CreationDate { get; }

    /// <summary>
    /// Date of the most recent modification (/ModDate), or <see langword="null"/> when absent.
    /// </summary>
    public DateTime? ModificationDate { get; }

    /// <summary>
    /// Whether the document has been fully trapped (/Trapped): <see langword="true"/> for /True,
    /// <see langword="false"/> for /False, <see langword="null"/> for /Unknown or when absent.
    /// </summary>
    public bool? IsTrapped { get; }

    /// <summary>
    /// Entries with keys not defined by the specification, keyed by their name.
    /// </summary>
    public IReadOnlyDictionary<PdfString, PdfString> CustomEntries { get; }

    private static bool? ReadTrapped(PdfString? trapped)
    {
        if (trapped == null)
        {
            return null;
        }

        return trapped.Value.ToString() switch
        {
            "True" => true,
            "False" => false,
            _ => null
        };
    }

    private static Dictionary<PdfString, PdfString> ReadCustomEntries(PdfDictionary dictionary)
    {
        Dictionary<PdfString, PdfString> entries = [];

        foreach (PdfString key in dictionary.RawValues.Keys)
        {
            if (IsStandardKey(key))
            {
                continue;
            }

            PdfString? value = dictionary.GetString(key);
            if (value != null)
            {
                entries[key] = value.Value;
            }
        }

        return entries;
    }

    private static bool IsStandardKey(in PdfString key)
    {
        return key == PdfTokens.InformationTitleKey
            || key == PdfTokens.AuthorKey
            || key == PdfTokens.InformationSubjectKey
            || key == PdfTokens.KeywordsKey
            || key == PdfTokens.CreatorKey
            || key == PdfTokens.ProducerKey
            || key == PdfTokens.CreationDateKey
            || key == PdfTokens.ModDateKey
            || key == PdfTokens.TrappedKey;
    }
}
