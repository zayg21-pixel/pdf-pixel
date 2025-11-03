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

        /// <summary>
        /// Attempt to parse a trailer dictionary at the current context position using the unified PdfParser.
        /// Reads two top-level values: an operator (must be 'trailer') and an optional dictionary.
        /// Returns false if the first value is not the trailer operator. Returns true otherwise (even if dictionary missing).
        /// </summary>
        public bool TryParseTrailerDictionary(ref PdfParseContext context, out PdfDictionary trailer)
        {
            trailer = null;

            // Use unified parser; it handles whitespace/comments internally.
            var parser = new PdfParser(context, _document, allowReferences: true);

            var firstValue = parser.ReadNextValue();

            if (firstValue.AsString() != PdfTokens.Trailer)
            {
                return false; // First operator is not 'trailer'.
            }

            trailer = parser.ReadNextValue().AsDictionary();

            return true; // Trailer keyword consumed (dictionary may be null).
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

            var prev = trailer.GetInteger(PdfTokens.PrevKey);
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
            }

            var idArray = trailer.GetArray(PdfTokens.IdKey);
            if (idArray != null && idArray.Count >= 2)
            {
                parameters.FileIdFirst = idArray.GetValue(0).AsStringBytes().ToArray();
                parameters.FileIdSecond = idArray.GetValue(1).AsStringBytes().ToArray();
            }

            _document.Decryptor = PdfDecryptorFactory.Create(parameters);
            if (_document.Decryptor == null)
            {
                _logger.LogWarning("PdfTrailerParser: Failed to create decryptor despite presence of /Encrypt dictionary (V={V} R={R}).", parameters.V, parameters.R);
            }
        }
    }
}
