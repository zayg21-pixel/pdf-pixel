using System;

namespace PdfReader.Rendering.Image.Jpg.Idct
{
    /// <summary>
    /// Precomputed plan for scaled IDCT that carries dequantization multipliers.
    /// This enables fused dequantization + zig-zag remap inside the IDCT call site.
    /// </summary>
    internal sealed class ScaledIdctPlan
    {
        public int TableId { get; }
        /// <summary>
        /// Dequant multipliers in zig-zag order (length 64).
        /// </summary>
        public int[] DequantZig { get; }
        /// <summary>
        /// Dequant multipliers in natural order (length 64).
        /// </summary>
        public int[] DequantNatural { get; }

        public ScaledIdctPlan(int tableId, int[] dequantZig, int[] dequantNatural)
        {
            TableId = tableId;
            DequantZig = dequantZig ?? throw new ArgumentNullException(nameof(dequantZig));
            DequantNatural = dequantNatural ?? throw new ArgumentNullException(nameof(dequantNatural));
            if (dequantZig.Length < 64 || dequantNatural.Length < 64)
            {
                throw new ArgumentException("Quantization tables must have 64 entries.");
            }
        }
    }
}
