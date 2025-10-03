using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PdfReader.Models;
using CommunityToolkit.HighPerformance;
using PdfReader.Streams;
using System.Linq;

namespace PdfReader
{
    public static class PdfStreamDecoder
    {
        public static ReadOnlyMemory<byte> DecodeContentStream(PdfObject obj)
        {
            List<string> filters = GetFilters(obj);

            var result = DecodeStream(obj.StreamData, filters);
            
            return result;
        }

        public static Stream DecodeContentAsStream(PdfObject obj)
        {
            List<string> filters = GetFilters(obj);

            var result = DecodeAsStream(obj.StreamData, filters);

            return result;
        }

        private static ReadOnlyMemory<byte> DecodeStream(ReadOnlyMemory<byte> streamData, List<string> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return streamData;
            }

            // Per ISO 32000-1, image-specific filters (DCTDecode, JPXDecode, JBIG2Decode, CCITTFaxDecode) are not to be decoded here and can't
            // be chained with other filters. If any such filter is present, return the original data.
            if (filters.Any(IsImageFilter))
            {
                return streamData;
            }

            using (var stream = DecodeAsStream(streamData, filters))
            {
                if (stream == null)
                {
                    return streamData;
                }

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        private static Stream DecodeAsStream(ReadOnlyMemory<byte> streamData, List<string> filters)
        {
            Stream current = streamData.AsStream();

            if (filters == null || filters.Count == 0)
            {
                return current;
            }

            // Per ISO 32000-1, image-specific filters (DCTDecode, JPXDecode, JBIG2Decode, CCITTFaxDecode) are not to be decoded here and can't
            // be chained with other filters. If any such filter is present, return the original data.
            if (filters.Any(IsImageFilter))
            {
                return streamData.AsStream();
            }

            foreach (var filter in filters)
            {
                switch (filter)
                {
                    // Image-specific encodings: handled by image decoders; stop chaining here
                    case PdfTokens.DCTDecode:
                    case PdfTokens.JPXDecode:
                    case PdfTokens.JBIG2Decode:
                    case PdfTokens.CCITTFaxDecode:
                        Console.Error.WriteLine($"PdfStreamDecoder: encountered image-specific filter '{filter}'. Leaving remaining filters to the image decoder.");
                        return current;

                    // Implemented decoders
                    case PdfTokens.FlateDecode:
                        current = DecompressFlateData(current);
                        break;

                    case PdfTokens.ASCIIHexDecode:
                        current = new AsciiHexDecodeStream(current, leaveOpen: false);
                        break;

                    // Not implemented (yet)
                    case PdfTokens.ASCII85Decode:
                        Console.Error.WriteLine("PdfStreamDecoder: TODO implement ASCII85Decode; returning partially decoded stream.");
                        return current;

                    case PdfTokens.LZWDecode:
                        Console.Error.WriteLine("PdfStreamDecoder: TODO implement LZWDecode; returning partially decoded stream.");
                        return current;

                    case PdfTokens.RunLengthDecode:
                        Console.Error.WriteLine("PdfStreamDecoder: TODO implement RunLengthDecode; returning partially decoded stream.");
                        return current;

                    case PdfTokens.Crypt:
                        Console.Error.WriteLine("PdfStreamDecoder: TODO implement Crypt filter; returning partially decoded stream.");
                        return current;

                    default:
                        Console.Error.WriteLine($"PdfStreamDecoder: unknown filter '{filter}'; returning partially decoded stream.");
                        return current;
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

            var filterValue = obj.Dictionary.GetArray(PdfTokens.FilterKey);
            if (filterValue != null)
            {
                foreach (var filter in filterValue)
                {
                    var filterName = filter.AsName();
                    if (filterName != null)
                    {
                        filters.Add(filterName);
                    }
                }
            }
            else
            {
                var singleFilter = obj.Dictionary.GetName(PdfTokens.FilterKey);
                if (singleFilter != null)
                {
                    filters.Add(singleFilter);
                }
            }

            return filters;
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
                        Console.Error.WriteLine("PdfStreamDecoder: FlateDecode: not enough data for zlib header; returning original stream.");
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
                        Console.Error.WriteLine("PdfStreamDecoder: FlateDecode: not enough data for zlib header; returning original stream.");
                        return compressed;
                    }
                }

                return new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            }
            catch
            {
                return compressed;
            }
        }
    }
}