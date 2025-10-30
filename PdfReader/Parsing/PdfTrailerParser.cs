using System;
using Microsoft.Extensions.Logging;
using PdfReader.Encryption;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Parses the trailer dictionary during a linear scan capturing minimal encryption related fields.
    /// </summary>
    internal sealed class PdfTrailerParser
    {
        private readonly PdfDocument _document;
        private readonly ILogger<PdfTrailerParser> _logger;

        public PdfTrailerParser(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = document.LoggerFactory.CreateLogger<PdfTrailerParser>();
        }

        public bool TryParseTrailerDictionary(ref PdfParseContext context, out PdfDictionary trailer)
        {
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

            if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Trailer))
            {
                trailer = null;
                return false;
            }

            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

            byte first = PdfParsingHelpers.PeekByte(ref context);
            byte second = PdfParsingHelpers.PeekByte(ref context, 1);
            if (first != PdfTokens.LeftAngle || second != PdfTokens.LeftAngle)
            {
                trailer = null;
                return true;
            }

            trailer = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true).AsDictionary();

            if (trailer == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Return the /Prev offset from the current trailer dictionary.
        /// </summary>
        public int? GetPrevOffset(PdfDictionary trailer)
        {
            if (trailer == null)
            {
                return null;
            }

            var prev = trailer.GetInt(PdfTokens.PrevKey);
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

            var encryptDict = trailer.GetDictionary(PdfTokens.EncryptKey);
            if (encryptDict == null)
            {
                return; // Not encrypted
            }

            var parameters = new PdfDecryptorParameters();
            parameters.SourceDictionary = encryptDict;
            parameters.V = encryptDict.GetIntegerOrDefault(PdfTokens.VKey);
            parameters.R = encryptDict.GetIntegerOrDefault(PdfTokens.RKey);
            parameters.LengthBits = encryptDict.GetIntegerOrDefault(PdfTokens.LengthKey);
            parameters.Permissions = encryptDict.GetIntegerOrDefault(PdfTokens.PKey);

            var encryptMetadata = encryptDict.GetBool(PdfTokens.EncryptMetadataKey);
            if (encryptMetadata.HasValue)
            {
                parameters.EncryptMetadata = encryptMetadata.Value;
            }

            // Use PdfTokens constants instead of string literals.
            parameters.OwnerEntry = encryptDict.GetValue(PdfTokens.OKey).AsStringBytes();
            parameters.UserEntry = encryptDict.GetValue(PdfTokens.UKey).AsStringBytes();

            if (parameters.R >= 5)
            {
                parameters.OwnerEncryptedKey = encryptDict.GetValue(PdfTokens.OEKey).AsStringBytes();
                parameters.UserEncryptedKey = encryptDict.GetValue(PdfTokens.UEKey).AsStringBytes();
                parameters.Perms = encryptDict.GetValue(PdfTokens.PermsKey).AsStringBytes();
            }

            if (parameters.V >= 4)
            {
                parameters.StreamCryptFilterName = encryptDict.GetName(PdfTokens.StmFKey);
                parameters.StringCryptFilterName = encryptDict.GetName(PdfTokens.StrFKey);
                parameters.EmbeddedFileCryptFilterName = encryptDict.GetName(PdfTokens.EffKey);
                parameters.CryptFilterDictionary = encryptDict.GetDictionary(PdfTokens.CFKey);
            }

            var idArray = trailer.GetArray(PdfTokens.IdKey);
            if (idArray != null && idArray.Count >= 2)
            {
                parameters.FileIdFirst = idArray.GetValue(0).AsStringBytes();
                parameters.FileIdSecond = idArray.GetValue(1).AsStringBytes();
            }

            _document.Decryptor = PdfDecryptorFactory.Create(parameters);
            if (_document.Decryptor == null)
            {
                _logger.LogWarning("PdfTrailerParser: Failed to create decryptor despite presence of /Encrypt dictionary (V={V} R={R}).", parameters.V, parameters.R);
            }
        }
    }
}
