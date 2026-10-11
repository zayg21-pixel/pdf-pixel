using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using PdfPixel.Text;
using System;

namespace PdfPixel.Files;

/// <summary>
/// An embedded file stream (/EmbeddedFile, PDF 1.3).
/// </summary>
public sealed class PdfEmbeddedFile
{
    private PdfEmbeddedFile(PdfObject fileObject)
    {
        Reference = fileObject.Reference;
        Stream = fileObject.Stream;
        Subtype = fileObject.Dictionary.GetName(PdfTokens.SubtypeKey);

        PdfDictionary? parameters = fileObject.Dictionary.GetDictionary(PdfTokens.ParamsKey);
        if (parameters != null)
        {
            Size = parameters.GetInteger(PdfTokens.SizeKey);
            CreationDate = PdfDateParser.ParsePdfDate(parameters.GetString(PdfTokens.CreationDateKey));
            ModificationDate = PdfDateParser.ParsePdfDate(parameters.GetString(PdfTokens.ModDateKey));
            CheckSum = parameters.GetString(PdfTokens.CheckSumKey);
        }
    }

    /// <summary>
    /// Reference of the object holding <see cref="Stream"/>.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Stream holding the file content.
    /// </summary>
    public PdfObjectStream Stream { get; }

    /// <summary>
    /// MIME media type of the file (Subtype), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Subtype { get; }

    /// <summary>
    /// Size of the uncompressed file in bytes (Params /Size), or <see langword="null"/> when absent.
    /// </summary>
    public int? Size { get; }

    /// <summary>
    /// Creation date of the file (Params /CreationDate), or <see langword="null"/> when absent.
    /// </summary>
    public DateTime? CreationDate { get; }

    /// <summary>
    /// Last modification date of the file (Params /ModDate), or <see langword="null"/> when absent.
    /// </summary>
    public DateTime? ModificationDate { get; }

    /// <summary>
    /// MD5 checksum of the uncompressed file (Params /CheckSum), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? CheckSum { get; }

    /// <summary>
    /// The embedded file stream at <paramref name="reference"/>, or <see langword="null"/> when there is none.
    /// </summary>
    internal static PdfEmbeddedFile? FromReference(IPdfDocumentInternal document, PdfReference? reference)
    {
        if (reference == null)
        {
            return null;
        }

        return document.ObjectCache.GetOrCreate(reference.Value, FromObject, requiresStream: true);
    }

    private static PdfEmbeddedFile FromObject(PdfObject fileObject) => new(fileObject);
}
