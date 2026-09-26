namespace PdfPixel.Tiff.Parsing;

/// <summary>
/// Tag codes read from a TIFF image file directory.
/// </summary>
internal enum TiffTag
{
    NewSubfileType = 254,
    ImageWidth = 256,
    ImageLength = 257,
    BitsPerSample = 258,
    Compression = 259,
    PhotometricInterpretation = 262,
    FillOrder = 266,
    StripOffsets = 273,
    Orientation = 274,
    SamplesPerPixel = 277,
    RowsPerStrip = 278,
    StripByteCounts = 279,
    PlanarConfiguration = 284,
    T4Options = 292,
    Predictor = 317,
    ColorMap = 320,
    TileWidth = 322,
    TileLength = 323,
    TileOffsets = 324,
    TileByteCounts = 325,
    SubIfds = 330,
    ExtraSamples = 338,
    SampleFormat = 339,
    JpegTables = 347,
    JpegProc = 512,
    JpegInterchangeFormat = 513,
    JpegInterchangeFormatLength = 514,
    JpegRestartInterval = 515,
    JpegQTables = 519,
    JpegDcTables = 520,
    JpegAcTables = 521,
    YCbCrCoefficients = 529,
    YCbCrSubSampling = 530,
    ReferenceBlackWhite = 532,
    IccProfile = 34675
}
