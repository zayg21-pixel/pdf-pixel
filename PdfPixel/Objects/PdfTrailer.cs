using PdfPixel.Encryption;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Parsing;

namespace PdfPixel.Objects;

/// <summary>
/// Trailer of a cross-reference section: the classic <c>trailer</c> dictionary or the dictionary of a
/// cross-reference stream.
/// </summary>
internal sealed class PdfTrailer
{
    private readonly PdfDictionary _dictionary;

    public PdfTrailer(PdfDictionary dictionary) => _dictionary = dictionary;

    /// <summary>
    /// The /Size entry: one greater than the highest object number in the file.
    /// </summary>
    public int? Size => _dictionary.GetInteger(PdfTokens.SizeKey);

    /// <summary>
    /// The /Prev entry: declared offset of the previous cross-reference section, or null when absent or negative.
    /// </summary>
    public int? Prev => GetOffset(PdfTokens.PrevKey);

    /// <summary>
    /// The /XRefStm entry: declared offset of the cross-reference stream of a hybrid-reference file,
    /// or null when absent or negative.
    /// </summary>
    public int? XRefStm => GetOffset(PdfTokens.XRefStmKey);

    /// <summary>
    /// The /ID entry, or null when it does not hold two strings.
    /// </summary>
    public PdfFileIdentifier? Id => PdfFileIdentifier.FromArray(_dictionary.GetArray(PdfTokens.IdKey));

    /// <summary>
    /// Resolves the /Root entry.
    /// </summary>
    public PdfObject? GetRoot() => _dictionary.GetObject(PdfTokens.RootKey);

    /// <summary>
    /// Resolves the /Info entry.
    /// </summary>
    public PdfDictionary? GetInformation() => _dictionary.GetDictionary(PdfTokens.InfoKey);

    /// <summary>
    /// Creates the decryptor of the /Encrypt entry, or returns null when the trailer has none.
    /// </summary>
    /// <param name="onCredentialRequested">Called when the empty user password does not decrypt the document.</param>
    public PdfDecryptor? GetDecryptor(PdfCredentialRequestedCallback? onCredentialRequested)
    {
        PdfDictionary? encryptDictionary = _dictionary.GetDictionary(PdfTokens.EncryptKey);
        if (encryptDictionary == null)
        {
            return null;
        }

        PdfDecryptorParameters parameters = new(encryptDictionary, _dictionary.GetArray(PdfTokens.IdKey));

        return PdfDecryptor.GetDecryptor(parameters, onCredentialRequested);
    }

    private int? GetOffset(in PdfString key)
    {
        int? offset = _dictionary.GetInteger(key);
        if (offset == null || offset.Value < 0)
        {
            return null;
        }

        return offset;
    }
}
