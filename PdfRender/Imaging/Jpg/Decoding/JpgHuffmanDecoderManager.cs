using System;
using PdfRender.Imaging.Jpg.Huffman;
using PdfRender.Imaging.Jpg.Model;

namespace PdfRender.Imaging.Jpg.Decoding;

/// <summary>
/// Manages JPEG Huffman decoder tables for DC and AC coefficients.
/// Provides validation and lookup of decoder tables by identifier.
/// </summary>
internal sealed class JpgHuffmanDecoderManager
{
    /// <summary>
    /// Maximum number of Huffman tables supported (0-3 for both DC and AC).
    /// </summary>
    public const int MaxTableCount = 4;

    private readonly JpgHuffmanDecoder[] _dcDecoders;
    private readonly JpgHuffmanDecoder[] _acDecoders;

    private JpgHuffmanDecoderManager()
    {
        _dcDecoders = new JpgHuffmanDecoder[MaxTableCount];
        _acDecoders = new JpgHuffmanDecoder[MaxTableCount];
    }

    /// <summary>
    /// Create and populate a decoder manager from the Huffman tables declared in the JPEG header.
    /// Missing tables for a later scan will be detected during validation.
    /// </summary>
    /// <param name="header">Parsed JPEG header.</param>
    /// <returns>Populated decoder manager.</returns>
    public static JpgHuffmanDecoderManager CreateFromHeader(JpgHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        var manager = new JpgHuffmanDecoderManager();
        foreach (var huffmanTable in header.HuffmanTables)
        {
            if (huffmanTable == null)
            {
                continue;
            }
            if (huffmanTable.TableId < 0 || huffmanTable.TableId >= MaxTableCount)
            {
                continue; // Ignore out-of-range table IDs per permissive behavior
            }

            var decoder = new JpgHuffmanDecoder(huffmanTable);
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
    public JpgHuffmanDecoder GetDcDecoder(int tableId)
    {
        if (tableId < 0 || tableId >= MaxTableCount)
        {
            return null;
        }
        return _dcDecoders[tableId];
    }

    /// <summary>
    /// Retrieve an AC Huffman decoder by table identifier or null if not present.
    /// </summary>
    public JpgHuffmanDecoder GetAcDecoder(int tableId)
    {
        if (tableId < 0 || tableId >= MaxTableCount)
        {
            return null;
        }
        return _acDecoders[tableId];
    }

    /// <summary>
    /// Validate that the required Huffman tables referenced by the scan are available.
    /// Throws <see cref="InvalidOperationException"/> with a descriptive message if any table is missing.
    /// </summary>
    /// <param name="scan">Scan specification to validate.</param>
    public void ValidateTablesForScan(JpgScanSpec scan)
    {
        if (scan == null)
        {
            throw new ArgumentNullException(nameof(scan));
        }

        for (int scanComponentIndex = 0; scanComponentIndex < scan.Components.Count; scanComponentIndex++)
        {
            var scanComponent = scan.Components[scanComponentIndex];
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
    public (JpgHuffmanDecoder dcDecoder, JpgHuffmanDecoder acDecoder) GetDecodersForScanComponent(JpgScanComponentSpec scanComponent)
    {
        if (scanComponent == null)
        {
            return (null, null);
        }
        var dcDecoder = GetDcDecoder(scanComponent.DcTableId);
        var acDecoder = GetAcDecoder(scanComponent.AcTableId);
        return (dcDecoder, acDecoder);
    }
}