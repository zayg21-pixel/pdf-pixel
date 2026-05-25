namespace PdfPixel.Ccitt;

/// <summary>
/// CCITT T.4 / T.6 white and black run-length Huffman code tables.
/// Includes terminating (0-63), make-up (64-1728), extended make-up (1792-2560) and EOL.
/// Tables are prefix-free per specification.
/// </summary>
internal static class CcittCodeTables
{
    private const int MaxCodeBits = 13;
    private const int LookupTableSize = 1 << MaxCodeBits;

    private static readonly CcittFaxCode[] White =
    [
        // Terminating codes 0-63
        new(8,  0b00110101,        0),
        new(6,  0b000111,          1),
        new(4,  0b0111,            2),
        new(4,  0b1000,            3),
        new(4,  0b1011,            4),
        new(4,  0b1100,            5),
        new(4,  0b1110,            6),
        new(4,  0b1111,            7),
        new(5,  0b10011,           8),
        new(5,  0b10100,           9),
        new(5,  0b00111,           10),
        new(5,  0b01000,           11),
        new(6,  0b001000,          12),
        new(6,  0b000011,          13),
        new(6,  0b110100,          14),
        new(6,  0b110101,          15),
        new(6,  0b101010,          16),
        new(6,  0b101011,          17),
        new(7,  0b0100111,         18),
        new(7,  0b0001100,         19),
        new(7,  0b0001000,         20),
        new(7,  0b0010111,         21),
        new(7,  0b0000011,         22),
        new(7,  0b0000100,         23),
        new(7,  0b0101000,         24),
        new(7,  0b0101011,         25),
        new(7,  0b0010011,         26),
        new(7,  0b0100100,         27),
        new(7,  0b0011000,         28),
        new(8,  0b00000010,        29),
        new(8,  0b00000011,        30),
        new(8,  0b00011010,        31),
        new(8,  0b00011011,        32),
        new(8,  0b00010010,        33),
        new(8,  0b00010011,        34),
        new(8,  0b00010100,        35),
        new(8,  0b00010101,        36),
        new(8,  0b00010110,        37),
        new(8,  0b00010111,        38),
        new(8,  0b00101000,        39),
        new(8,  0b00101001,        40),
        new(8,  0b00101010,        41),
        new(8,  0b00101011,        42),
        new(8,  0b00101100,        43),
        new(8,  0b00101101,        44),
        new(8,  0b00000100,        45),
        new(8,  0b00000101,        46),
        new(8,  0b00001010,        47),
        new(8,  0b00001011,        48),
        new(8,  0b01010010,        49),
        new(8,  0b01010011,        50),
        new(8,  0b01010100,        51),
        new(8,  0b01010101,        52),
        new(8,  0b00100100,        53),
        new(8,  0b00100101,        54),
        new(8,  0b01011000,        55),
        new(8,  0b01011001,        56),
        new(8,  0b01011010,        57),
        new(8,  0b01011011,        58),
        new(8,  0b01001010,        59),
        new(8,  0b01001011,        60),
        new(8,  0b00110010,        61),
        new(8,  0b00110011,        62),
        new(8,  0b00110100,        63),
        // Make-up codes 64-1728
        new(5,  0b11011,           64,   true),
        new(5,  0b10010,           128,  true),
        new(6,  0b010111,          192,  true),
        new(7,  0b0110111,         256,  true),
        new(8,  0b00110110,        320,  true),
        new(8,  0b00110111,        384,  true),
        new(8,  0b01100100,        448,  true),
        new(8,  0b01100101,        512,  true),
        new(8,  0b01101000,        576,  true),
        new(8,  0b01100111,        640,  true),
        new(9,  0b011001100,       704,  true),
        new(9,  0b011001101,       768,  true),
        new(9,  0b011010010,       832,  true),
        new(9,  0b011010011,       896,  true),
        new(9,  0b011010100,       960,  true),
        new(9,  0b011010101,       1024, true),
        new(9,  0b011010110,       1088, true),
        new(9,  0b011010111,       1152, true),
        new(9,  0b011011000,       1216, true),
        new(9,  0b011011001,       1280, true),
        new(9,  0b011011010,       1344, true),
        new(9,  0b011011011,       1408, true),
        new(9,  0b010011000,       1472, true),
        new(9,  0b010011001,       1536, true),
        new(9,  0b010011010,       1600, true),
        new(6,  0b011000,          1664, true),
        new(9,  0b010011011,       1728, true),
        // Extended make-up 1792-2560
        new(11, 0b00000001000,     1792, true),
        new(11, 0b00000001100,     1856, true),
        new(11, 0b00000001101,     1920, true),
        new(12, 0b000000010010,    1984, true),
        new(12, 0b000000010011,    2048, true),
        new(12, 0b000000010100,    2112, true),
        new(12, 0b000000010101,    2176, true),
        new(12, 0b000000010110,    2240, true),
        new(12, 0b000000010111,    2304, true),
        new(12, 0b000000011100,    2368, true),
        new(12, 0b000000011101,    2432, true),
        new(12, 0b000000011110,    2496, true),
        new(12, 0b000000011111,    2560, true),
        // EOL
        new(12, 0b000000000001,    0,    false, true)
    ];

    private static readonly CcittFaxCode[] Black =
    [
        // Terminating codes 0-63
        new(10, 0b0000110111,      0),
        new(3,  0b010,             1),
        new(2,  0b11,              2),
        new(2,  0b10,              3),
        new(3,  0b011,             4),
        new(4,  0b0011,            5),
        new(4,  0b0010,            6),
        new(5,  0b00011,           7),
        new(6,  0b000101,          8),
        new(6,  0b000100,          9),
        new(7,  0b0000100,         10),
        new(7,  0b0000101,         11),
        new(7,  0b0000111,         12),
        new(8,  0b00000100,        13),
        new(8,  0b00000111,        14),
        new(9,  0b000011000,       15),
        new(10, 0b0000010111,      16),
        new(10, 0b0000011000,      17),
        new(10, 0b0000001000,      18),
        new(11, 0b00001100111,     19),
        new(11, 0b00001101000,     20),
        new(11, 0b00001101100,     21),
        new(11, 0b00000110111,     22),
        new(11, 0b00000101000,     23),
        new(11, 0b00000010111,     24),
        new(11, 0b00000011000,     25),
        new(12, 0b000011001010,    26),
        new(12, 0b000011001011,    27),
        new(12, 0b000011001100,    28),
        new(12, 0b000011001101,    29),
        new(12, 0b000001101000,    30),
        new(12, 0b000001101001,    31),
        new(12, 0b000001101010,    32),
        new(12, 0b000001101011,    33),
        new(12, 0b000011010010,    34),
        new(12, 0b000011010011,    35),
        new(12, 0b000011010100,    36),
        new(12, 0b000011010101,    37),
        new(12, 0b000011010110,    38),
        new(12, 0b000011010111,    39),
        new(12, 0b000001101100,    40),
        new(12, 0b000001101101,    41),
        new(12, 0b000011011010,    42),
        new(12, 0b000011011011,    43),
        new(12, 0b000001010100,    44),
        new(12, 0b000001010101,    45),
        new(12, 0b000001010110,    46),
        new(12, 0b000001010111,    47),
        new(12, 0b000001100100,    48),
        new(12, 0b000001100101,    49),
        new(12, 0b000001010010,    50),
        new(12, 0b000001010011,    51),
        new(12, 0b000000100100,    52),
        new(12, 0b000000110111,    53),
        new(12, 0b000000111000,    54),
        new(12, 0b000000100111,    55),
        new(12, 0b000000101000,    56),
        new(12, 0b000001011000,    57),
        new(12, 0b000001011001,    58),
        new(12, 0b000000101011,    59),
        new(12, 0b000000101100,    60),
        new(12, 0b000001011010,    61),
        new(12, 0b000001100110,    62),
        new(12, 0b000001100111,    63),
        // Make-up codes 64-1728
        new(10, 0b0000001111,      64,   true),
        new(12, 0b000011001000,    128,  true),
        new(12, 0b000011001001,    192,  true),
        new(12, 0b000001011011,    256,  true),
        new(12, 0b000000110011,    320,  true),
        new(12, 0b000000110100,    384,  true),
        new(12, 0b000000110101,    448,  true),
        new(13, 0b0000001101100,   512,  true),
        new(13, 0b0000001101101,   576,  true),
        new(13, 0b0000001001010,   640,  true),
        new(13, 0b0000001001011,   704,  true),
        new(13, 0b0000001001100,   768,  true),
        new(13, 0b0000001001101,   832,  true),
        new(13, 0b0000001110010,   896,  true),
        new(13, 0b0000001110011,   960,  true),
        new(13, 0b0000001110100,   1024, true),
        new(13, 0b0000001110101,   1088, true),
        new(13, 0b0000001110110,   1152, true),
        new(13, 0b0000001110111,   1216, true),
        new(13, 0b0000001010010,   1280, true),
        new(13, 0b0000001010011,   1344, true),
        new(13, 0b0000001010100,   1408, true),
        new(13, 0b0000001010101,   1472, true),
        new(13, 0b0000001011010,   1536, true),
        new(13, 0b0000001011011,   1600, true),
        new(13, 0b0000001100100,   1664, true),
        new(13, 0b0000001100101,   1728, true),
        // Extended make-up 1792-2560
        new(11, 0b00000001000,     1792, true),
        new(11, 0b00000001100,     1856, true),
        new(11, 0b00000001101,     1920, true),
        new(12, 0b000000010010,    1984, true),
        new(12, 0b000000010011,    2048, true),
        new(12, 0b000000010100,    2112, true),
        new(12, 0b000000010101,    2176, true),
        new(12, 0b000000010110,    2240, true),
        new(12, 0b000000010111,    2304, true),
        new(12, 0b000000011100,    2368, true),
        new(12, 0b000000011101,    2432, true),
        new(12, 0b000000011110,    2496, true),
        new(12, 0b000000011111,    2560, true),
        // EOL
        new(12, 0b000000000001,    0,    false, true)
    ];

    public static readonly CcittFaxCode[] WhiteLookup = BuildLookupTable(White);
    public static readonly CcittFaxCode[] BlackLookup = BuildLookupTable(Black);

    private static CcittFaxCode[] BuildLookupTable(CcittFaxCode[] table)
    {
        var lookup = new CcittFaxCode[LookupTableSize];

        for (int i = 0; i < table.Length; i++)
        {
            CcittFaxCode code = table[i];
            int prefix = code.Code << MaxCodeBits - code.BitLength;
            int fillCount = 1 << MaxCodeBits - code.BitLength;

            for (int j = 0; j < fillCount; j++)
            {
                lookup[prefix | j] = code;
            }
        }

        return lookup;
    }
}
