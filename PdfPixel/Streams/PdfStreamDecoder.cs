using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PdfPixel.Streams;

/// <summary>
/// Decodes generic PDF streams by applying /Filter chains and optional /DecodeParms predictor post-processing.
/// Image specific formats (DCT, JPX, JBIG2, CCITT) are left undecoded for specialized handlers.
/// </summary>
public sealed class PdfStreamDecoder
{
    private const int DecodeChunkSize = 2048;

    private readonly ILogger<PdfStreamDecoder> _logger;

    /// <summary>
    /// Initializes a new <see cref="PdfStreamDecoder"/> using the provided logger factory.
    /// </summary>
    public PdfStreamDecoder(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger<PdfStreamDecoder>();

    /// <summary>
    /// Decode the full stream into memory (filters + predictor) and return the resulting bytes.
    /// </summary>
    public ReadOnlyMemory<byte> DecodeContentStream(
        Stream rawStream,
        List<PdfFilterType> filters,
        List<PdfDecodeParameters?> decodeParameters,
        IPdfExecutionObserver? observer)
    {
        if (rawStream == null)
        {
            throw new ArgumentNullException(nameof(rawStream));
        }

        using Stream final = DecodeContentAsStream(rawStream, filters, decodeParameters);

        using MemoryStream memoryStream = new();
        var buffer = new byte[DecodeChunkSize];
        int read;
        while ((read = final.Read(buffer, 0, buffer.Length)) > 0)
        {
            memoryStream.Write(buffer, 0, read);
            observer?.Notify();
        }

        return memoryStream.ToArray();
    }

    /// <summary>
    /// Decode the stream and return a readable Stream (caller disposes).
    /// </summary>
    public Stream DecodeContentAsStream(
        Stream current,
        List<PdfFilterType> filters,
        List<PdfDecodeParameters?> decodeParameters)
    {
        if (filters == null)
        {
            throw new ArgumentNullException(nameof(filters));
        }

        if (decodeParameters == null)
        {
            throw new ArgumentNullException(nameof(decodeParameters));
        }

        for (int filterIndex = 0; filterIndex < filters.Count; filterIndex++)
        {
            PdfFilterType filter = filters[filterIndex];

            switch (filter)
            {
                case PdfFilterType.Unknown:
                {
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
                    PdfDecodeParameters? parameters = GetDecodeParmsForIndex(filterIndex, decodeParameters);
                    bool earlyChange = parameters?.EarlyChange != 0;
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
                    _logger.LogWarning("Crypt filter is not supported, returning partially decoded stream.");
                    return current; // TODO: [LOW] implement Crypt filter integration
                }
                default:
                {
                    _logger.LogWarning("unknown filter '{FilterName}'; returning partially decoded stream.", filter);
                    return current;
                }
            }

            PdfDecodeParameters? parametersForFilter = GetDecodeParmsForIndex(filterIndex, decodeParameters);
            if (parametersForFilter != null && (filter == PdfFilterType.FlateDecode || filter == PdfFilterType.LZWDecode))
            {
                current = ApplyPredictorIfNeeded(current, parametersForFilter);
            }
        }

        return current;
    }

    /// <summary>
    /// Extracts the filter chain from a stream dictionary.
    /// </summary>
    public static List<PdfFilterType> GetFilters(PdfDictionary dictionary)
    {
        if (dictionary == null)
        {
            throw new ArgumentNullException(nameof(dictionary));
        }

        return GetFilters(dictionary.GetValue(PdfTokens.FilterKey));
    }

    /// <summary>
    /// Expands a <c>/Filter</c> value (a single name or an array of names) into the ordered filter chain.
    /// </summary>
    public static List<PdfFilterType> GetFilters(IPdfValue? filterValue)
    {
        List<PdfFilterType> filters = [];

        if (filterValue == null)
        {
            return filters;
        }

        if (filterValue.Type == PdfValueType.Array)
        {
            PdfArray? filterArray = filterValue.AsArray();
            if (filterArray != null)
            {
                for (int index = 0; index < filterArray.Count; index++)
                {
                    PdfFilterType filterType = filterArray.GetName(index).AsEnum<PdfFilterType>();
                    filters.Add(filterType);
                }
            }
        }
        else if (filterValue.Type == PdfValueType.Name)
        {
            PdfString filterName = filterValue.AsName();
            if (!filterName.IsEmpty)
            {
                filters.Add(filterName.AsEnum<PdfFilterType>());
            }
        }

        return filters;
    }

    /// <summary>
    /// Extracts and parses decode parameters from a stream dictionary into strongly-typed instances.
    /// </summary>
    public static List<PdfDecodeParameters?> GetDecodeParameters(PdfDictionary dictionary)
    {
        if (dictionary == null)
        {
            throw new ArgumentNullException(nameof(dictionary));
        }

        List<PdfDecodeParameters?> list = [];

        PdfArray? parmsArray = dictionary.GetArray(PdfTokens.DecodeParmsKey);
        if (parmsArray != null)
        {
            for (int index = 0; index < parmsArray.Count; index++)
            {
                PdfDictionary? dict = parmsArray.GetDictionary(index);
                list.Add(PdfDecodeParameters.FromDictionary(dict));
            }
        }
        else if (dictionary.HasKey(PdfTokens.DecodeParmsKey))
        {
            PdfDictionary? single = dictionary.GetDictionary(PdfTokens.DecodeParmsKey);
            list.Add(PdfDecodeParameters.FromDictionary(single));
        }

        return list;
    }

    private static PdfDecodeParameters? GetDecodeParmsForIndex(int filterIndex, List<PdfDecodeParameters?> decodeParameters)
    {
        if (decodeParameters.Count == 0)
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

    private Stream ApplyPredictorIfNeeded(Stream decoded, PdfDecodeParameters decodeParameters)
    {
        int predictor = decodeParameters.Predictor ?? 1;
        if (predictor <= 1)
        {
            return decoded;
        }

        if (predictor != 2 && (predictor < 10 || predictor > 15))
        {
            _logger.LogWarning("Unsupported predictor {Predictor}; skipping predictor stage.", predictor);
            return decoded;
        }

        int colors = decodeParameters.Colors ?? 1;
        int bitsPerComponent = decodeParameters.BitsPerComponent ?? 8;
        int columns = decodeParameters.Columns ?? 1;

        if (columns <= 0)
        {
            _logger.LogWarning("Predictor specified without valid /Columns; skipping predictor stage.");
            return decoded;
        }

        return new PredictorDecodeStream(decoded, predictor, colors, bitsPerComponent, columns, leaveOpen: false);
    }

    private static Stream DecompressFlateData(Stream compressed)
    {
        if (compressed == null)
        {
            return Stream.Null;
        }

        if (compressed.CanSeek)
        {
            if (compressed.Length - compressed.Position < 2)
            {
                throw new InvalidDataException("FlateDecode: insufficient data for zlib header.");
            }

            compressed.ReadByte();
            compressed.ReadByte();
        }
        else
        {
            var headerBytes = new byte[2];
            int readCount = compressed.Read(headerBytes, 0, 2);
            if (readCount < 2)
            {
                throw new InvalidDataException("FlateDecode: insufficient data for zlib header (non-seekable).");
            }
        }

        return new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
    }
}
