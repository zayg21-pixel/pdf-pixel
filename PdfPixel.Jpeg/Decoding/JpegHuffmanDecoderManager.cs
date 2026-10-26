using System;

using PdfPixel.Jpeg.Huffman;

namespace PdfPixel.Jpeg.Decoding;

/// <summary>
/// Manages JPEG Huffman decoder tables for DC and AC coefficients.
/// Provides validation and lookup of decoder tables by identifier.
/// </summary>
internal sealed class JpegHuffmanDecoderManager
{
    /// <summary>
    /// Maximum number of Huffman tables supported (0-3 for both DC and AC).
    /// </summary>
    public const int MaxTableCount = 4;

    private readonly JpegHuffmanDecoder[] _dcDecoders;
    private readonly JpegHuffmanDecoder[] _acDecoders;

    private JpegHuffmanDecoderManager()
    {
        _dcDecoders = new JpegHuffmanDecoder[MaxTableCount];
        _acDecoders = new JpegHuffmanDecoder[MaxTableCount];
    }

    /// <summary>
    /// Create and populate a decoder manager from the Huffman tables declared in the JPEG header.
    /// Missing tables for a later scan will be detected during validation.
    /// </summary>
    /// <param name="header">Parsed JPEG header.</param>
    /// <returns>Populated decoder manager.</returns>
    public static JpegHuffmanDecoderManager CreateFromHeader(JpegHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        JpegHuffmanDecoderManager manager = new();
        foreach (JpegHuffmanTable huffmanTable in header.HuffmanTables)
        {
            if (huffmanTable == null)
            {
                continue;
            }

            if (huffmanTable.TableId < 0 || huffmanTable.TableId >= MaxTableCount)
            {
                continue; // Ignore out-of-range table IDs per permissive behavior
            }

            JpegHuffmanDecoder decoder = new(huffmanTable);
            if (huffmanTable.TableClass == 0)
            {
                manager._dcDecoders[huffmanTable.TableId] = decoder;
            }
            else if (huffmanTable.TableClass == 1)
            {
                manager._acDecoders[huffmanTable.TableId] = decoder;
            }
        }

        return manager;
    }

    /// <summary>
    /// Retrieve a DC Huffman decoder by table identifier or null if not present.
    /// </summary>
    public JpegHuffmanDecoder GetDcDecoder(int tableId)
    {
        if (tableId < 0 || tableId >= MaxTableCount)
        {
            throw new ArgumentOutOfRangeException($"DC decoder {tableId} is out of range of allowed 0-{MaxTableCount}.");
        }

        return _dcDecoders[tableId];
    }

    /// <summary>
    /// Retrieve an AC Huffman decoder by table identifier or null if not present.
    /// </summary>
    public JpegHuffmanDecoder GetAcDecoder(int tableId)
    {
        if (tableId < 0 || tableId >= MaxTableCount)
        {
            throw new ArgumentOutOfRangeException($"AC decoder {tableId}  is out of range of allowed 0- {MaxTableCount}.");
        }

        return _acDecoders[tableId];
    }

    /// <summary>
    /// Validate that the required Huffman tables referenced by the scan are available.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message if any table is missing.
    /// </summary>
    /// <param name="scan">Scan specification to validate.</param>
    public void ValidateTablesForScan(JpegScanSpec scan)
    {
        if (scan == null)
        {
            throw new ArgumentNullException(nameof(scan));
        }

        for (int scanComponentIndex = 0; scanComponentIndex < scan.Components.Count; scanComponentIndex++)
        {
            JpegScanComponentSpec scanComponent = scan.Components[scanComponentIndex];
            int dcTableId = scanComponent.DcTableId;
            int acTableId = scanComponent.AcTableId;

            if (dcTableId < 0 || dcTableId >= MaxTableCount || _dcDecoders[dcTableId] == null)
            {
                throw new InvalidOperationException(
                    $"Missing DC Huffman table (id={dcTableId}) required by scan component index {scanComponentIndex}.");
            }

            if (acTableId < 0 || acTableId >= MaxTableCount || _acDecoders[acTableId] == null)
            {
                throw new InvalidOperationException(
                    $"Missing AC Huffman table (id={acTableId}) required by scan component index {scanComponentIndex}.");
            }
        }
    }

    /// <summary>
    /// Get both DC and AC decoders for the supplied scan component specification.
    /// Returns (null, null) if <paramref name="scanComponent"/> is null.
    /// </summary>
    public (JpegHuffmanDecoder dcDecoder, JpegHuffmanDecoder acDecoder) GetDecodersForScanComponent(JpegScanComponentSpec scanComponent)
    {
        JpegHuffmanDecoder dcDecoder = GetDcDecoder(scanComponent.DcTableId);
        JpegHuffmanDecoder acDecoder = GetAcDecoder(scanComponent.AcTableId);
        return (dcDecoder, acDecoder);
    }
}
