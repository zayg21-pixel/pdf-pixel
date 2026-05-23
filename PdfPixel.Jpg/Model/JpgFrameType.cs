namespace PdfPixel.Jpg.Model;

/// <summary>
/// Identifies the JPEG Start-of-Frame (SOF) encoding type as defined by the JPEG standard.
/// Values correspond directly to the SOF marker byte (the second byte of the 0xFF 0xCn marker pair).
/// </summary>
public enum JpgFrameType
{
    /// <summary>Frame type not yet determined (no SOF segment parsed).</summary>
    Unknown = 0,

    /// <summary>SOF0: Baseline DCT, Huffman coding.</summary>
    BaselineDct = 0xC0,

    /// <summary>SOF1: Extended sequential DCT, Huffman coding. Supports 12-bit precision and multiple sequential scans.</summary>
    ExtendedSequentialDct = 0xC1,

    /// <summary>SOF2: Progressive DCT, Huffman coding.</summary>
    ProgressiveDct = 0xC2,

    /// <summary>SOF3: Lossless sequential, Huffman coding.</summary>
    LosslessSequential = 0xC3,

    /// <summary>SOF5: Differential sequential DCT, Huffman coding.</summary>
    DifferentialSequentialDct = 0xC5,

    /// <summary>SOF6: Differential progressive DCT, Huffman coding.</summary>
    DifferentialProgressiveDct = 0xC6,

    /// <summary>SOF7: Differential lossless sequential, Huffman coding.</summary>
    DifferentialLosslessSequential = 0xC7,

    /// <summary>SOF9: Extended sequential DCT, arithmetic coding.</summary>
    ExtendedSequentialDctArithmetic = 0xC9,

    /// <summary>SOF10: Progressive DCT, arithmetic coding.</summary>
    ProgressiveDctArithmetic = 0xCA,

    /// <summary>SOF11: Lossless sequential, arithmetic coding.</summary>
    LosslessSequentialArithmetic = 0xCB,

    /// <summary>SOF13: Differential sequential DCT, arithmetic coding.</summary>
    DifferentialSequentialDctArithmetic = 0xCD,

    /// <summary>SOF14: Differential progressive DCT, arithmetic coding.</summary>
    DifferentialProgressiveDctArithmetic = 0xCE,

    /// <summary>SOF15: Differential lossless sequential, arithmetic coding.</summary>
    DifferentialLosslessSequentialArithmetic = 0xCF,
}
