using PdfPixel.Imaging.Jbig2.Model;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Jbig2.Decoding; // TODO: [MEDIUM] continue to optimize here

/// <summary>
/// Shared row-level decode logic for JBIG2 generic and refinement regions.
/// Each method iterates over a row of pixels, builds a template-specific context label,
/// decodes one bit via the arithmetic coder, and sets the pixel.
/// Template data and building logic lives in <see cref="Jbig2Templates"/>.
/// </summary>
internal static class Jbig2RowDecoder
{
    /// <summary>
    /// Decodes one row using the given template pixels. Iterates over each pixel, builds the
    /// context label from <paramref name="templatePixels"/>, decodes one bit via the arithmetic
    /// coder, and writes it to the bitmap. Used for both default-AT and custom-AT layouts.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeRow(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int y,
        int width,
        ReadOnlySpan<Jbig2ContextPixel> templatePixels)
    {
        for (int x = 0; x < width; x++)
        {
            DecodeValue(ref decoder, bitmap, contexts, x, y, templatePixels);
        }
    }

    /// <summary>
    /// Decodes a single pixel using per-pixel GetPixel context gathering (slow path).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeValue(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int x,
        int y,
        ReadOnlySpan<Jbig2ContextPixel> templatePixels)
    {
        int context = 0;
        for (int k = 0; k < templatePixels.Length; k++)
        {
            ref readonly Jbig2ContextPixel pixel = ref templatePixels[k];
            context |= bitmap.GetPixel(x + pixel.Dx, y + pixel.Dy) << pixel.Shift;
        }

        int bit = decoder.DecodeBit(ref contexts[context]);
        bitmap.SetPixel(x, y, bit);
    }

    /// <summary>
    /// Fast row decoder for generic regions. Reads context bits directly from bitmap.Data
    /// using mask extraction. For each row group, loads 2 bytes from the source row and
    /// extracts all group bits with a single shift+mask.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeRowOld(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int y,
        int width,
        Jbig2RowTemplate tmpl)
    {
        int groupCount = tmpl.Groups.Length;
        Span<byte> data = bitmap.Data;
        int stride = bitmap.Stride;
        int height = bitmap.Height;

        // Destination row output
        Span<byte> destRow = bitmap.GetRow(y);
        int destByteIdx = 0;
        int dataLength = data.Length;
        ref byte contextRef = ref contexts[0];
        ref byte sourceRef = ref data[0];
        ref byte destByteRef = ref destRow[0];
        byte destAccum = 0;
        int destBitPos = 7;

        for (int x = 0; x < width; x++)
        {
            uint context = 0;

            for (int g = 0; g < groupCount; g++)
            {
                ref readonly Jbig2RowGroupInfo grp = ref tmpl.Groups[g];
                int rowY = y + grp.Dy;
                if ((uint)rowY >= (uint)height)
                {
                    continue;
                }

                int startX = x + grp.MinDx;
                int endX = x + grp.MaxDx;
                byte contextShift = grp.ContextShift;

                int bitCount = (byte)(grp.MaxDx - grp.MinDx + 1);
                uint groupMask = (1u << bitCount) - 1;
                int rowBase = rowY * stride;
                int bytePos = rowBase + (startX >> 3);
                int bitPos = startX & 7;
                ref var sourceByteRef = ref Unsafe.Add(ref sourceRef, bytePos);

                uint raw = 0;

                if (bytePos >= 0 && bytePos < dataLength)
                {
                    raw |= (uint)sourceByteRef << 8;
                }
                if (bytePos + 1 >= 0 && bytePos + 1 < dataLength)
                {
                    raw |= Unsafe.Add(ref sourceByteRef, 1);
                }

                // Extract the bits window
                int shift = 16 - bitCount - bitPos;
                uint bits = (raw >> shift) & groupMask;

                // Zero out OOB bits using batch masks (lower bits first, then top)
                if (endX >= width)
                {
                    int oobRight = endX - width + 1;
                    bits &= ~((1u << oobRight) - 1);
                }
                if (startX < 0)
                {
                    int validBits = bitCount + startX;
                    if (validBits <= 0)
                    {
                        bits = 0;
                    }
                    else
                    {
                        bits &= (1u << validBits) - 1;
                    }
                }

                context |= bits << contextShift;
            }

            int bit = decoder.DecodeBit(ref Unsafe.Add(ref contextRef, context));

            // Write decoded bit to output
            destAccum |= (byte)(bit << destBitPos);
            destByteRef = destAccum;
            destBitPos--;

            if (destBitPos < 0)
            {
                destByteIdx++;
                if (destByteIdx < destRow.Length)
                {
                    destByteRef = ref Unsafe.Add(ref destByteRef, 1);
                    destAccum = destByteRef;
                }

                destBitPos = 7;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeRow(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int y,
        int width,
        Jbig2RowTemplate tmpl)
    {
        if (tmpl.RequiresSlowPath)
        {
            DecodeRowOld(ref decoder, bitmap, contexts, y, width, tmpl);
            return;
        }

        DecodeRowFast(ref decoder, bitmap, contexts, y, width, tmpl);
    }

    /// <summary>
    /// Sliding-window row decoder. Above rows refill from source bitmap data; all dy=0 groups
    /// (zero, one, or several) read from a single 64-bit rolling buffer of decoded bits which
    /// also serves as the destination write-back accumulator (flushed every 64 pixels as one
    /// big-endian ulong store). When the template has no dy=0 group at all, the buffer still
    /// runs as a pure write-back register — context simply has no row-0 contribution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DecodeRowFast(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int y,
        int width,
        Jbig2RowTemplate tmpl)
    {
        int groupCount = tmpl.Groups.Length;
        Span<byte> data = bitmap.Data;
        int stride = bitmap.Stride;
        int height = bitmap.Height;
        int dataLength = data.Length;

        // Per-group sliding-window state. Stack-allocated; group counts are tiny (<=4).
        Span<RowValue> rowValues = stackalloc RowValue[groupCount];

        // ── Seed all groups + locate the first dy=0 group ──
        // Groups are sorted by dy ascending, so any dy=0 groups are contiguous at the end.
        // Multiple dy=0 groups happen e.g. for pattern dictionaries: a custom AT pixel at
        // (-PW, 0) becomes its own row-0 group separate from the standard row-0 view.
        // ALL dy=0 groups must extract from the shared rolling buffer of decoded bits, never
        // from the bitmap data we're currently writing.
        int firstRow0Idx = groupCount;
        for (int i = 0; i < groupCount; i++)
        {
            ref readonly Jbig2RowGroupInfo group = ref tmpl.Groups[i];
            rowValues[i] = new RowValue(in group, y, height, stride);

            if (group.Dy == 0)
            {
                if (i < firstRow0Idx)
                {
                    firstRow0Idx = i;
                }
                continue;
            }

            ref RowValue rv = ref rowValues[i];
            if (rv.IsActive == 0)
            {
                continue;
            }

            rv.Value = LoadWindowAtColumn(data, rv.RowByteBase, dataLength, group.MinDx, width);
        }

        ref byte contextRef = ref MemoryMarshal.GetReference(contexts);
        ref byte dataRef = ref MemoryMarshal.GetReference(data);
        ref RowValue rowsRef = ref MemoryMarshal.GetReference(rowValues);

        // The rolling decoded-bit buffer. d(x) is OR'd in at bit 0 and shifted left, so d(j)
        // sits at bit (k - j + 1) after iter k's shift. Each row-0 group has its own ExtractShift
        // / Mask (set by the constructor) so a single (uint)(row0Value >> ExtractShift) & Mask
        // per group lifts the right slice of decoded bits into the right context bits.
        // The buffer always exists, even when the template has no dy=0 group — in that case it
        // contributes nothing to context (the row-0 loop runs 0 times) and serves purely as the
        // batched write-back register.
        ulong row0Value = 0;
        int row0ByteBase = y * stride;

        // ── Hot pixel loop ──
        for (int x = 0; x < width; x++)
        {
            uint context = 0;

            // Above rows: refill if drained, extract context, advance. No d(x) dependency.
            for (int g = 0; g < firstRow0Idx; g++)
            {
                ref RowValue rv = ref Unsafe.Add(ref rowsRef, g);
                if (rv.IsActive == 0)
                {
                    continue;
                }

                if (rv.ExtractsRemaining == 0)
                {
                    rv.Value = LoadWindowAtColumn(data, rv.RowByteBase, dataLength, rv.TopColumn, width);
                    rv.ExtractsRemaining = rv.ExtractsPerWindow;
                }

                context |= (uint)(rv.Value >> rv.ExtractShift) & rv.Mask;
                rv.Value <<= 1;
                rv.TopColumn++;
                rv.ExtractsRemaining--;
            }

            // All row-0 groups read from the shared buffer with their own ExtractShift / Mask.
            for (int g = firstRow0Idx; g < groupCount; g++)
            {
                ref RowValue rv = ref Unsafe.Add(ref rowsRef, g);
                context |= (uint)(row0Value >> rv.ExtractShift) & rv.Mask;
            }

            ref byte contextByteRef = ref Unsafe.Add(ref contextRef, context);
            int bit = decoder.DecodeBit(ref contextByteRef);

            // Pre-shift snapshot has all 64 latest decoded bits at bits 63..0 — used for both
            // the batched write and the advance.
            ulong preShift = row0Value | (uint)bit;

            if ((x & 63) == 63)
            {
                int byteOffset = row0ByteBase + ((x - 63) >> 3);
                BinaryPrimitives.WriteUInt64BigEndian(data.Slice(byteOffset), preShift);
            }

            row0Value = preShift << 1;
        }

        // Trailing 1..63 bits not yet flushed.
        int tail = width & 63;
        if (tail > 0)
        {
            int tailByteBase = row0ByteBase + ((width - tail) >> 3);
            int fullBytes = tail >> 3;
            int tailRem = tail & 7;

            for (int i = 0; i < fullBytes; i++)
            {
                Unsafe.Add(ref dataRef, tailByteBase + i) = (byte)(row0Value >> (tail - 8 * i - 7));
            }

            if (tailRem > 0)
            {
                Unsafe.Add(ref dataRef, tailByteBase + fullBytes) = (byte)(row0Value << (7 - tailRem));
            }
        }
    }

    /// <summary>
    /// Loads 64 source bits big-endian, with bit 63 = source pixel at column <paramref name="column"/>.
    /// Columns outside [0, <paramref name="rowWidth"/>) and bytes outside the data buffer read as 0.
    /// Fast path (no OOB at either edge of the row) avoids per-byte bounds checks and OOB masking;
    /// slow path handles row-edge cases at seed and at the right edge of the last refill.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong LoadWindowAtColumn(
        ReadOnlySpan<byte> data,
        int rowByteBase,
        int dataLength,
        int column,
        int rowWidth)
    {
        int startByte = column >> 3;
        int bitOffset = ((column % 8) + 8) % 8; // safe modulo for negative columns
        int firstByteIdx = rowByteBase + startByte;

        // Fast path: column ≥ 0, all 64 bits inside the row, all 9 source bytes inside data.
        // Single big-endian 64-bit read + at most one bit-alignment shift. No per-byte bounds
        // checks, no OOB masking. This covers every mid-row refill — the common case.
        if (column >= 0 && column + 64 <= rowWidth && firstByteIdx + 9 <= dataLength)
        {
            ulong main = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(firstByteIdx));
            return bitOffset == 0
                ? main
                : (main << bitOffset) | ((ulong)data[firstByteIdx + 8] >> (8 - bitOffset));
        }

        // Slow path: build 9-byte buffer with per-byte OOB-as-zero, then mask edge bits.
        ulong buf = 0;
        for (int i = 0; i < 8; i++)
        {
            buf <<= 8;
            int b = firstByteIdx + i;
            if ((uint)b < (uint)dataLength)
            {
                buf |= data[b];
            }
        }

        ulong result;
        if (bitOffset == 0)
        {
            result = buf;
        }
        else
        {
            int b = firstByteIdx + 8;
            ulong extra = (uint)b < (uint)dataLength ? data[b] : 0UL;
            result = (buf << bitOffset) | (extra >> (8 - bitOffset));
        }

        // Mask out OOB columns to the left of 0.
        if (column < 0)
        {
            int leadOob = -column;
            if (leadOob >= 64)
            {
                return 0;
            }
            result &= (1UL << (64 - leadOob)) - 1;
        }

        // Mask out OOB columns past rowWidth.
        int endCol = column + 63;
        if (endCol >= rowWidth)
        {
            int trailOob = endCol - rowWidth + 1;
            if (trailOob >= 64)
            {
                return 0;
            }
            result &= ~((1UL << trailOob) - 1);
        }

        return result;
    }

    /// <summary>
    /// Self-contained per-group sliding-window state used in the hot loop.
    /// Above rows: <see cref="Value"/> holds source bits top-aligned (bit 63 = column TopColumn).
    /// Row 0:     <see cref="Value"/> is a rolling decoded-bit buffer — d(x) is OR'd in at bit 0
    ///            then shifted left, so bits 8..1 always contain the latest byte MSB-first and
    ///            bits 64..1 hold the latest 64 decoded bits just before the iter-63 shift.
    /// Everything the inner loop needs lives here so we never touch <see cref="Jbig2RowTemplate"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RowValue
    {
        /// <summary>64-bit window. Mutates per pixel.</summary>
        public ulong Value;

        /// <summary>Safe extracts left in the current window. Mutates per pixel; refill at 0.</summary>
        public int ExtractsRemaining;

        /// <summary>Source column at bit 63 of <see cref="Value"/> (above rows). Mutates per pixel.</summary>
        public int TopColumn;

        /// <summary>Context-aligned mask: ((1u &lt;&lt; BC) - 1) &lt;&lt; ContextShift. Already in context bit-space.</summary>
        public readonly uint Mask;

        /// <summary>Shift-right amount mapping Value-space → context-space (= ViewBottom - ContextShift).</summary>
        public readonly byte ExtractShift;

        /// <summary>0 = OOB row (skip in hot loop), 1 = active.</summary>
        public readonly byte IsActive;

        /// <summary>Constant per row: extracts per fully-loaded 64-bit window (= 65 - BC). Refill resets <see cref="ExtractsRemaining"/> to this.</summary>
        public readonly int ExtractsPerWindow;

        /// <summary>Byte offset where the row begins in bitmap.Data — refill source for above rows, write target for row 0.</summary>
        public readonly int RowByteBase;

        /// <summary>
        /// Builds the per-iteration state for one template group at row <paramref name="y"/>.
        /// Caller invariant: row-0 group has MaxDx ≤ -1 so ViewBottom ≥ 1.
        /// </summary>
        public RowValue(in Jbig2RowGroupInfo group, int y, int height, int stride)
        {
            int bitCount = group.MaxDx - group.MinDx + 1;
            int rowY = y + group.Dy;
            bool isCurrentRow = group.Dy == 0;
            bool active = (uint)rowY < (uint)height;

            // Above rows: view is top-aligned (bits [64-BC .. 63]).
            // Row 0:     view sits at bits [-MaxDx .. -MinDx] (= [1..BC] for standard templates),
            //            since d(x) is inserted at bit 0 then shifted left.
            int viewBottom = isCurrentRow ? -group.MaxDx : 64 - bitCount;

            Mask = ((1u << bitCount) - 1) << group.ContextShift;
            ExtractShift = (byte)(viewBottom - group.ContextShift);
            IsActive = (byte)(active ? 1 : 0);
            ExtractsPerWindow = 65 - bitCount;
            RowByteBase = active ? rowY * stride : 0;

            Value = 0;
            ExtractsRemaining = ExtractsPerWindow;
            TopColumn = group.MinDx;
        }
    }

    /// <summary>
    /// Unified fast row decoder supporting both generic and refinement regions.
    /// Groups marked as reference read from the reference bitmap with
    /// <paramref name="refDx"/>/<paramref name="refDy"/> offsets applied.
    /// When <paramref name="usePrediction"/> is set, checks TPGRON implicit value before decoding.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeRefinementRow(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int y,
        int width,
        Jbig2RowTemplate tmpl,
        Jbig2Bitmap reference,
        int refDx,
        int refDy,
        bool usePrediction)
    {
        int groupCount = tmpl.Groups.Length;
        Span<byte> data = bitmap.Data;
        int stride = bitmap.Stride;
        int height = bitmap.Height;

        Span<byte> refData = reference.Data;
        int refStride = reference.Stride;
        int refHeight = reference.Height;
        int refWidth = reference.Width;
        int refDataLength = refData.Length;
        ref byte refSourceRef = ref refData[0];

        // Destination row output
        Span<byte> destRow = bitmap.GetRow(y);
        int destByteIdx = 0;
        int dataLength = data.Length;
        ref byte sourceRef = ref data[0];
        ref byte destByteRef = ref destRow[0];
        byte destAccum = 0;
        int destBitPos = 7;

        for (int x = 0; x < width; x++)
        {
            // TPGRON prediction: check if reference neighborhood is uniform
            if (usePrediction)
            {
                int rx = x - refDx;
                int ry = y - refDy;
                int implicitValue = GetImplicitValue(reference, rx, ry);

                if (implicitValue >= 0)
                {
                    // Write implicit value and advance output position
                    destAccum |= (byte)(implicitValue << destBitPos);
                    destByteRef = destAccum;
                    destBitPos--;

                    if (destBitPos < 0)
                    {
                        destByteIdx++;
                        if (destByteIdx < destRow.Length)
                        {
                            destByteRef = ref Unsafe.Add(ref destByteRef, 1);
                            destAccum = destByteRef;
                        }

                        destBitPos = 7;
                    }

                    continue;
                }
            }

            uint context = 0;

            for (int g = 0; g < groupCount; g++)
            {
                ref readonly Jbig2RowGroupInfo grp = ref tmpl.Groups[g];
                bool isRef = grp.IsReference != 0;

                int effectiveStride;
                int effectiveHeight;
                int effectiveWidth;
                int effectiveDataLength;
                ref byte effectiveSourceRef = ref (isRef ? ref refSourceRef : ref sourceRef);

                int rowY;
                int startX;
                int endX;

                if (isRef)
                {
                    rowY = (y - refDy) + grp.Dy;
                    startX = (x - refDx) + grp.MinDx;
                    endX = (x - refDx) + grp.MaxDx;
                    effectiveStride = refStride;
                    effectiveHeight = refHeight;
                    effectiveWidth = refWidth;
                    effectiveDataLength = refDataLength;
                }
                else
                {
                    rowY = y + grp.Dy;
                    startX = x + grp.MinDx;
                    endX = x + grp.MaxDx;
                    effectiveStride = stride;
                    effectiveHeight = height;
                    effectiveWidth = width;
                    effectiveDataLength = dataLength;
                }

                if ((uint)rowY >= (uint)effectiveHeight)
                {
                    continue;
                }

                byte contextShift = grp.ContextShift;
                int bitCount = (byte)(grp.MaxDx - grp.MinDx + 1);
                uint groupMask = (1u << bitCount) - 1;
                int rowBase = rowY * effectiveStride;
                int bytePos = rowBase + (startX >> 3);
                int bitPos = startX & 7;
                ref var sourceByteRef = ref Unsafe.Add(ref effectiveSourceRef, bytePos);

                uint raw = 0;

                if (bytePos >= 0 && bytePos < effectiveDataLength)
                {
                    raw |= (uint)sourceByteRef << 8;
                }
                if (bytePos + 1 >= 0 && bytePos + 1 < effectiveDataLength)
                {
                    raw |= Unsafe.Add(ref sourceByteRef, 1);
                }

                // Extract the bits window
                int shift = 16 - bitCount - bitPos;
                uint bits = (raw >> shift) & groupMask;

                // Zero out OOB bits
                if (endX >= effectiveWidth)
                {
                    int oobRight = endX - effectiveWidth + 1;
                    bits &= ~((1u << oobRight) - 1);
                }
                if (startX < 0)
                {
                    int validBits = bitCount + startX;
                    if (validBits <= 0)
                    {
                        bits = 0;
                    }
                    else
                    {
                        bits &= (1u << validBits) - 1;
                    }
                }

                context |= bits << contextShift;
            }

            int bit = decoder.DecodeBit(ref contexts[(int)context]);

            // Write decoded bit to output
            destAccum |= (byte)(bit << destBitPos);
            destByteRef = destAccum;
            destBitPos--;

            if (destBitPos < 0)
            {
                destByteIdx++;
                if (destByteIdx < destRow.Length)
                {
                    destByteRef = ref Unsafe.Add(ref destByteRef, 1);
                    destAccum = destByteRef;
                }

                destBitPos = 7;
            }
        }
    }

    /// <summary>
    /// Checks whether a pixel's value can be predicted from the reference bitmap alone
    /// (ITU-T T.88 Section 6.3.5.6, step 5). Returns the implicit pixel value (0 or 1)
    /// if all 8 neighbors in the reference match the center, or -1 if arithmetic decoding
    /// is required.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetImplicitValue(Jbig2Bitmap reference, int rx, int ry)
    {
        int center = reference.GetPixel(rx, ry);

        if (reference.GetPixel(rx - 1, ry - 1) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx, ry - 1) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx + 1, ry - 1) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx - 1, ry) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx + 1, ry) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx - 1, ry + 1) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx, ry + 1) != center)
        {
            return -1;
        }

        if (reference.GetPixel(rx + 1, ry + 1) != center)
        {
            return -1;
        }

        return center;
    }
}
