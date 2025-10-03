using System;
using PdfReader.Rendering.Image.Jpg.Model;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Manages JPEG Huffman decoder tables for DC and AC coefficients.
    /// Provides validation and lookup of decoder tables by ID.
    /// </summary>
    internal sealed class JpgHuffmanDecoderManager
    {
        private readonly JpgHuffmanDecoder[] _dcDecoders;
        private readonly JpgHuffmanDecoder[] _acDecoders;

        /// <summary>
        /// Maximum number of Huffman tables supported (0-3 for both DC and AC).
        /// </summary>
        public const int MaxTableCount = 4;

        public JpgHuffmanDecoderManager()
        {
            _dcDecoders = new JpgHuffmanDecoder[MaxTableCount];
            _acDecoders = new JpgHuffmanDecoder[MaxTableCount];
        }

        /// <summary>
        /// Initialize decoders from the JPEG header Huffman tables.
        /// </summary>
        public static JpgHuffmanDecoderManager CreateFromHeader(JpgHeader header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            var manager = new JpgHuffmanDecoderManager();

            foreach (var huffmanTable in header.HuffmanTables)
            {
                if (huffmanTable.TableId >= 0 && huffmanTable.TableId < MaxTableCount)
                {
                    var decoder = new JpgHuffmanDecoder(huffmanTable);
                    if (huffmanTable.TableClass == 0) // DC
                    {
                        manager._dcDecoders[huffmanTable.TableId] = decoder;
                    }
                    else if (huffmanTable.TableClass == 1) // AC
                    {
                        manager._acDecoders[huffmanTable.TableId] = decoder;
                    }
                }
            }

            return manager;
        }

        /// <summary>
        /// Get a DC Huffman decoder by table ID.
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
        /// Get an AC Huffman decoder by table ID.
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
        /// Validate that all required Huffman tables are present for a scan.
        /// </summary>
        public bool ValidateTablesForScan(JpgScanSpec scan)
        {
            if (scan == null)
            {
                return false;
            }

            for (int scanComponentIndex = 0; scanComponentIndex < scan.Components.Count; scanComponentIndex++)
            {
                var scanComponent = scan.Components[scanComponentIndex];
                int dcTableId = scanComponent.DcTableId;
                int acTableId = scanComponent.AcTableId;

                if (dcTableId < 0 || dcTableId >= MaxTableCount || _dcDecoders[dcTableId] == null)
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Missing DC Huffman table id {dcTableId} for scan component {scanComponentIndex}");
                    return false;
                }

                if (acTableId < 0 || acTableId >= MaxTableCount || _acDecoders[acTableId] == null)
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Missing AC Huffman table id {acTableId} for scan component {scanComponentIndex}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get decoders for a specific scan component.
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
}