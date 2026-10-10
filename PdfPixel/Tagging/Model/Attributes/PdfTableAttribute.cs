using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Table attributes (/O /Table).
/// </summary>
public sealed class PdfTableAttribute : PdfStructureAttributeBase
{
    internal PdfTableAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.Table, revision)
    {
        RowSpan = dictionary.GetInteger(PdfTokens.RowSpanKey);
        ColumnSpan = dictionary.GetInteger(PdfTokens.ColumnSpanKey);
        Headers = ReadHeaders(dictionary.GetArray(PdfTokens.HeadersKey));
        Scope = dictionary.GetName(PdfTokens.ScopeKey)?.AsEnum<PdfStructureTableScope>();
        Summary = dictionary.GetString(PdfTokens.SummaryKey);
        Short = dictionary.GetString(PdfTokens.ShortKey);
    }

    /// <summary>
    /// Number of rows the cell spans (RowSpan), or <see langword="null"/> when absent.
    /// </summary>
    public int? RowSpan { get; }

    /// <summary>
    /// Number of columns the cell spans (ColSpan), or <see langword="null"/> when absent.
    /// </summary>
    public int? ColumnSpan { get; }

    /// <summary>
    /// Identifiers (/ID) of the header cells of the cell (Headers), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString[]? Headers { get; }

    /// <summary>
    /// Cells a header cell applies to (Scope), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureTableScope? Scope { get; }

    /// <summary>
    /// Summary of the table's purpose and structure (Summary), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Summary { get; }

    /// <summary>
    /// Short form of a header cell's content (Short, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Short { get; }

    private static PdfString[]? ReadHeaders(PdfArray? headers)
    {
        if (headers == null)
        {
            return null;
        }

        List<PdfString> identifiers = new(headers.Count);
        for (int index = 0; index < headers.Count; index++)
        {
            PdfString? identifier = headers.GetString(index);
            if (identifier != null)
            {
                identifiers.Add(identifier.Value);
            }
        }

        return identifiers.ToArray();
    }
}
