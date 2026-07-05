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
        parameters.SourceDictionary = encryptDict;
        parameters.V = encryptDict.GetIntegerOrDefault(PdfTokens.VKey);
        parameters.R = encryptDict.GetIntegerOrDefault(PdfTokens.RKey);
        parameters.LengthBits = encryptDict.GetIntegerOrDefault(PdfTokens.LengthKey);
        parameters.Permissions = encryptDict.GetIntegerOrDefault(PdfTokens.PKey);

        bool? encryptMetadata = encryptDict.GetBoolean(PdfTokens.EncryptMetadataKey);
        if (encryptMetadata.HasValue)
        {
            parameters.EncryptMetadata = encryptMetadata.Value;
        }

        parameters.OwnerEntry = encryptDict.GetValue(PdfTokens.OKey).AsStringBytes().ToArray();
        parameters.UserEntry = encryptDict.GetValue(PdfTokens.UKey).AsStringBytes().ToArray();

        if (parameters.R >= 5)
        {
            parameters.OwnerEncryptedKey = encryptDict.GetValue(PdfTokens.OEKey).AsStringBytes().ToArray();
            parameters.UserEncryptedKey = encryptDict.GetValue(PdfTokens.UEKey).AsStringBytes().ToArray();
            parameters.Perms = encryptDict.GetValue(PdfTokens.PermsKey).AsStringBytes().ToArray();
        }

        if (parameters.V >= 4)
        {
            parameters.StreamCryptFilterName = encryptDict.GetName(PdfTokens.StmFKey);
            parameters.StringCryptFilterName = encryptDict.GetName(PdfTokens.StrFKey);
            parameters.EmbeddedFileCryptFilterName = encryptDict.GetName(PdfTokens.EffKey);
            parameters.CryptFilterDictionary = encryptDict.GetDictionary(PdfTokens.CFKey);

            if (parameters.CryptFilterDictionary != null)
            {
                PdfDictionary? streamCfEntry = parameters.CryptFilterDictionary.GetDictionary(parameters.StreamCryptFilterName);
                if (streamCfEntry != null)
                {
                    parameters.StreamCryptFilterMethod = streamCfEntry.GetName(PdfTokens.CfmKey);
                    parameters.StreamCryptFilterLength = streamCfEntry.GetInteger(PdfTokens.LengthKey);
                }

                PdfDictionary? stringCfEntry = parameters.CryptFilterDictionary.GetDictionary(parameters.StringCryptFilterName);
                if (stringCfEntry != null)
                {
                    parameters.StringCryptFilterMethod = stringCfEntry.GetName(PdfTokens.CfmKey);
                    parameters.StringCryptFilterLength = stringCfEntry.GetInteger(PdfTokens.LengthKey);
                }
            }
        }

        PdfArray? idArray = trailer.GetArray(PdfTokens.IdKey);
        if (idArray?.Count >= 2)
        {
            parameters.FileIdFirst = idArray.GetValue(0).AsStringBytes().ToArray();
            parameters.FileIdSecond = idArray.GetValue(1).AsStringBytes().ToArray();
        }

        _document.Decryptor = PdfDecryptorFactory.Create(parameters);
    }
}
