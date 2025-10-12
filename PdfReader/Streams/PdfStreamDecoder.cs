using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfReader.Models;
using CommunityToolkit.HighPerformance;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace PdfReader.Streams
{
    /// <summary>
    /// Decodes generic PDF streams by applying /Filter chains and optional /DecodeParms predictor post-processing.
    /// Image specific formats (DCT, JPX, JBIG2, CCITT) are left undecoded for specialized handlers.
    /// </summary>
    public sealed class PdfStreamDecoder
    {
        private readonly PdfDocument _document;
        private readonly ILogger<PdfStreamDecoder> _logger;

        public PdfStreamDecoder(PdfDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _logger = document.LoggerFactory.CreateLogger<PdfStreamDecoder>();
        }

        /// <summary>
        /// Decode the full stream into memory (filters + predictor) and return the resulting bytes.
        /// Note: should only be used for smaller streams that can fit into memory comfortably.
        /// </summary>
        public ReadOnlyMemory<byte> DecodeContentStream(PdfObject obj)
        {
            var filters = GetFilters(obj);
            var decodeParameters = GetDecodeParms(obj);
            using Stream final = DecodeAsStream(obj.StreamData, filters, decodeParameters);

            if (final == null)
            {
                return obj.StreamData;
            }

            using var memoryStream = new MemoryStream();
            final.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        /// <summary>
        /// Decode the stream and return a readable Stream (caller disposes).
        /// </summary>
        public Stream DecodeContentAsStream(PdfObject obj)
        {
            var filters = GetFilters(obj);
            var decodeParameters = GetDecodeParms(obj);
            return DecodeAsStream(obj.StreamData, filters, decodeParameters);
        }

        private Stream DecodeAsStream(ReadOnlyMemory<byte> streamData, List<string> filters, List<PdfDictionary> decodeParameters)
        {
            Stream current = streamData.AsStream();

            if (filters == null || filters.Count == 0)
            {
                return ApplyPredictorIfNeeded(current, null); // May still have predictor parameters.
            }

            if (filters.Any(IsImageFilter))
            {
                return current;
            }

            for (int filterIndex = 0; filterIndex < filters.Count; filterIndex++)
            {
                string filter = filters[filterIndex];
                switch (filter)
                {
                    case PdfTokens.DCTDecode:
                    case PdfTokens.JPXDecode:
                    case PdfTokens.JBIG2Decode:
                    case PdfTokens.CCITTFaxDecode:
                    {
                        return current;
                    }
                    case PdfTokens.FlateDecode:
                    {
                        current = DecompressFlateData(current);
                        break;
                    }
                    case PdfTokens.ASCIIHexDecode:
                    {
                        current = new AsciiHexDecodeStream(current, leaveOpen: false);
                        break;
                    }
                    case PdfTokens.ASCII85Decode:
                    {
                        _logger.LogWarning("PdfStreamDecoder: TODO implement ASCII85Decode; stopping further filter decoding.");
                        return current; // TODO implement ASCII85Decode; stopping further filter decoding.
                    }
                    case PdfTokens.LZWDecode:
                    {
                        _logger.LogWarning("PdfStreamDecoder: TODO implement LZWDecode; stopping further filter decoding (predictor may be pending).");
                        return current; // TODO implement LZWDecode; stopping further filter decoding (predictor may be pending).
                    }
                    case PdfTokens.RunLengthDecode:
                    {
                        _logger.LogWarning("PdfStreamDecoder: TODO implement RunLengthDecode; stopping further filter decoding.");
                        return current; // TODO implement RunLengthDecode; stopping further filter decoding.
                    }
                    case PdfTokens.Crypt:
                    {
                        _logger.LogWarning("PdfStreamDecoder: TODO implement Crypt filter integration; returning partially decoded stream.");
                        return current; // TODO implement Crypt filter integration; returning partially decoded stream.
                    }
                    default:
                    {
                        _logger.LogWarning("PdfStreamDecoder: unknown filter '{FilterName}'; returning partially decoded stream.", filter);
                        return current; // Unknown filter – return partially decoded stream.
                    }
                }

                var parametersForFilter = GetDecodeParmsForIndex(filterIndex, decodeParameters);
                if (parametersForFilter != null && (filter == PdfTokens.FlateDecode || filter == PdfTokens.LZWDecode))
                {
                    current = ApplyPredictorIfNeeded(current, parametersForFilter);
                }
            }

            return current;
        }

        private bool IsImageFilter(string filter)
        {
            return filter == PdfTokens.DCTDecode ||
                   filter == PdfTokens.JPXDecode ||
                   filter == PdfTokens.JBIG2Decode ||
                   filter == PdfTokens.CCITTFaxDecode;
        }

        private List<string> GetFilters(PdfObject obj)
        {
            var filters = new List<string>();
            if (obj == null || obj.Dictionary == null)
            {
                return filters;
            }
            var filterArray = obj.Dictionary.GetArray(PdfTokens.FilterKey);
            if (filterArray != null)
            {
                for (int index = 0; index < filterArray.Count; index++)
                {
                    var name = filterArray.GetName(index);
                    if (!string.IsNullOrEmpty(name))
                    {
                        filters.Add(name);
                    }
                }
            }
            else
            {
                var single = obj.Dictionary.GetName(PdfTokens.FilterKey);
                if (!string.IsNullOrEmpty(single))
                {
                    filters.Add(single);
                }
            }
            return filters;
        }

        private List<PdfDictionary> GetDecodeParms(PdfObject obj)
        {
            var list = new List<PdfDictionary>();
            if (obj == null || obj.Dictionary == null)
            {
                return list;
            }
            var parmsArray = obj.Dictionary.GetArray(PdfTokens.DecodeParmsKey);
            if (parmsArray != null)
            {
                for (int index = 0; index < parmsArray.Count; index++)
                {
                    var dict = parmsArray.GetDictionary(index);
                    if (dict != null)
                    {
                        list.Add(dict);
                    }
                }
            }
            else
            {
                var single = obj.Dictionary.GetDictionary(PdfTokens.DecodeParmsKey);
                if (single != null)
                {
                    list.Add(single);
                }
            }
            return list;
        }

        private PdfDictionary GetDecodeParmsForIndex(int filterIndex, List<PdfDictionary> decodeParameters)
        {
            if (decodeParameters == null || decodeParameters.Count == 0)
            {
                return null;
            }
            if (decodeParameters.Count == 1)
            {
                return decodeParameters[0];
            }
            if (filterIndex >= 0 && filterIndex < decodeParameters.Count)
            {
                return decodeParameters[filterIndex];
            }
            return null;
        }

        private Stream ApplyPredictorIfNeeded(Stream decoded, PdfDictionary decodeParameterDictionary)
        {
            if (decodeParameterDictionary == null)
            {
                return decoded;
            }

            int predictor = decodeParameterDictionary.GetInt(PdfTokens.PredictorKey) ?? 1;
            if (predictor <= 1)
            {
                return decoded;
            }
            if (predictor != 2 && (predictor < 10 || predictor > 15))
            {
                _logger.LogWarning("PdfStreamDecoder: unsupported predictor {Predictor}; skipping predictor stage.", predictor);
                return decoded; // Unsupported predictor variant.
            }

            int colors = decodeParameterDictionary.GetInt(PdfTokens.ColorsKey) ?? 1;
            int bitsPerComponent = decodeParameterDictionary.GetInt(PdfTokens.BitsPerComponentKey) ?? 8;
            int columns = decodeParameterDictionary.GetInt(PdfTokens.ColumnsKey) ?? 1;

            if (columns <= 0)
            {
                _logger.LogWarning("PdfStreamDecoder: predictor specified without valid /Columns; skipping predictor stage.");
                return decoded; // Cannot proceed without a positive column count.
            }

            return new PredictorDecodeStream(decoded, predictor, colors, bitsPerComponent, columns);
        }

        private Stream DecompressFlateData(Stream compressed)
        {
            if (compressed == null)
            {
                return Stream.Null;
            }
            try
            {
                if (compressed.CanSeek)
                {
                    if (compressed.Length - compressed.Position < 2)
                    {
                        _logger.LogWarning("PdfStreamDecoder: FlateDecode: insufficient data for zlib header; returning original stream.");
                        return compressed;
                    }
                    compressed.ReadByte();
                    compressed.ReadByte();
                }
                else
                {
                    byte[] headerBytes = new byte[2];
                    int readCount = compressed.Read(headerBytes, 0, 2);
                    if (readCount < 2)
                    {
                        _logger.LogWarning("PdfStreamDecoder: FlateDecode: insufficient data for zlib header (non-seekable); returning original stream.");
                        return compressed;
                    }
                }
                return new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PdfStreamDecoder: FlateDecode: exception during decompression; returning original stream.");
                return compressed;
            }
        }
    }
}