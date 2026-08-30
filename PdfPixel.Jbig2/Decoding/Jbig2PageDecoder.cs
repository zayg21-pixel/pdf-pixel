using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfPixel.Jbig2.Model;
using PdfPixel.Jbig2.Parsing;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Top-level JBIG2 page decoder. Processes segment headers and dispatches to appropriate
/// segment decoders to build the final page bitmap.
/// Supports globals merged with page-specific segments.
/// </summary>
public sealed class Jbig2PageDecoder
{
    private readonly Jbig2SegmentParser _segmentParser;

    /// <summary>
    /// Initializes the page decoder.
    /// </summary>
    public Jbig2PageDecoder() => _segmentParser = new Jbig2SegmentParser();

    /// <summary>
    /// Decodes a JBIG2 globals stream into a reusable <see cref="Jbig2SegmentCache"/>
    /// and passed to <see cref="Decode"/> so globals are processed only once per document.
    /// </summary>
    /// <param name="globalsData">Raw globals stream data.</param>
    /// <returns>A populated cache containing all decoded globals segments.</returns>
    public Jbig2SegmentCache DecodeGlobalCache(in ReadOnlySpan<byte> globalsData)
    {
        Jbig2SegmentCache globalsCache = new();
        List<Jbig2SegmentHeader> globalSegments = _segmentParser.ParseSegments(globalsData);
        ProcessSegments(globalSegments, globalsData, pageBitmap: null, globalsCache);
        return globalsCache;
    }

    /// <summary>
    /// Returns the page numbers the data declares, in ascending order. Segments associated with
    /// page 0 hold globals and are not listed.
    /// </summary>
    /// <param name="data">JBIG2 data, with or without a file header.</param>
    /// <returns>The page numbers present.</returns>
    public IReadOnlyList<int> GetPageNumbers(in ReadOnlySpan<byte> data)
    {
        List<Jbig2SegmentHeader> segments = _segmentParser.ParseSegments(data);
        List<int> pageNumbers = [];

        foreach (Jbig2SegmentHeader segment in segments)
        {
            if (segment.Type == Jbig2SegmentType.PageInformation && !pageNumbers.Contains(segment.PageAssociation))
            {
                pageNumbers.Add(segment.PageAssociation);
            }
        }

        pageNumbers.Sort();

        return pageNumbers;
    }

    /// <summary>
    /// Decodes a JBIG2 page from the given data.
    /// </summary>
    /// <param name="pageData">Page-specific encoded data.</param>
    /// <param name="expectedWidth">Expected page width, used when the page information segment declares none.</param>
    /// <param name="expectedHeight">Expected page height, used when the page information segment declares none.</param>
    /// <param name="globalCache">
    /// Optional pre-decoded globals cache. When provided, its
    /// symbol and pattern dictionaries are merged into the page cache before decoding.
    /// Obtain via <see cref="DecodeGlobalCache"/>.
    /// </param>
    /// <param name="observer">Observer to notify on long-running decode operations.</param>
    /// <param name="pageNumber">Page to decode, or null for the first page the data declares.</param>
    /// <returns>The decoded page bitmap, or null on failure.</returns>
    public Jbig2Bitmap Decode(
        in ReadOnlySpan<byte> pageData,
        int expectedWidth,
        int expectedHeight,
        Jbig2SegmentCache? globalCache = null,
        IJBig2ExectionObserver? observer = default,
        int? pageNumber = null)
    {
        Jbig2SegmentCache cache = new();

        if (globalCache != null)
        {
            cache.MergeFrom(globalCache);
        }

        // Parse page segments
        List<Jbig2SegmentHeader> allSegments = _segmentParser.ParseSegments(pageData);

        // Each segment names the page it belongs to, and page 0 the globals every page shares.
        int targetPage = pageNumber ?? GetFirstPageNumber(allSegments);
        List<Jbig2SegmentHeader> pageSegments = [];

        foreach (Jbig2SegmentHeader segment in allSegments)
        {
            if (segment.PageAssociation == targetPage || segment.PageAssociation == 0)
            {
                pageSegments.Add(segment);
            }
        }

        // Find page information segment to determine dimensions
        Jbig2PageInfo? pageInfo = null;
        foreach (Jbig2SegmentHeader segment in pageSegments)
        {
            if (segment.Type == Jbig2SegmentType.PageInformation)
            {
                pageInfo = ParsePageInformation(segment, pageData);
                break;
            }
        }

        int width;
        int height;

        if (pageInfo?.Width != null && pageInfo.Width >= 0 && pageInfo.Width != unchecked((int)0xFFFFFFFF))
        {
            width = pageInfo.Width;
        }
        else
        {
            width = expectedWidth;
        }

        if (pageInfo?.Height != null && pageInfo.Height >= 0 && pageInfo.Height != unchecked((int)0xFFFFFFFF))
        {
            height = pageInfo.Height;
        }
        else
        {
            height = expectedHeight;
        }

        // When page height is unknown (0xFFFFFFFF maps to -1 as int), derive it from segment data.
        // All segment data is already in memory, so we pre-scan region headers and End-of-Stripe
        // segments to find the maximum vertical extent (ITU-T T.88 Section 7.4.8).
        if (height <= 0)
        {
            height = ComputePageHeight(pageSegments, pageData);
        }

        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Invalid image dimensions");
        }

        byte defaultPixel = pageInfo?.DefaultPixelValue ?? 0;
        Jbig2Bitmap pageBitmap = new(width, height, defaultPixel);

        // Process all page segments
        ProcessSegments(pageSegments, pageData, pageBitmap, cache, observer);

        return pageBitmap;
    }

    /// <summary>
    /// Pre-scans parsed segments to determine the actual page height when the page information
    /// segment declares an unknown height (0xFFFFFFFF). Per ITU-T T.88 Section 7.4.8, pages
    /// with unknown height use striped mode and include End-of-Stripe segments whose 4-byte
    /// payload carries the last row index of each stripe.
    /// </summary>
    /// <param name="segments">Already-parsed segment headers.</param>
    /// <param name="data">Full page data for reading segment payloads.</param>
    /// <returns>Computed page height, or 0 if no End-of-Stripe segments are found.</returns>
    private static int ComputePageHeight(List<Jbig2SegmentHeader> segments, in ReadOnlySpan<byte> data)
    {
        int maxHeight = 0;

        foreach (Jbig2SegmentHeader segment in segments)
        {
            if (segment.Type != Jbig2SegmentType.EndOfStripe)
            {
                continue;
            }

            // End-of-Stripe carries a 4-byte big-endian row index (last row of the stripe).
            // The page height is endRow + 1.
            if (segment.DataLength >= 4 && segment.DataOffset + 4 <= data.Length)
            {
                int endRow = BinaryPrimitives.ReadInt32BigEndian(
                    data.Slice(segment.DataOffset, 4));
                int candidate = endRow + 1;
                if (candidate > maxHeight)
                {
                    maxHeight = candidate;
                }
            }
        }

        return maxHeight;
    }

    private void ProcessSegments(List<Jbig2SegmentHeader> segments, in ReadOnlySpan<byte> data, Jbig2Bitmap? pageBitmap, Jbig2SegmentCache cache, IJBig2ExectionObserver? observer = default)
    {
        foreach (Jbig2SegmentHeader segment in segments)
        {
            observer?.Notify();
            cache.AddSegment(segment);

            if (segment.DataLength <= 0 && segment.DataLength != -1)
            {
                continue;
            }

            ReadOnlySpan<byte> segmentData = (segment.DataLength >= 0)
                ? data.Slice(segment.DataOffset, (int)segment.DataLength)
                : data.Slice(segment.DataOffset);

            switch (segment.Type)
            {
                case Jbig2SegmentType.SymbolDictionary:
                    {
                        ProcessSymbolDictionary(segment, segmentData, cache);
                        break;
                    }
                case Jbig2SegmentType.ImmediateGenericRegion:
                case Jbig2SegmentType.ImmediateLosslessGenericRegion:
                    {
                        ProcessGenericRegion(segment, segmentData, pageBitmap, cache, observer);
                        break;
                    }
                case Jbig2SegmentType.IntermediateGenericRegion:
                    {
                        ProcessGenericRegion(segment, segmentData, pageBitmap: null, cache, observer);
                        break;
                    }
                case Jbig2SegmentType.ImmediateTextRegion:
                case Jbig2SegmentType.ImmediateLosslessTextRegion:
                    {
                        ProcessTextRegion(segment, segmentData, pageBitmap, cache);
                        break;
                    }
                case Jbig2SegmentType.IntermediateTextRegion:
                    {
                        ProcessTextRegion(segment, segmentData, pageBitmap: null, cache);
                        break;
                    }
                case Jbig2SegmentType.ImmediateHalftoneRegion:
                case Jbig2SegmentType.ImmediateLosslessHalftoneRegion:
                    {
                        ProcessHalftoneRegion(segment, segmentData, pageBitmap, cache);
                        break;
                    }
                case Jbig2SegmentType.IntermediateHalftoneRegion:
                    {
                        ProcessHalftoneRegion(segment, segmentData, pageBitmap: null, cache);
                        break;
                    }
                case Jbig2SegmentType.PatternDictionary:
                    {
                        ProcessPatternDictionary(segment, segmentData, cache);
                        break;
                    }
                case Jbig2SegmentType.ImmediateGenericRefinementRegion:
                case Jbig2SegmentType.ImmediateLosslessGenericRefinementRegion:
                    {
                        ProcessRefinementRegion(segment, segmentData, pageBitmap, cache);
                        break;
                    }
                case Jbig2SegmentType.IntermediateGenericRefinementRegion:
                    {
                        ProcessRefinementRegion(segment, segmentData, pageBitmap: null, cache);
                        break;
                    }
                case Jbig2SegmentType.Tables:
                    {
                        ProcessTableSegment(segment, segmentData, cache);
                        break;
                    }
                case Jbig2SegmentType.PageInformation:
                case Jbig2SegmentType.EndOfPage:
                case Jbig2SegmentType.EndOfFile:
                case Jbig2SegmentType.EndOfStripe:
                    // Control segments — no bitmap processing needed
                    break;
            }
        }
    }

    private void ProcessSymbolDictionary(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2SegmentCache cache)
    {
        List<Jbig2Bitmap> referredSymbols = cache.CollectReferredSymbols(segment);
        List<Jbig2HuffmanTable> customTables = cache.CollectReferredUserTables(segment);
        Jbig2Bitmap[] decoded = Jbig2SymbolDictionaryDecoder.Decode(segmentData, referredSymbols, customTables);
        cache.AddSymbolDictionary(segment.SegmentNumber, decoded);
    }

    private void ProcessTextRegion(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2Bitmap? pageBitmap, Jbig2SegmentCache cache)
    {
        var regionHeader = Jbig2RegionHeader.Parse(segment, segmentData);
        List<Jbig2Bitmap> symbols = cache.CollectReferredSymbols(segment);
        List<Jbig2HuffmanTable> customTables = cache.CollectReferredUserTables(segment);

        ReadOnlySpan<byte> textData = segmentData.Slice(Jbig2RegionHeader.Jbig2RegionHeaderLength);
        Jbig2TextRegionPlacements placements = Jbig2TextRegionDecoder.Decode(textData, regionHeader, symbols, customTables);

        if (pageBitmap == null)
        {
            // Intermediate region: materialise the region bitmap (default-pixel fill + SBCOMBOP)
            // by composing onto a fresh target with REPLACE; later segments may reference it.
            Jbig2Bitmap regionBitmap = new(regionHeader.Width, regionHeader.Height);
            placements.Compose(regionBitmap, 0, 0, Jbig2CombinationOperator.Replace);
            cache.AddIntermediateRegion(segment.SegmentNumber, regionBitmap);
        }
        else
        {
            placements.Compose(pageBitmap, regionHeader.X, regionHeader.Y, regionHeader.CombinationOperator);
        }
    }

    private void ProcessRefinementRegion(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2Bitmap? pageBitmap, Jbig2SegmentCache cache)
    {
        var regionHeader = Jbig2RegionHeader.Parse(segment, segmentData);
        Jbig2Bitmap regionBitmap = DecodeRefinementRegion(segment, segmentData, regionHeader, pageBitmap, cache);

        if (regionBitmap == null)
        {
            return;
        }

        cache.AddIntermediateRegion(segment.SegmentNumber, regionBitmap);

        pageBitmap?.Composite(regionBitmap, regionHeader.X, regionHeader.Y, regionHeader.CombinationOperator);
    }

    private Jbig2Bitmap DecodeRefinementRegion(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2RegionHeader regionInfo, Jbig2Bitmap? pageBitmap, Jbig2SegmentCache cache)
    {
        Jbig2Bitmap? reference = cache.ResolveReferenceBitmap(segment);

        bool usePageAsReference = reference == null && pageBitmap != null;
        if (usePageAsReference)
        {
            reference = pageBitmap;
        }

        // When the page bitmap is used as reference, output pixel (x,y) maps to page pixel
        // (x + regionX, y + regionY), so refDx = -regionX, refDy = -regionY.
        int refOffsetX = usePageAsReference ? -regionInfo.X : 0;
        int refOffsetY = usePageAsReference ? -regionInfo.Y : 0;

        // Flags byte and AT pixels follow the 17-byte region header; Jbig2RefinementRegionDecoder reads them internally.
        return Jbig2RefinementRegionDecoder.Decode(
            segmentData.Slice(Jbig2RegionHeader.Jbig2RegionHeaderLength),
            regionInfo,
            reference,
            refOffsetX,
            refOffsetY);
    }

    private void ProcessPatternDictionary(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2SegmentCache cache)
    {
        Jbig2Bitmap[] patterns = Jbig2PatternDictionaryDecoder.Decode(segmentData);
        cache.AddPatternDictionary(segment.SegmentNumber, patterns);
    }

    private void ProcessTableSegment(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2SegmentCache cache)
    {
        Jbig2TableParser parser = new();
        Jbig2HuffmanTable? table = parser.Parse(segmentData);

        if (table != null)
        {
            cache.AddUserTable(segment.SegmentNumber, table);
        }
    }

    private void ProcessHalftoneRegion(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2Bitmap? pageBitmap, Jbig2SegmentCache cache)
    {
        var regionHeader = Jbig2RegionHeader.Parse(segment, segmentData);

        Jbig2Bitmap[]? patterns = cache.CollectReferredPatterns(segment);

        if (patterns == null || patterns.Length == 0)
        {
            return;
        }

        // Halftone region data starts after region info
        ReadOnlySpan<byte> halftoneData = segmentData.Slice(Jbig2RegionHeader.Jbig2RegionHeaderLength);

        Jbig2Bitmap regionBitmap = Jbig2HalftoneRegionDecoder.Decode(halftoneData, regionHeader, patterns);

        if (regionBitmap == null)
        {
            return;
        }

        if (pageBitmap == null)
        {
            cache.AddIntermediateRegion(segment.SegmentNumber, regionBitmap);
        }
        else
        {
            pageBitmap.Composite(regionBitmap, regionHeader.X, regionHeader.Y, regionHeader.CombinationOperator);
        }
    }

    private void ProcessGenericRegion(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> segmentData, Jbig2Bitmap? pageBitmap, Jbig2SegmentCache cache, IJBig2ExectionObserver? observer = null)
    {
        var regionHeader = Jbig2RegionHeader.Parse(segment, segmentData);

        // Flags byte and coded data follow the region header; Jbig2GenericRegionDecoder reads flags internally.
        ReadOnlySpan<byte> codedData = segmentData.Slice(Jbig2RegionHeader.Jbig2RegionHeaderLength);

        Jbig2Bitmap regionBitmap = Jbig2GenericRegionDecoder.Decode(codedData, regionHeader, observer);

        // Composite onto page, or store for potential reference by later segments (e.g. refinement regions)
        if (pageBitmap == null)
        {
            cache.AddIntermediateRegion(segment.SegmentNumber, regionBitmap);
        }
        else
        {
            pageBitmap.Composite(regionBitmap, regionHeader.X, regionHeader.Y, regionHeader.CombinationOperator);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Jbig2PageInfo? ParsePageInformation(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> segData = data.Slice(segment.DataOffset, (int)segment.DataLength);

        if (segData.Length < 19)
        {
            return null;
        }


        Jbig2PageInfo info = new()
        {
            Width = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(0, 4)),
            Height = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(4, 4)),
            XResolution = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(8, 4)),
            YResolution = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(12, 4))
        };

        byte flags = segData[16];
        info.DefaultPixelValue = (byte)((flags >> 2) & 1);
        info.CombinationOperator = (Jbig2CombinationOperator)((flags >> 3) & 0x03);
        info.RequiresBuffer = (flags & 0x20) == 0;

        return info;
    }

    /// <summary>
    /// Returns the page the first page information segment belongs to, or 1 when there is none.
    /// </summary>
    /// <param name="segments">Already-parsed segment headers.</param>
    private static int GetFirstPageNumber(List<Jbig2SegmentHeader> segments)
    {
        foreach (Jbig2SegmentHeader segment in segments)
        {
            if (segment.Type == Jbig2SegmentType.PageInformation)
            {
                return segment.PageAssociation;
            }
        }

        return 1;
    }
}
