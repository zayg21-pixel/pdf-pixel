using System;
using PdfPixel.Encryption.Decryption;
using PdfPixel.Encryption.Model;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Parsing;

/// <summary>
/// Parses the trailer dictionary during a linear scan capturing minimal encryption related fields.
/// </summary>
internal sealed class PdfTrailerParser
{
    private readonly IPdfDocumentInternal _document;

    public PdfTrailerParser(IPdfDocumentInternal document)
        => _document = document ?? throw new ArgumentNullException(nameof(document));

    /// <summary>
    /// Return the /Prev offset from the current trailer dictionary.
    /// </summary>
    public int? GetPrevOffset(PdfDictionary? trailer)
    {
        if (trailer == null)
        {
            return null;
        }

        int? prev = trailer.GetInteger(PdfTokens.PrevKey);
        if (!prev.HasValue || prev.Value < 0)
        {
            return null;
        }

        return prev.Value;
    }

    /// <summary>
    /// Return the /XRefStm offset from the current trailer dictionary. A hybrid-reference file puts it
    /// on the trailer of a classic section to point at the cross-reference stream that indexes the
    /// objects the section itself cannot describe, chiefly the compressed ones.
    /// </summary>
    public int? GetCrossReferenceStreamOffset(PdfDictionary? trailer)
    {
        if (trailer == null)
        {
            return null;
        }

        int? offset = trailer.GetInteger(PdfTokens.XRefStmKey);
        if (!offset.HasValue || offset.Value < 0)
        {
            return null;
        }

        return offset.Value;
    }

    public void TrySetDecryptor(PdfDictionary trailer)
    {
        if (trailer == null)
        {
            return;
        }

        if (_document.Decryptor != null)
        {
            return; // Already set (e.g., from /Encrypt in an object stream)
        }

        PdfDictionary? encryptDict = trailer.GetDictionary(PdfTokens.EncryptKey);
        if (encryptDict == null)
        {
            return; // Not encrypted
        }

        PdfDecryptorParameters parameters = new(encryptDict, trailer.GetArray(PdfTokens.IdKey));

        // Resolving the entries above may run the recovery scan, which sets its own decryptor.
        if (_document.Decryptor == null)
        {
            _document.Decryptor = PdfDecryptorFactory.Create(parameters, _document.OnCredentialRequested);
        }
    }
}
