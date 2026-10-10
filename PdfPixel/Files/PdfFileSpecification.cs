using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Files;

/// <summary>
/// A file specification (PDF 1.1), referring to a file external to the document or embedded in it.
/// </summary>
public sealed class PdfFileSpecification
{
    private PdfFileSpecification(in PdfString file)
        => File = file;

    private PdfFileSpecification(PdfDictionary dictionary, PdfReference? reference)
    {
        Reference = reference;
        RawFileSystem = dictionary.GetName(PdfTokens.FileSystemKey);
        FileSystem = RawFileSystem?.AsEnum<PdfFileSystem>();
        File = dictionary.GetString(PdfTokens.FKey);
        UnicodeFile = dictionary.GetString(PdfTokens.UFKey);
        Dos = dictionary.GetString(PdfTokens.DosKey);
        Mac = dictionary.GetString(PdfTokens.MacKey);
        Unix = dictionary.GetString(PdfTokens.UnixKey);
        Id = ReadId(dictionary.GetArray(PdfTokens.IdKey));
        Volatile = dictionary.GetBoolean(PdfTokens.VolatileKey);

        PdfDictionary? embeddedFiles = dictionary.GetDictionary(PdfTokens.EFKey);
        EmbeddedFile = PdfEmbeddedFile.FromReference(dictionary.Document, embeddedFiles?.GetReference(PdfTokens.FKey));
        UnicodeEmbeddedFile = PdfEmbeddedFile.FromReference(dictionary.Document, embeddedFiles?.GetReference(PdfTokens.UFKey));

        PdfDictionary? relatedFiles = dictionary.GetDictionary(PdfTokens.RelatedFilesKey);
        RelatedFiles = ReadRelatedFiles(relatedFiles?.GetArray(PdfTokens.FKey));
        UnicodeRelatedFiles = ReadRelatedFiles(relatedFiles?.GetArray(PdfTokens.UFKey));

        Description = dictionary.GetString(PdfTokens.DescriptionKey);
        RawAssociatedFileRelationship = dictionary.GetName(PdfTokens.AssociatedFileRelationshipKey);
        AssociatedFileRelationship = RawAssociatedFileRelationship?.AsEnum<PdfFileRelationship>();
        EncryptedPayload = PdfEncryptedPayload.FromDictionary(dictionary.GetDictionary(PdfTokens.EncryptedPayloadKey));

        Thumbnail = PdfImage.FromDictionaryEntry(dictionary, PdfTokens.ThumbnailKey);
    }

    /// <summary>
    /// Indirect reference of the file specification dictionary, or <see langword="null"/> when it is a
    /// direct object or a string.
    /// </summary>
    public PdfReference? Reference { get; }

    /// <summary>
    /// File system interpreting the specification (FS), or <see langword="null"/> when absent.
    /// <see cref="PdfFileSystem.Raw"/> when the file system is not defined by a specification.
    /// </summary>
    public PdfFileSystem? FileSystem { get; }

    /// <summary>
    /// File system (FS) as written, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawFileSystem { get; }

    /// <summary>
    /// File specification string or URL (F, or the string form of the specification), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? File { get; }

    /// <summary>
    /// Unicode file specification string (UF, PDF 1.7), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? UnicodeFile { get; }

    /// <summary>
    /// DOS file name (DOS, deprecated in PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Dos { get; }

    /// <summary>
    /// Mac OS file name (Mac, deprecated in PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Mac { get; }

    /// <summary>
    /// UNIX file name (Unix, deprecated in PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Unix { get; }

    /// <summary>
    /// File identifier of the referenced file (ID), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString[]? Id { get; }

    /// <summary>
    /// Whether the referenced file changes frequently (V, PDF 1.2), or <see langword="null"/> when absent.
    /// </summary>
    public bool? Volatile { get; }

    /// <summary>
    /// Embedded file for <see cref="File"/> (EF /F, PDF 1.3), or <see langword="null"/> when absent.
    /// </summary>
    public PdfEmbeddedFile? EmbeddedFile { get; }

    /// <summary>
    /// Embedded file for <see cref="UnicodeFile"/> (EF /UF, PDF 1.7), or <see langword="null"/> when absent.
    /// </summary>
    public PdfEmbeddedFile? UnicodeEmbeddedFile { get; }

    /// <summary>
    /// Files related to <see cref="EmbeddedFile"/> (RF /F, PDF 1.3), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfRelatedFile>? RelatedFiles { get; }

    /// <summary>
    /// Files related to <see cref="UnicodeEmbeddedFile"/> (RF /UF, PDF 1.7), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfRelatedFile>? UnicodeRelatedFiles { get; }

    /// <summary>
    /// Description of the file (Desc, PDF 1.6), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Description { get; }

    /// <summary>
    /// Relationship of the associated file to its referring component (AFRelationship, PDF 2.0),
    /// or <see langword="null"/> when absent. <see cref="PdfFileRelationship.Raw"/> when the relationship
    /// is not defined by a specification.
    /// </summary>
    public PdfFileRelationship? AssociatedFileRelationship { get; }

    /// <summary>
    /// Relationship (AFRelationship) as written, or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawAssociatedFileRelationship { get; }

    /// <summary>
    /// Encrypted payload referenced by this specification (EP, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfEncryptedPayload? EncryptedPayload { get; }

    /// <summary>
    /// Thumbnail image of the file (Thumb, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfImage? Thumbnail { get; }

    // TODO: [MEDIUM] parse /CI collection item dictionaries, together with portable collections

    /// <summary>
    /// The file specification held by <paramref name="key"/> of <paramref name="owner"/>, in string or
    /// dictionary form, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfFileSpecification? FromDictionaryEntry(PdfDictionary owner, in PdfString key)
    {
        PdfReference? reference = owner.GetReference(key);
        if (reference == null)
        {
            return FromValue(owner.GetValue(key), null);
        }

        return FromReference(owner.Document, reference.Value);
    }

    /// <summary>
    /// The file specification at <paramref name="index"/> of <paramref name="array"/>, in string or
    /// dictionary form, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfFileSpecification? FromArrayEntry(PdfArray array, int index)
    {
        PdfReference? reference = array.GetReference(index);
        if (reference == null)
        {
            return FromValue(array.GetValue(index), null);
        }

        return FromReference(array.Document, reference.Value);
    }

    /// <summary>
    /// The document-level embedded files (/Names /EmbeddedFiles) of <paramref name="catalog"/>, keyed by name.
    /// Empty when the catalog has none.
    /// </summary>
    internal static Dictionary<PdfString, PdfFileSpecification> FromCatalog(PdfDictionary catalog, PdfTreeReader treeReader)
    {
        PdfDictionary? embeddedFiles = catalog.GetDictionary(PdfTokens.NamesKey)?.GetDictionary(PdfTokens.EmbeddedFilesKey);
        if (embeddedFiles == null)
        {
            return new Dictionary<PdfString, PdfFileSpecification>();
        }

        return treeReader.ReadNameTree(embeddedFiles, FromArrayEntry);
    }

    /// <summary>
    /// The file specifications in <paramref name="array"/>, or <see langword="null"/> when it is absent.
    /// </summary>
    internal static List<PdfFileSpecification>? FromArray(PdfArray? array)
    {
        if (array == null)
        {
            return null;
        }

        List<PdfFileSpecification> result = new(array.Count);
        for (int index = 0; index < array.Count; index++)
        {
            PdfFileSpecification? specification = FromArrayEntry(array, index);
            if (specification != null)
            {
                result.Add(specification);
            }
        }

        return result;
    }

    private static PdfFileSpecification? FromReference(IPdfDocumentInternal document, in PdfReference reference)
    {
        Dictionary<PdfReference, PdfFileSpecification> cache = document.ObjectCache.FileSpecifications;
        if (cache.TryGetValue(reference, out PdfFileSpecification? cached))
        {
            return cached;
        }

        PdfFileSpecification? specification = FromValue(document.ObjectCache.GetObject(reference)?.Value, reference);
        if (specification != null)
        {
            cache[reference] = specification;
        }

        return specification;
    }

    private static PdfFileSpecification? FromValue(IPdfValue? value, PdfReference? reference)
    {
        PdfString? file = value.AsString();
        if (file != null)
        {
            return new PdfFileSpecification(file.Value);
        }

        PdfDictionary? dictionary = value.AsDictionary();
        if (dictionary == null)
        {
            return null;
        }

        return new PdfFileSpecification(dictionary, reference);
    }

    private static PdfString[]? ReadId(PdfArray? identifiers)
    {
        if (identifiers == null)
        {
            return null;
        }

        List<PdfString> result = new(identifiers.Count);
        for (int index = 0; index < identifiers.Count; index++)
        {
            PdfString? identifier = identifiers.GetString(index);
            if (identifier != null)
            {
                result.Add(identifier.Value);
            }
        }

        return result.ToArray();
    }

    private static List<PdfRelatedFile>? ReadRelatedFiles(PdfArray? relatedFiles)
    {
        if (relatedFiles == null)
        {
            return null;
        }

        List<PdfRelatedFile> result = [];
        for (int index = 0; index + 1 < relatedFiles.Count; index += 2)
        {
            PdfString? name = relatedFiles.GetString(index);
            PdfEmbeddedFile? file = PdfEmbeddedFile.FromReference(relatedFiles.Document, relatedFiles.GetReference(index + 1));
            if (name != null && file != null)
            {
                result.Add(new PdfRelatedFile(name.Value, file));
            }
        }

        return result;
    }
}
