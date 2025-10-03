using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Jpg.Quantization
{
    /// <summary>
    /// Represents a single JPEG quantization table (8x8) parsed from a DQT segment.
    /// Supports 8-bit (Pq=0) and 16-bit (Pq=1) precision entries.
    /// </summary>
    internal sealed class JpgQuantizationTable
    {
        public int TableId { get; set; }
        public bool Is16Bit { get; set; }
        public ushort[] Entries { get; private set; } = new ushort[64];

        public static List<JpgQuantizationTable> ParseDqtPayload(ReadOnlySpan<byte> payload)
        {
            var list = new List<JpgQuantizationTable>();
            int offset = 0;
            while (offset < payload.Length)
            {
                if (offset + 1 >= payload.Length)
                {
                    throw new ArgumentException("Invalid DQT segment: truncated specifier");
                }

                byte pqTq = payload[offset++];
                int precision = (pqTq >> 4) & 0x0F; // 0 or 1
                int tableId = pqTq & 0x0F; // 0..3
                bool is16 = precision != 0;

                var table = new JpgQuantizationTable
                {
                    TableId = tableId,
                    Is16Bit = is16
                };

                int step = is16 ? 2 : 1;
                int required = 64 * step;
                if (offset + required > payload.Length)
                {
                    throw new ArgumentException("Invalid DQT segment: table entries truncated");
                }

                if (!is16)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        table.Entries[i] = payload[offset + i];
                    }
                    offset += 64;
                }
                else
                {
                    for (int i = 0; i < 64; i++)
                    {
                        table.Entries[i] = (ushort)((payload[offset + 2 * i] << 8) | payload[offset + 2 * i + 1]);
                    }
                    offset += 128;
                }

                list.Add(table);
            }

            return list;
        }
    }
}
