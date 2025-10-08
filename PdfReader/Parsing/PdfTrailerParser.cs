using System;
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

        public PdfTrailerParser(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public bool TryParseTrailerDictionary(ref PdfParseContext context)
        {
            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

            if (!PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Trailer))
            {
                return false;
            }

            PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

            byte first = PdfParsingHelpers.PeekByte(ref context);
            byte second = PdfParsingHelpers.PeekByte(ref context, 1);
            if (first != PdfTokens.LeftAngle || second != PdfTokens.LeftAngle)
            {
                return true;
            }

            if (_document.TrailerDictionary != null)
            {
                return true;
            }

            _document.TrailerDictionary = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true).AsDictionary();
            _document.RootObject = _document.TrailerDictionary?.GetPageObject(PdfTokens.RootKey);
            return true;
        }

        public void FinalizeTrailer()
        {
            var dict = _document.TrailerDictionary;
            if (dict == null)
            {
                return;
            }

            if (_document.Decryptor != null)
            {
                return; // Already set (e.g., from /Encrypt in an object stream)
            }

            // Only create decryptor if /Encrypt present
            var encryptDict = dict.GetDictionary(PdfTokens.EncryptKey);
            if (encryptDict == null)
            {
                return; // Not encrypted; leave Decryptor null
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

            // Optional legacy /O and /U entries
            parameters.OwnerEntry = encryptDict.GetValue("/O").AsHexBytes();
            parameters.UserEntry = encryptDict.GetValue("/U").AsHexBytes();

            // R>=5 entries ( /OE /UE /Perms )
            if (parameters.R >= 5)
            {
                parameters.OwnerEncryptedKey = encryptDict.GetValue("/OE").AsHexBytes();
                parameters.UserEncryptedKey = encryptDict.GetValue("/UE").AsHexBytes();
                parameters.Perms = encryptDict.GetValue("/Perms").AsHexBytes();
            }

            // Crypt filter related (V=4/5) entries
            if (parameters.V >= 4)
            {
                parameters.StreamCryptFilterName = encryptDict.GetName("/StmF");
                parameters.StringCryptFilterName = encryptDict.GetName("/StrF");
                parameters.EmbeddedFileCryptFilterName = encryptDict.GetName("/EFF");
                parameters.CryptFilterDictionary = encryptDict.GetDictionary("/CF");
            }

            var idArray = dict.GetArray(PdfTokens.IdKey);
            if (idArray != null && idArray.Count >= 2)
            {
                parameters.FileIdFirst = idArray.GetValue(0).AsHexBytes();
                parameters.FileIdSecond = idArray.GetValue(1).AsHexBytes();
            }

            _document.Decryptor = PdfDecryptorFactory.Create(parameters);
        }
    }
}
