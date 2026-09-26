using System.Collections.Generic;

namespace PdfPixel.Tiff.Model;

/// <summary>
/// Image file directory (IFD) of a TIFF file: the tags describing one image.
/// </summary>
public sealed class TiffDirectory
{
    /// <summary>
    /// Gets or sets a value indicating whether multi-byte values in the file are little-endian.
    /// </summary>
    public bool IsLittleEndian { get; set; }

    /// <summary>
    /// Gets or sets the kind of data this directory holds (NewSubfileType).
    /// </summary>
    public TiffSubfileType SubfileType { get; set; }

    /// <summary>
    /// Gets the child directories this directory links (SubIFDs), in file order.
    /// </summary>
    public List<TiffDirectory> SubDirectories { get; } = [];

    /// <summary>
    /// Gets or sets the image width in pixels (ImageWidth).
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height in pixels (ImageLength).
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the bit depth of each sample (BitsPerSample).
    /// </summary>
    public int[] BitsPerSample { get; set; } = [1];

    /// <summary>
    /// Gets or sets the number of samples per pixel (SamplesPerPixel).
    /// </summary>
    public int SamplesPerPixel { get; set; } = 1;

    /// <summary>
    /// Gets or sets the compression scheme (Compression).
    /// </summary>
    public TiffCompression Compression { get; set; } = TiffCompression.Uncompressed;

    /// <summary>
    /// Gets or sets the color model of the samples (PhotometricInterpretation), or null when the tag is absent.
    /// </summary>
    public TiffPhotometric? Photometric { get; set; }

    /// <summary>
    /// Gets or sets the placement of the stored rows and columns (Orientation), or null when the tag is absent.
    /// </summary>
    public TiffOrientation? Orientation { get; set; }

    /// <summary>
    /// Gets or sets the embedded ICC profile (InterColorProfile), or null when the tag is absent.
    /// </summary>
    public byte[]? IccProfile { get; set; }

    /// <summary>
    /// Gets or sets how the samples of a pixel are arranged (PlanarConfiguration).
    /// </summary>
    public TiffPlanarConfiguration PlanarConfiguration { get; set; } = TiffPlanarConfiguration.Chunky;

    /// <summary>
    /// Gets or sets the prediction applied before compression (Predictor).
    /// </summary>
    public TiffPredictor Predictor { get; set; } = TiffPredictor.NoPrediction;

    /// <summary>
    /// Gets or sets the bit order of the compressed data (FillOrder).
    /// </summary>
    public TiffFillOrder FillOrder { get; set; } = TiffFillOrder.MostSignificantBitFirst;

    /// <summary>
    /// Gets or sets the numeric interpretation of the samples (SampleFormat).
    /// </summary>
    public TiffSampleFormat SampleFormat { get; set; } = TiffSampleFormat.UnsignedInteger;

    /// <summary>
    /// Gets or sets the number of rows in each strip (RowsPerStrip), or null when the tag is absent.
    /// </summary>
    public long? RowsPerStrip { get; set; }

    /// <summary>
    /// Gets or sets the file offset of each strip (StripOffsets), or null for a tiled image.
    /// </summary>
    public long[]? StripOffsets { get; set; }

    /// <summary>
    /// Gets or sets the compressed byte count of each strip (StripByteCounts), or null for a tiled image.
    /// </summary>
    public long[]? StripByteCounts { get; set; }

    /// <summary>
    /// Gets or sets the tile width in pixels (TileWidth), or null for a stripped image.
    /// </summary>
    public int? TileWidth { get; set; }

    /// <summary>
    /// Gets or sets the tile height in pixels (TileLength), or null for a stripped image.
    /// </summary>
    public int? TileHeight { get; set; }

    /// <summary>
    /// Gets or sets the file offset of each tile (TileOffsets), or null for a stripped image.
    /// </summary>
    public long[]? TileOffsets { get; set; }

    /// <summary>
    /// Gets or sets the compressed byte count of each tile (TileByteCounts), or null for a stripped image.
    /// </summary>
    public long[]? TileByteCounts { get; set; }

    /// <summary>
    /// Gets or sets the CCITT Group 3 option bits (T4Options).
    /// </summary>
    public int T4Options { get; set; }

    /// <summary>
    /// Gets or sets the palette (ColorMap): all red values, then all green, then all blue, each 16-bit.
    /// </summary>
    public ushort[]? ColorMap { get; set; }

    /// <summary>
    /// Gets or sets the meaning of each sample beyond the photometric ones (ExtraSamples).
    /// </summary>
    public TiffExtraSample[] ExtraSamples { get; set; } = [];

    /// <summary>
    /// Gets or sets the quantization and Huffman tables shared by every JPEG strip or tile (JPEGTables).
    /// </summary>
    public byte[]? JpegTables { get; set; }

    /// <summary>
    /// Gets or sets the tags of the original JPEG scheme, or null when the directory has none.
    /// </summary>
    public TiffOldJpegParameters? OldJpeg { get; set; }

    /// <summary>
    /// Gets or sets the luma coefficients of red, green and blue (YCbCrCoefficients), or null when the tag is absent.
    /// </summary>
    public double[]? YCbCrCoefficients { get; set; }

    /// <summary>
    /// Gets or sets the horizontal and vertical chroma subsampling factors (YCbCrSubSampling), or null when the tag is absent.
    /// </summary>
    public int[]? YCbCrSubsampling { get; set; }

    /// <summary>
    /// Gets or sets the black and white reference codes of each component (ReferenceBlackWhite), or null when the tag is absent.
    /// </summary>
    public double[]? ReferenceBlackWhite { get; set; }
}
