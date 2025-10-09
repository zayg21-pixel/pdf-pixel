using System;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Quantization;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Manages JPEG quantization tables and provides quantization/dequantization operations.
    /// Throws exceptions on invalid usage (e.g. missing required tables).
    /// </summary>
    internal sealed class JpgQuantizationManager
    {
        private readonly JpgQuantizationTable[] _tables;

        private readonly int[][] _entriesInt = new int[MaxTableCount][];
        private readonly int[][] _entriesNaturalInt = new int[MaxTableCount][];

        public const int MaxTableCount = 4;

        private JpgQuantizationManager()
        {
            _tables = new JpgQuantizationTable[MaxTableCount];
        }

        /// <summary>
        /// Create a quantization manager from the header. Throws if header is null.
        /// </summary>
        public static JpgQuantizationManager CreateFromHeader(JpgHeader header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            JpgQuantizationManager manager = new JpgQuantizationManager();
            if (header.QuantizationTables == null)
            {
                return manager; // No tables provided (legal for scans referencing none yet)
            }

            foreach (JpgQuantizationTable quantTable in header.QuantizationTables)
            {
                if (quantTable == null)
                {
                    continue;
                }

                if (quantTable.TableId < 0 || quantTable.TableId >= MaxTableCount)
                {
                    continue; // Ignore out-of-range table ids
                }

                manager._tables[quantTable.TableId] = quantTable;

                int[] zigZagEntries = new int[64];
                for (int coefficientIndex = 0; coefficientIndex < 64; coefficientIndex++)
                {
                    zigZagEntries[coefficientIndex] = quantTable.Entries[coefficientIndex];
                }
                manager._entriesInt[quantTable.TableId] = zigZagEntries;

                int[] naturalEntries = new int[64];
                for (int zigIndex = 0; zigIndex < 64; zigIndex++)
                {
                    int naturalIndex = JpgZigZag.Table[zigIndex];
                    naturalEntries[naturalIndex] = zigZagEntries[zigIndex];
                }
                manager._entriesNaturalInt[quantTable.TableId] = naturalEntries;
            }

            return manager;
        }

        public JpgQuantizationTable GetTable(int tableId)
        {
            if (tableId < 0 || tableId >= MaxTableCount)
            {
                return null;
            }

            return _tables[tableId];
        }

        /// <summary>
        /// Ensure the quantization table exists for a component; throws if missing.
        /// </summary>
        public void ValidateTableExists(int tableId, int componentIndex)
        {
            JpgQuantizationTable table = GetTable(tableId);
            if (table == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} required for component {componentIndex} is missing.");
            }
        }

        /// <summary>
        /// Create a new <see cref="Block8x8F"/> whose 64 scalar elements are populated with the natural-order
        /// quantization table entries for the given <paramref name="tableId"/>. This mirrors the internal
        /// representation used by <see cref="Idct.ScaledIdctPlan.DequantNaturalBlock"/>.
        /// </summary>
        /// <param name="tableId">Quantization table identifier (0..3).</param>
        /// <returns>Block with natural-order table values converted to float.</returns>
        public Block8x8F CreateNaturalBlock(int tableId)
        {
            if (tableId < 0 || tableId >= MaxTableCount)
            {
                throw new ArgumentOutOfRangeException(nameof(tableId));
            }

            int[] naturalEntries = _entriesNaturalInt[tableId];
            if (naturalEntries == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} not loaded.");
            }

            Block8x8F block = default;
            for (int coefficientIndex = 0; coefficientIndex < Block8x8F.Size; coefficientIndex++)
            {
                block[coefficientIndex] = naturalEntries[coefficientIndex];
            }

            return block;
        }
    }
}