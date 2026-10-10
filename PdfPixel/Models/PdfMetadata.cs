using PdfPixel.Streams;
using PdfPixel.Text;
using System;

namespace PdfPixel.Models;

/// <summary>
/// Metadata stream (/Metadata) holding XMP metadata of a document or an object.
/// </summary>
public sealed class PdfMetadata
{
    internal PdfMetadata(PdfObject metadataObject)
    {
        Reference = metadataObject.Reference;
        Stream = metadataObject.Stream;
    }

    /// <summary>
    /// Reference of the object holding <see cref="Stream"/>.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Stream holding the XMP packet.
    /// </summary>
    public PdfObjectStream Stream { get; }

    /// <summary>
    /// Returns the decoded XMP packet bytes.
    /// </summary>
    public ReadOnlyMemory<byte> GetData() => Stream.DecodeAsMemory();

    /// <summary>
    /// The metadata stream stored under /Metadata in <paramref name="dictionary"/>, or <see langword="null"/>
    /// when there is none.
    /// </summary>
    /// <param name="dictionary">Dictionary owning the /Metadata entry.</param>
    internal static PdfMetadata? FromDictionary(PdfDictionary dictionary)
    {
        PdfObject? metadataObject = dictionary.GetObject(PdfTokens.MetadataKey);
        if (metadataObject == null || !metadataObject.HasStream)
        {
            return null;
        }

        return new PdfMetadata(metadataObject);
    }
}
