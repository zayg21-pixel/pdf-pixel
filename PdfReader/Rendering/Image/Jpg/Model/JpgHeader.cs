using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Jpg.Model
{
    /// <summary>
    /// Parsed JPEG header metadata required to drive a custom JPEG decoder and color handling (including CMYK/YCCK).
    /// Does not include entropy-coded scan data; only structural and metadata information from header segments.
    /// </summary>
    internal sealed class JpgHeader
    {
        // SOF information
        public bool IsBaseline { get; set; }
        public bool IsProgressive { get; set; }
        public byte SamplePrecision { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ComponentCount { get; set; }

        /// <summary>
        /// Components described by SOF (e.g., 1 = Gray, 3 = YCbCr, 4 = CMYK).
        /// </summary>
        public List<JpgComponent> Components { get; } = new List<JpgComponent>();

        // Marker presence
        public bool HasHuffmanTables { get; set; }
        public bool HasQuantizationTables { get; set; }

        // Collected tables (optional; present if DHT/DQT segments encountered before SOS)
        public List<Huffman.JpgHuffmanTable> HuffmanTables { get; } = new List<Huffman.JpgHuffmanTable>();
        public List<Quantization.JpgQuantizationTable> QuantizationTables { get; } = new List<Quantization.JpgQuantizationTable>();

        // Restart interval from DRI; 0 means no restarts
        public int RestartInterval { get; set; }

        // Parsed scan specifications (from SOS)
        public List<JpgScanSpec> Scans { get; } = new List<JpgScanSpec>();

        // Content start (offset of entropy-coded data following the first SOS header)
        public int ContentOffset { get; set; } = -1;

        // APP0 (JFIF)
        public bool IsJfif { get; set; }
        public ushort JfifVersion { get; set; }
        public byte DensityUnits { get; set; }
        public ushort XDensity { get; set; }
        public ushort YDensity { get; set; }

        // APP1 (Exif)
        public bool IsExif { get; set; }

        // APP14 (Adobe) - used to signal color transform: 0 = Unknown/CMYK, 1 = YCbCr, 2 = YCCK
        public bool HasAdobeApp14 { get; set; }
        public byte AdobeColorTransform { get; set; }

        // ICC profile (APP2 ICC_PROFILE). Segments can be reassembled by SequenceNumber/TotalSegments.
        public bool HasIccProfile { get; set; }
        public List<IccSegmentInfo> IccProfileSegments { get; } = new List<IccSegmentInfo>();
    }
}
