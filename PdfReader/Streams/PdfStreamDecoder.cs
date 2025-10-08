using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfReader.Models;
using CommunityToolkit.HighPerformance;
using System.Linq;

namespace PdfReader.Streams
{
    /// <summary>
    /// Decodes generic PDF streams by applying /Filter chains and optional /DecodeParms predictor post-processing.
    /// Image specific formats (DCT, JPX, JBIG2, CCITT) are left undecoded for specialized handlers.
    /// </summary>
    public static class PdfStreamDecoder
    {
        /// <summary>
        /// Decode the full stream into memory (filters + predictor) and return the resulting bytes.
        /// </summary>
        public static ReadOnlyMemory<byte> DecodeContentStream(PdfObject obj)
        {
            List<string> filters = GetFilters(obj);
            List<PdfDictionary> decodeParms = GetDecodeParms(obj);
            using (Stream final = DecodeAsStream(obj.StreamData, filters, decodeParms))
            {
                if (final == null)
                {
                    return obj.StreamData;
                }
                using (var ms = new MemoryStream())
                {
                    final.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Decode the stream and return a readable Stream (caller disposes). May buffer internally for predictor undo.
        /// </summary>
        public static Stream DecodeContentAsStream(PdfObject obj)
        {
            // TODO: use
            List<string> filters = GetFilters(obj);
            List<PdfDictionary> decodeParms = GetDecodeParms(obj);

            return DecodeAsStream(obj.StreamData, filters, decodeParms);
        }

        private static Stream DecodeAsStream(ReadOnlyMemory<byte> streamData, List<string> filters, List<PdfDictionary> decodeParms)
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
                            Console.Error.WriteLine("PdfStreamDecoder: TODO implement ASCII85Decode; stopping further filter decoding.");
                            return current;
                        }
                    case PdfTokens.LZWDecode:
                        {
                            Console.Error.WriteLine("PdfStreamDecoder: TODO implement LZWDecode; stopping further filter decoding (predictor may be pending).");
                            return current;
                        }
                    case PdfTokens.RunLengthDecode:
                        {
                            Console.Error.WriteLine("PdfStreamDecoder: TODO implement RunLengthDecode; stopping further filter decoding.");
                            return current;
                        }
                    case PdfTokens.Crypt:
                        {
                            Console.Error.WriteLine("PdfStreamDecoder: TODO implement Crypt filter integration; returning partially decoded stream.");
                            return current;
                        }
                    default:
                        {
                            Console.Error.WriteLine($"PdfStreamDecoder: unknown filter '{filter}'; returning partially decoded stream.");
                            return current; // Unknown filter – return partially decoded stream.
                        }
                }

                var parmsForFilter = GetDecodeParmsForIndex(filterIndex, decodeParms);
                if (parmsForFilter != null && (filter == PdfTokens.FlateDecode || filter == PdfTokens.LZWDecode))
                {
                    current = ApplyPredictorIfNeeded(current, parmsForFilter);
                }
            }

            return current;
        }

        private static bool IsImageFilter(string filter)
        {
            return filter == PdfTokens.DCTDecode ||
                   filter == PdfTokens.JPXDecode ||
                   filter == PdfTokens.JBIG2Decode ||
                   filter == PdfTokens.CCITTFaxDecode;
        }

        private static List<string> GetFilters(PdfObject obj)
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

        private static List<PdfDictionary> GetDecodeParms(PdfObject obj)
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

        private static PdfDictionary GetDecodeParmsForIndex(int filterIndex, List<PdfDictionary> decodeParms)
        {
            if (decodeParms == null || decodeParms.Count == 0)
            {
                return null;
            }
            if (decodeParms.Count == 1)
            {
                return decodeParms[0];
            }
            if (filterIndex >= 0 && filterIndex < decodeParms.Count)
            {
                return decodeParms[filterIndex];
            }
            return null;
        }

        private static Stream ApplyPredictorIfNeeded(Stream decoded, PdfDictionary decodeParmDict)
        {
            if (decodeParmDict == null)
            {
                return decoded;
            }

            int predictor = decodeParmDict.GetInt(PdfTokens.PredictorKey) ?? 1;
            if (predictor <= 1)
            {
                return decoded;
            }
            if (predictor != 2 && (predictor < 10 || predictor > 15))
            {
                Console.Error.WriteLine($"PdfStreamDecoder: unsupported predictor {predictor}; skipping predictor stage.");
                return decoded; // Unsupported predictor variant.
            }

            int colors = decodeParmDict.GetInt(PdfTokens.ColorsKey) ?? 1;
            int bitsPerComponent = decodeParmDict.GetInt(PdfTokens.BitsPerComponentKey) ?? 8;
            int columns = decodeParmDict.GetInt(PdfTokens.ColumnsKey) ?? 1;

            if (columns <= 0)
            {
                Console.Error.WriteLine("PdfStreamDecoder: predictor specified without valid /Columns; skipping predictor stage.");
                return decoded; // Cannot proceed without a positive column count.
            }
            if (bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                Console.Error.WriteLine($"PdfStreamDecoder: predictor bitsPerComponent={bitsPerComponent} unsupported; skipping predictor stage.");
                return decoded; // Current implementation supports only byte-aligned sample sizes.
            }
            return new PredictorDecodeStream(decoded, predictor, colors, bitsPerComponent, columns);
        }

        private static Stream DecompressFlateData(Stream compressed)
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
                        Console.Error.WriteLine("PdfStreamDecoder: FlateDecode: insufficient data for zlib header; returning original stream.");
                        return compressed;
                    }
                    compressed.ReadByte();
                    compressed.ReadByte();
                }
                else
                {
                    byte[] hdr = new byte[2];
                    int read = compressed.Read(hdr, 0, 2);
                    if (read < 2)
                    {
                        Console.Error.WriteLine("PdfStreamDecoder: FlateDecode: insufficient data for zlib header (non-seekable); returning original stream.");
                        return compressed;
                    }
                }
                return new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            }
            catch
            {
                Console.Error.WriteLine("PdfStreamDecoder: FlateDecode: exception during decompression; returning original stream.");
                return compressed;
            }
        }
    }
}