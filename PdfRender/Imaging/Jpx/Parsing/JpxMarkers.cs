namespace PdfRender.Imaging.Jpx.Parsing;

/// <summary>
/// JPEG 2000 marker codes as defined in ITU-T T.800.
/// </summary>
internal static class JpxMarkers
{
    // Delimiter markers
    public const ushort SOC = 0xFF4F; // Start of codestream
    public const ushort EOC = 0xFF4C; // End of codestream
    public const ushort SOT = 0xFF90; // Start of tile-part
    public const ushort SOD = 0xFF93; // Start of data

    // Fixed information markers
    public const ushort SIZ = 0xFF51; // Image and tile size
    
    // Functional markers
    public const ushort COD = 0xFF52; // Coding style default
    public const ushort COC = 0xFF53; // Coding style component
    public const ushort RGN = 0xFF5E; // Region-of-interest
    public const ushort QCD = 0xFF5C; // Quantization default
    public const ushort QCC = 0xFF5D; // Quantization component
    public const ushort POC = 0xFF5F; // Progression order change
    
    // Pointer markers
    public const ushort TLM = 0xFF55; // Tile-part lengths
    public const ushort PLM = 0xFF57; // Packet length, main header
    public const ushort PLT = 0xFF58; // Packet length, tile-part header
    public const ushort PPM = 0xFF60; // Packed packet headers, main header
    public const ushort PPT = 0xFF61; // Packed packet headers, tile-part header
    
    // In bit stream markers
    public const ushort SOP = 0xFF91; // Start of packet
    public const ushort EPH = 0xFF92; // End of packet header
    
    // Informational markers
    public const ushort CRG = 0xFF63; // Component registration
    public const ushort COM = 0xFF64; // Comment
    
    // JP2 box type codes (used in JP2 file format wrapper)
    public const uint JPEG2000_SIGNATURE = 0x6A502020; // jP box signature ("jP  ")
    public const uint FILETYPE_BOX = 0x66747970; // ftyp
    public const uint HEADER_BOX = 0x6A703268; // jp2h
    public const uint IMAGE_HEADER_BOX = 0x69686472; // ihdr
    public const uint BITS_PER_COMPONENT_BOX = 0x62706363; // bpcc
    public const uint COLOR_SPECIFICATION_BOX = 0x636F6C72; // colr
    public const uint PALETTE_BOX = 0x70636C72; // pclr
    public const uint COMPONENT_MAPPING_BOX = 0x636D6170; // cmap
    public const uint CHANNEL_DEFINITION_BOX = 0x63646566; // cdef
    public const uint RESOLUTION_BOX = 0x72657320; // res 
    public const uint CAPTURE_RESOLUTION_BOX = 0x72657363; // resc
    public const uint DEFAULT_DISPLAY_RESOLUTION_BOX = 0x72657364; // resd
    public const uint CONTIGUOUS_CODESTREAM_BOX = 0x6A703263; // jp2c
    
    /// <summary>
    /// Checks if a marker is a delimiter marker.
    /// </summary>
    public static bool IsDelimiterMarker(ushort marker)
    {
        return marker switch
        {
            SOC or EOC or SOT or SOD => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a marker is a functional marker (has parameters).
    /// </summary>
    public static bool IsFunctionalMarker(ushort marker)
    {
        return marker switch
        {
            SIZ or COD or COC or RGN or QCD or QCC or POC => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a marker is an informational marker.
    /// </summary>
    public static bool IsInformationalMarker(ushort marker)
    {
        return marker switch
        {
            CRG or COM => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a marker is a pointer marker.
    /// </summary>
    public static bool IsPointerMarker(ushort marker)
    {
        return marker switch
        {
            TLM or PLM or PLT or PPM or PPT => true,
            _ => false
        };
    }
}