using System;
using Microsoft.Extensions.Logging;
using PdfReader.Encryption;
using PdfReader.Fonts;
using PdfReader.Models;

namespace PdfReader.Parsing
{
    /// <summary>
    /// Handles parsing of PDF objects and object streams.
    /// Performs a single forward scan that can also detect trailer dictionaries to set the document RootRef when present.
    /// </summary>
    public class PdfObjectParser
    {
        private readonly PdfDocument _document;
        private readonly PdfObjectStreamParser _objectStreamParser;
        private readonly PdfTrailerParser _pdfTrailerParser;
        private readonly ILogger<PdfObjectParser> _logger;

        public PdfObjectParser(PdfDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _document = document;
            _objectStreamParser = new PdfObjectStreamParser(document);
            _pdfTrailerParser = new PdfTrailerParser(document);
            _logger = document.LoggerFactory.CreateLogger<PdfObjectParser>();
        }

        /// <summary>
        /// Parse all objects in the PDF document (single pass) and extract objects from any object streams.
        /// Also detects trailer dictionaries (by keyword 'trailer') and sets RootRef if not already established.
        /// </summary>
        /// <param name="context">Parse context wrapping the full PDF bytes.</param>
        /// <param name="password">Password for password protected documents. Can be null.</param>
        public void ParseObjects(ref PdfParseContext context, string password)
        {
            context.Position = 0;

            while (context.Position < context.Length - PdfTokens.MinBufferLengthForObjectParsing)
            {
                // Attempt to parse an object header: "number generation obj"
                if (PdfParsers.TryParseObjectHeader(ref context, out int objectNumber, out int generation))
                {
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);

                    // Parse the object value (could be any PDF value; dictionary sets obj.Dictionary internally)
                    var value = PdfParsers.ParsePdfValue(ref context, _document, allowReferences: true);
                    var pdfObject = new PdfObject(new PdfReference(objectNumber, generation), _document, value);

                    // If a dictionary precedes a stream keyword, parse the stream data.
                    PdfParsingHelpers.SkipWhitespaceAndComment(ref context);
                    if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Stream))
                    {
                        pdfObject.StreamData = PdfParsers.ParseStream(ref context, pdfObject.Dictionary);
                    }

                    if (value != null)
                    {
                        _document.Objects[pdfObject.Reference] = pdfObject;
                    }

                    // Advance to endobj (tolerant scan)
                    while (context.Position < context.Length - PdfTokens.MinBufferLengthForEndObj)
                    {
                        if (PdfParsingHelpers.MatchSequence(ref context, PdfTokens.Endobj))
                        {
                            break;
                        }
                        context.Position++;
                    }
                }
                else if (_pdfTrailerParser.TryParseTrailerDictionary(ref context))
                {
                    // Advance by one byte and continue scanning.
                    context.Position++;
                }
                else
                {
                    context.Position++;
                }
            }

            _pdfTrailerParser.FinalizeTrailer();
            DecryptObjects(password);

            // Extract objects that are embedded in object streams (ObjStm)
            _objectStreamParser.ParseObjectStreams();

            UpdateRoot();
        }

        private void UpdateRoot()
        {
            if (_document.TrailerDictionary != null)
            {
                var rootObj = _document.TrailerDictionary.GetPageObject(PdfTokens.RootKey);

                if (rootObj != null)
                {
                    _document.RootRef = rootObj.Reference;
                    return;
                }
            }

            foreach (var pdfObject in _document.Objects.Values)
            {
                string typeName = pdfObject.Dictionary.GetName(PdfTokens.TypeKey);
                if (typeName == PdfTokens.CatalogKey)
                {
                    // Direct /Catalog object discovered.
                    _document.RootRef = pdfObject.Reference;
                    return;
                }
                else if (typeName == PdfTokens.XRefKey)
                {
                    // XRef stream dictionaries (PDF 1.5+) are trailer dictionaries in stream form.
                    // They may contain /Root just like a classic trailer. Resolve its reference.
                    var rootObject = pdfObject.Dictionary.GetPageObject(PdfTokens.RootKey);
                    if (rootObject != null)
                    {
                        _document.RootRef = rootObject.Reference;
                        return;
                    }
                }
                else
                {
                    // Rare recovery case: if this dictionary itself exposes a /Root key (non-standard) we may accept it.
                    var fallbackRoot = pdfObject.Dictionary.GetPageObject(PdfTokens.RootKey);
                    if (fallbackRoot != null)
                    {
                        _document.RootRef = fallbackRoot.Reference;
                        return;
                    }
                }
            }

            _logger.LogWarning("Root object is not discovered.");
        }

        private void DecryptObjects(string password)
        {
            if (_document.Decryptor == null)
            {
                return;
            }

            _document.Decryptor.UpdatePassword(password);

            foreach (var obj in _document.Objects.Values)
            {
                DecryptIPdfValues(obj.Value, obj, _document.Decryptor);

                if (!obj.StreamData.IsEmpty)
                {
                    obj.StreamData = _document.Decryptor.DecryptBytes(obj.StreamData, obj.Reference);
                }
            }
        }

        private void DecryptIPdfValues(IPdfValue value, PdfObject pdfObject, BasePdfDecryptor decryptor)
        {
            if (value is PdfDictionary dictionary)
            {
                foreach (var dictionaryValue in dictionary.RawValues.Values)
                {
                    DecryptIPdfValues(dictionaryValue, pdfObject, decryptor);
                }
            }
            else if (value is PdfArray array)
            {
                foreach (var arrayValue in array.RawValues)
                {
                    DecryptIPdfValues(arrayValue, pdfObject, decryptor);
                }
            }
            else if (value is IPdfValue<string> textValue)
            {
                if (value.Type == PdfValueType.String || value.Type == PdfValueType.HexString)
                {
                    byte[] bytes = EncodingExtensions.PdfDefault.GetBytes(textValue.Value);
                    var decrypted = decryptor.DecryptBytes(bytes, pdfObject.Reference);
                    var decryptedText = EncodingExtensions.PdfDefault.GetString(decrypted);
                    textValue.UpdateValue(decryptedText);
                }
            }
        }
    }
}