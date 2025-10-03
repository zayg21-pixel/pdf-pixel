using System;
using System.Numerics;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Quantization;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Manages JPEG quantization tables and provides quantization/dequantization operations.
    /// </summary>
    internal sealed class JpgQuantizationManager
    {
        private readonly JpgQuantizationTable[] _tables;

        // Cached int copies of entries for faster math and vectorization
        private readonly int[][] _entriesInt = new int[MaxTableCount][];
        // Cached entries remapped to natural order (index = natural, value = quant at that natural position)
        private readonly int[][] _entriesNaturalInt = new int[MaxTableCount][];

        /// <summary>
        /// Maximum number of quantization tables supported (0-3).
        /// </summary>
        public const int MaxTableCount = 4;

        public JpgQuantizationManager()
        {
            _tables = new JpgQuantizationTable[MaxTableCount];
        }

        /// <summary>
        /// Initialize quantization manager from JPEG header.
        /// </summary>
        public static JpgQuantizationManager CreateFromHeader(JpgHeader header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            var manager = new JpgQuantizationManager();

            foreach (var quantTable in header.QuantizationTables)
            {
                if (quantTable.TableId >= 0 && quantTable.TableId < MaxTableCount)
                {
                    manager._tables[quantTable.TableId] = quantTable;

                    // Build int[] entries
                    int[] ints = new int[64];
                    for (int i = 0; i < 64; i++)
                    {
                        ints[i] = quantTable.Entries[i];
                    }
                    manager._entriesInt[quantTable.TableId] = ints;

                    // Build natural-order mapping
                    int[] nat = new int[64];
                    for (int zig = 0; zig < 64; zig++)
                    {
                        int natural = JpgZigZag.Table[zig];
                        nat[natural] = ints[zig];
                    }
                    manager._entriesNaturalInt[quantTable.TableId] = nat;
                }
            }

            return manager;
        }

        /// <summary>
        /// Get quantization table by ID.
        /// </summary>
        public JpgQuantizationTable GetTable(int tableId)
        {
            if (tableId < 0 || tableId >= MaxTableCount)
            {
                return null;
            }

            return _tables[tableId];
        }

        /// <summary>
        /// Validate that a quantization table exists for the given ID.
        /// </summary>
        public bool ValidateTableExists(int tableId, int componentIndex)
        {
            var table = GetTable(tableId);
            if (table == null)
            {
                Console.Error.WriteLine($"[PdfReader][JPEG] Missing quantization table id {tableId} for component index {componentIndex}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Dequantize a 64-element coefficient block using the specified quantization table.
        /// Coefficients are in zig-zag order.
        /// </summary>
        public void DequantizeBlock(int[] coefficientsZigZag, int quantizationTableId)
        {
            // Route to vectorized implementation
            DequantizeBlockZigZag(coefficientsZigZag, quantizationTableId);
        }

        /// <summary>
        /// Dequantize a 64-element coefficient block that is stored in natural (row-major) order in-place.
        /// </summary>
        public void DequantizeNaturalBlock(int[] coefficientsNatural, int quantizationTableId)
        {
            DequantizeNaturalBlockInPlace(coefficientsNatural, quantizationTableId);
        }

        /// <summary>
        /// Vectorized dequantization when coefficients are in zig-zag order.
        /// </summary>
        public void DequantizeBlockZigZag(int[] coefficientsZigZag, int tableId)
        {
            var q = tableId >= 0 && tableId < MaxTableCount ? _entriesInt[tableId] : null;
            if (q == null)
            {
                return;
            }

            int width = 64;
            int vec = Vector<int>.Count;
            if (Vector.IsHardwareAccelerated && width >= vec)
            {
                int i = 0;
                for (; i <= width - vec; i += vec)
                {
                    var v = new Vector<int>(coefficientsZigZag, i);
                    var m = new Vector<int>(q, i);
                    (v * m).CopyTo(coefficientsZigZag, i);
                }

                for (; i < width; i++)
                {
                    coefficientsZigZag[i] = coefficientsZigZag[i] * q[i];
                }
            }
            else
            {
                for (int i = 0; i < width; i++)
                {
                    coefficientsZigZag[i] = coefficientsZigZag[i] * q[i];
                }
            }
        }

        /// <summary>
        /// Vectorized in-place dequantization for natural-order coefficients.
        /// </summary>
        public void DequantizeNaturalBlockInPlace(int[] coefficientsNatural, int tableId)
        {
            var qNat = tableId >= 0 && tableId < MaxTableCount ? _entriesNaturalInt[tableId] : null;
            if (qNat == null)
            {
                return;
            }

            int width = 64;
            int vec = Vector<int>.Count;
            if (Vector.IsHardwareAccelerated && width >= vec)
            {
                int i = 0;
                for (; i <= width - vec; i += vec)
                {
                    var v = new Vector<int>(coefficientsNatural, i);
                    var m = new Vector<int>(qNat, i);
                    (v * m).CopyTo(coefficientsNatural, i);
                }

                for (; i < width; i++)
                {
                    coefficientsNatural[i] = coefficientsNatural[i] * qNat[i];
                }
            }
            else
            {
                for (int i = 0; i < width; i++)
                {
                    coefficientsNatural[i] = coefficientsNatural[i] * qNat[i];
                }
            }
        }

        /// <summary>
        /// Fused dequantization and de-zigzag: writes natural-order output from zig-zag coefficients.
        /// </summary>
        public void DequantizeAndDeZigZag(ReadOnlySpan<int> srcZigZag, int tableId, Span<int> dstNatural)
        {
            var q = tableId >= 0 && tableId < MaxTableCount ? _entriesInt[tableId] : null;
            if (q == null)
            {
                for (int i = 0; i < 64; i++)
                {
                    int natural = JpgZigZag.Table[i];
                    dstNatural[natural] = srcZigZag[i];
                }
                return;
            }

            for (int i = 0; i < 64; i++)
            {
                int natural = JpgZigZag.Table[i];
                dstNatural[natural] = srcZigZag[i] * q[i];
            }
        }

        /// <summary>
        /// Create a ScaledIdctPlan for the given quantization table id.
        /// </summary>
        public Idct.ScaledIdctPlan CreateScaledIdctPlan(int tableId)
        {
            var q = tableId >= 0 && tableId < MaxTableCount ? _entriesInt[tableId] : null;
            var qNat = tableId >= 0 && tableId < MaxTableCount ? _entriesNaturalInt[tableId] : null;
            if (q == null || qNat == null)
            {
                return null;
            }

            // Clone to ensure safety if caller caches plan beyond manager lifetime
            int[] zig = new int[64];
            int[] nat = new int[64];
            Array.Copy(q, zig, 64);
            Array.Copy(qNat, nat, 64);
            return new Idct.ScaledIdctPlan(tableId, zig, nat);
        }
    }
}