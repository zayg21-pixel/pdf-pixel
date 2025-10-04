using System;
using System.Numerics;
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

            var manager = new JpgQuantizationManager();
            if (header.QuantizationTables == null)
            {
                return manager; // No tables provided (legal for scans referencing none yet)
            }

            foreach (var quantTable in header.QuantizationTables)
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

                int[] ints = new int[64];
                for (int i = 0; i < 64; i++)
                {
                    ints[i] = quantTable.Entries[i];
                }
                manager._entriesInt[quantTable.TableId] = ints;

                int[] nat = new int[64];
                for (int zig = 0; zig < 64; zig++)
                {
                    int naturalIndex = JpgZigZag.Table[zig];
                    nat[naturalIndex] = ints[zig];
                }
                manager._entriesNaturalInt[quantTable.TableId] = nat;
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
            var table = GetTable(tableId);
            if (table == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} required for component {componentIndex} is missing.");
            }
        }

        public void DequantizeBlock(int[] coefficientsZigZag, int quantizationTableId)
        {
            DequantizeBlockZigZag(coefficientsZigZag, quantizationTableId);
        }

        public void DequantizeNaturalBlock(int[] coefficientsNatural, int quantizationTableId)
        {
            DequantizeNaturalBlockInPlace(coefficientsNatural, quantizationTableId);
        }

        public void DequantizeBlockZigZag(int[] coefficientsZigZag, int tableId)
        {
            var q = tableId >= 0 && tableId < MaxTableCount ? _entriesInt[tableId] : null;
            if (q == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} not loaded.");
            }

            const int Width = 64;
            int vectorWidth = Vector<int>.Count;
            if (Vector.IsHardwareAccelerated && Width >= vectorWidth)
            {
                int i = 0;
                for (; i <= Width - vectorWidth; i += vectorWidth)
                {
                    var v = new Vector<int>(coefficientsZigZag, i);
                    var m = new Vector<int>(q, i);
                    (v * m).CopyTo(coefficientsZigZag, i);
                }
                for (; i < Width; i++)
                {
                    coefficientsZigZag[i] = coefficientsZigZag[i] * q[i];
                }
            }
            else
            {
                for (int i = 0; i < Width; i++)
                {
                    coefficientsZigZag[i] = coefficientsZigZag[i] * q[i];
                }
            }
        }

        public void DequantizeNaturalBlockInPlace(int[] coefficientsNatural, int tableId)
        {
            var qNat = tableId >= 0 && tableId < MaxTableCount ? _entriesNaturalInt[tableId] : null;
            if (qNat == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} not loaded.");
            }

            const int Width = 64;
            int vectorWidth = Vector<int>.Count;
            if (Vector.IsHardwareAccelerated && Width >= vectorWidth)
            {
                int i = 0;
                for (; i <= Width - vectorWidth; i += vectorWidth)
                {
                    var v = new Vector<int>(coefficientsNatural, i);
                    var m = new Vector<int>(qNat, i);
                    (v * m).CopyTo(coefficientsNatural, i);
                }
                for (; i < Width; i++)
                {
                    coefficientsNatural[i] = coefficientsNatural[i] * qNat[i];
                }
            }
            else
            {
                for (int i = 0; i < Width; i++)
                {
                    coefficientsNatural[i] = coefficientsNatural[i] * qNat[i];
                }
            }
        }

        public void DequantizeAndDeZigZag(ReadOnlySpan<int> srcZigZag, int tableId, Span<int> dstNatural)
        {
            var q = tableId >= 0 && tableId < MaxTableCount ? _entriesInt[tableId] : null;
            if (q == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} not loaded.");
            }

            for (int i = 0; i < 64; i++)
            {
                int naturalIndex = JpgZigZag.Table[i];
                dstNatural[naturalIndex] = srcZigZag[i] * q[i];
            }
        }

        public Idct.ScaledIdctPlan CreateScaledIdctPlan(int tableId)
        {
            var q = tableId >= 0 && tableId < MaxTableCount ? _entriesInt[tableId] : null;
            var qNat = tableId >= 0 && tableId < MaxTableCount ? _entriesNaturalInt[tableId] : null;
            if (q == null || qNat == null)
            {
                throw new InvalidOperationException($"Quantization table id {tableId} not loaded.");
            }

            int[] zig = new int[64];
            int[] nat = new int[64];
            Array.Copy(q, zig, 64);
            Array.Copy(qNat, nat, 64);
            return new Idct.ScaledIdctPlan(tableId, zig, nat);
        }
    }
}