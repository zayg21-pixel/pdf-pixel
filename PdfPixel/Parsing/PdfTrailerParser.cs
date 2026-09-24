using System;
using PdfPixel.Encryption;
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

        PdfDecryptorParameters parameters = new();
        parameters.V = encryptDict.GetIntegerOrDefault(PdfTokens.VKey);
        parameters.R = encryptDict.GetIntegerOrDefault(PdfTokens.RKey);
        parameters.LengthBits = encryptDict.GetIntegerOrDefault(PdfTokens.LengthKey);
        parameters.Permissions = encryptDict.GetIntegerOrDefault(PdfTokens.PKey);

        bool? encryptMetadata = encryptDict.GetBoolean(PdfTokens.EncryptMetadataKey);
        if (encryptMetadata.HasValue)
        {
            parameters.EncryptMetadata = encryptMetadata.Value;
        }

        parameters.OwnerEntry = encryptDict.GetValue(PdfTokens.OKey).AsString()?.Value.ToArray();
        parameters.UserEntry = encryptDict.GetValue(PdfTokens.UKey).AsString()?.Value.ToArray();

        if (parameters.R >= 5)
        {
            parameters.OwnerEncryptedKey = encryptDict.GetValue(PdfTokens.OEKey).AsString()?.Value.ToArray();
            parameters.UserEncryptedKey = encryptDict.GetValue(PdfTokens.UEKey).AsString()?.Value.ToArray();
            parameters.Perms = encryptDict.GetValue(PdfTokens.PermsKey).AsString()?.Value.ToArray();
        }

        if (parameters.V >= 4)
        {
            PdfDictionary? cryptFilterDictionary = encryptDict.GetDictionary(PdfTokens.CFKey);
            if (cryptFilterDictionary != null)
            {
                foreach (PdfString name in cryptFilterDictionary.RawValues.Keys)
                {
                    PdfDictionary? entry = cryptFilterDictionary.GetDictionary(name);
                    if (entry == null)
                    {
                        continue;
                    }

                    PdfCryptFilter cryptFilter = new(
                        entry.GetNameOrDefault(PdfTokens.CfmKey).AsEnum<PdfCryptFilterMethod>(),
                        entry.GetInteger(PdfTokens.LengthKey),
                        entry.GetNameOrDefault(PdfTokens.AuthEventKey).AsEnum<PdfAuthEvent>());

                    parameters.CryptFilters[name] = cryptFilter;
                }
            }

            // StmF and StrF always authenticate on document open (ISO 32000-2, Table 25, AuthEvent).
            PdfCryptFilter streamCryptFilter = parameters.GetCryptFilter(encryptDict.GetName(PdfTokens.StmFKey));
            PdfCryptFilter stringCryptFilter = parameters.GetCryptFilter(encryptDict.GetName(PdfTokens.StrFKey));
            parameters.StreamCryptFilter = new PdfCryptFilter(streamCryptFilter.Method, streamCryptFilter.Length, PdfAuthEvent.DocumentOpen);
            parameters.StringCryptFilter = new PdfCryptFilter(stringCryptFilter.Method, stringCryptFilter.Length, PdfAuthEvent.DocumentOpen);

            PdfString? embeddedFileFilterName = encryptDict.GetName(PdfTokens.EffKey);
            if (embeddedFileFilterName == null)
            {
                parameters.EmbeddedFileCryptFilter = parameters.StreamCryptFilter;
            }
            else
            {
                parameters.EmbeddedFileCryptFilter = parameters.GetCryptFilter(embeddedFileFilterName);
            }
        }
        else
        {
            PdfCryptFilter handlerCryptFilter = new(PdfCryptFilterMethod.V2, null, PdfAuthEvent.DocumentOpen);
            parameters.StreamCryptFilter = handlerCryptFilter;
            parameters.StringCryptFilter = handlerCryptFilter;
            parameters.EmbeddedFileCryptFilter = handlerCryptFilter;
        }

        PdfArray? idArray = trailer.GetArray(PdfTokens.IdKey);
        if (idArray?.Count >= 2)
        {
            parameters.FileIdFirst = idArray.GetValue(0).AsString()?.Value.ToArray();
            parameters.FileIdSecond = idArray.GetValue(1).AsString()?.Value.ToArray();
        }

        _document.Decryptor = PdfDecryptorFactory.Create(parameters, _document.OnPasswordRequested);
    }
}
