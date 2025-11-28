using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfReader.Models;
using Microsoft.Extensions.Logging;
using PdfReader.Text;

namespace PdfReader.Streams
{
    /// <summary>
    /// Decodes generic PDF streams by applying /Filter chains and optional /DecodeParms predictor post-processing.
    /// Image specific formats (DCT, JPX, JBIG2, CCITT) are left undecoded for specialized handlers.
    /// </summary>
    public sealed class PdfStreamDecoder
    {
        private readonly ILogger<PdfStreamDecoder> _logger;

        public PdfStreamDecoder(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PdfStreamDecoder>();
        }

        /// <summary>
        /// Decode the full stream into memory (filters + predictor) and return the resulting bytes.
        /// Note: should only be used for smaller streams that can fit into memory comfortably.
        /// </summary>
        public ReadOnlyMemory<byte> DecodeContentStream(PdfObject obj)
        {
            var filters = GetFilters(obj);
            var rawStream = obj.GetRawStream();
            var decodeParameters = GetDecodeParms(obj);
            using Stream final = DecodeAsStream(rawStream, filters, decodeParameters);

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
            return DecodeAsStream(obj.GetRawStream(), filters, decodeParameters);
        }

        private Stream DecodeAsStream(Stream current, List<PdfFilterType> filters, List<PdfDictionary> decodeParameters)
        {
            for (int filterIndex = 0; filterIndex < filters.Count; filterIndex++)
            {
                PdfFilterType filter = filters[filterIndex];

                switch (filter)
                {
                    case PdfFilterType.Unknown:
                    {
                        // no filter.
                        break;
                    }
                    case PdfFilterType.DCTDecode:
                    case PdfFilterType.JPXDecode:
                    case PdfFilterType.JBIG2Decode:
                    case PdfFilterType.CCITTFaxDecode:
                    {
                        return current;
                    }
                    case PdfFilterType.FlateDecode:
                    {
                        current = DecompressFlateData(current);
                        break;
                    }
                    case PdfFilterType.ASCIIHexDecode:
                    {
                        current = new AsciiHexDecodeStream(current, leaveOpen: false);
                        break;
                    }
                    case PdfFilterType.ASCII85Decode:
                    {
                        current = new Ascii85DecodeStream(current, leaveOpen: false);
                        break;
                    }
                    case PdfFilterType.LZWDecode:
                    {
                        var parametersForLzwFilter = GetDecodeParmsForIndex(filterIndex, decodeParameters);
                        bool earlyChange = parametersForLzwFilter?.GetInteger(PdfTokens.EarlyChangeKey) != 0;
                        current = new LzwDecodeStream(current, leaveOpen: false, earlyChange: earlyChange);
                        break;
                    }
                    case PdfFilterType.RunLengthDecode:
                    {
                        current = new RunLengthDecodeStream(current, leaveOpen: false);
                        break;
                    }
                    case PdfFilterType.Crypt:
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
                if (parametersForFilter != null && (filter == PdfFilterType.FlateDecode || filter == PdfFilterType.LZWDecode))
                {
                    current = ApplyPredictorIfNeeded(current, parametersForFilter);
                }
            }

            return current;
        }

        /// <summary>
        /// Expands filter array.
        /// </summary>
        /// <param name="obj">Source object.</param>
        /// <returns>Collection of filters.</returns>
        public static List<PdfFilterType> GetFilters(PdfObject obj)
        {
            var filters = new List<PdfFilterType>();
            if (obj == null)
            {
                return filters;
            }

            var filterArray = obj.Dictionary.GetArray(PdfTokens.FilterKey);
            if (filterArray != null)
            {
                for (int index = 0; index < filterArray.Count; index++)
                {
                    var filterType = filterArray.GetName(index).AsEnum<PdfFilterType>();
                    filters.Add(filterType);
                }
            }
            else
            {
                var filterType = obj.Dictionary.GetName(PdfTokens.FilterKey).AsEnum<PdfFilterType>();
                filters.Add(filterType);
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
                    list.Add(dict);
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

            int predictor = decodeParameterDictionary.GetInteger(PdfTokens.PredictorKey) ?? 1;
            if (predictor <= 1)
            {
                return decoded;
            }
            if (predictor != 2 && (predictor < 10 || predictor > 15))
            {
                _logger.LogWarning("PdfStreamDecoder: unsupported predictor {Predictor}; skipping predictor stage.", predictor);
                return decoded; // Unsupported predictor variant.
            }

            int colors = decodeParameterDictionary.GetInteger(PdfTokens.ColorsKey) ?? 1;
            int bitsPerComponent = decodeParameterDictionary.GetInteger(PdfTokens.BitsPerComponentKey) ?? 8;
            int columns = decodeParameterDictionary.GetInteger(PdfTokens.ColumnsKey) ?? 1;

            if (columns <= 0)
            {
                _logger.LogWarning("PdfStreamDecoder: predictor specified without valid /Columns; skipping predictor stage.");
                return decoded; // Cannot proceed without a positive column count.
            }

            return new PredictorDecodeStream(decoded, predictor, colors, bitsPerComponent, columns, leaveOpen: false);
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