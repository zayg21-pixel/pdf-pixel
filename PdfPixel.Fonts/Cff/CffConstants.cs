namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Well-known byte values and encoding constants used throughout CFF parsing and writing.
/// </summary>
internal static class CffConstants
{
    // CFF DICT operator bytes
    public const byte DictOperatorMax = 21;
    public const byte DictOperatorEscape = 12;

    // CFF DICT/charstring operand marker bytes
    public const byte OperandShortInt = 28;
    public const byte OperandLongInt = 29;
    public const byte OperandRealNumber = 30;
    public const byte OperandIntLow = 32;
    public const byte OperandIntHigh = 246;
    public const byte OperandPositiveIntStart = 247;
    public const byte OperandPositiveIntEnd = 250;
    public const byte OperandNegativeIntStart = 251;
    public const byte OperandNegativeIntEnd = 254;

    // CFF integer operand encoding biases
    public const int SingleByteIntegerBias = 139;
    public const int TwoByteIntegerBias = 108;

    // CFF DICT real number nibble codes
    public const byte RealNibbleDecimalPoint = 0xA;
    public const byte RealNibblePositiveExponent = 0xB;
    public const byte RealNibbleNegativeExponent = 0xC;
    public const byte RealNibbleReserved = 0xD;
    public const byte RealNibbleMinus = 0xE;
    public const byte RealNibbleTerminator = 0xF;

    // CFF Top DICT operator codes (non-escaped)
    public const byte TopDictOperatorWeight = 4;
    public const byte TopDictOperatorFontBBox = 5;
    public const byte TopDictOperatorCharset = 15;
    public const byte TopDictOperatorEncoding = 16;
    public const byte TopDictOperatorCharStrings = 17;
    public const byte TopDictOperatorPrivate = 18;

    // CFF Top DICT operator codes (escaped, 12 xx)
    public const byte TopDictEscapedOperatorItalicAngle = 2;
    public const byte TopDictEscapedOperatorPaintType = 5;
    public const byte TopDictEscapedOperatorCharstringType = 6;
    public const byte TopDictEscapedOperatorFontMatrix = 7;
    public const byte TopDictEscapedOperatorStrokeWidth = 8;
    public const byte TopDictEscapedOperatorRos = 30;
    public const byte TopDictEscapedOperatorFdArray = 36;
    public const byte TopDictEscapedOperatorFdSelect = 37;

    // CFF Private DICT operator codes (non-escaped)
    public const byte PrivateDictOperatorSubrs = 19;
    public const byte PrivateDictOperatorDefaultWidthX = 20;
    public const byte PrivateDictOperatorNominalWidthX = 21;

    // CFF charset predefined IDs (used as the CharsetOffset value when less than 3)
    public const int CharsetPredefinedIsoAdobe = 0;
    public const int CharsetPredefinedExpert = 1;
    public const int CharsetPredefinedExpertSubset = 2;

    // CFF encoding predefined IDs (used as the EncodingOffset value when less than 2)
    public const int EncodingPredefinedStandard = 0;
    public const int EncodingPredefinedExpert = 1;

    // CFF encoding format byte layout: low 7 bits are the table format (0 or 1), high bit flags a
    // trailing supplement.
    public const byte EncodingFormatMask = 0x7F;
    public const byte EncodingSupplementFlag = 0x80;

    // Type2 charstring operator/operand marker bytes
    public const byte CharStringOperatorMax = 31;
    public const byte CharStringOperandFixed = 255;

    // Type2 charstring operator codes (non-escaped)
    public const byte CharStringOperatorHstem = 1;
    public const byte CharStringOperatorVstem = 3;
    public const byte CharStringOperatorVmoveto = 4;
    public const byte CharStringOperatorRlineto = 5;
    public const byte CharStringOperatorHlineto = 6;
    public const byte CharStringOperatorVlineto = 7;
    public const byte CharStringOperatorRrcurveto = 8;
    public const byte CharStringOperatorCallsubr = 10;
    public const byte CharStringOperatorReturn = 11;
    public const byte CharStringOperatorEndchar = 14;
    public const byte CharStringOperatorHstemhm = 18;
    public const byte CharStringOperatorHintmask = 19;
    public const byte CharStringOperatorCntrmask = 20;
    public const byte CharStringOperatorRmoveto = 21;
    public const byte CharStringOperatorHmoveto = 22;
    public const byte CharStringOperatorVstemhm = 23;
    public const byte CharStringOperatorRcurveline = 24;
    public const byte CharStringOperatorRlinecurve = 25;
    public const byte CharStringOperatorVvcurveto = 26;
    public const byte CharStringOperatorHhcurveto = 27;
    public const byte CharStringOperatorCallgsubr = 29;
    public const byte CharStringOperatorVhcurveto = 30;
    public const byte CharStringOperatorHvcurveto = 31;

    // Type2 charstring operator codes (escaped, 12 xx) -- flex family only; everything else
    // (arithmetic/storage operators) is essentially never emitted by real-world fonts.
    public const byte CharStringEscapedOperatorHflex = 34;
    public const byte CharStringEscapedOperatorFlex = 35;
    public const byte CharStringEscapedOperatorHflex1 = 36;
    public const byte CharStringEscapedOperatorFlex1 = 37;

    // Local/global subroutine index bias thresholds (CFF spec, Type2 Charstring Format section 4.7)
    public const int SubrBiasSmallCountThreshold = 1240;
    public const int SubrBiasMediumCountThreshold = 33900;
    public const int SubrBiasSmall = 107;
    public const int SubrBiasMedium = 1131;
    public const int SubrBiasLarge = 32768;

    // Type2 charstring evaluation limits
    public const int MaxSubroutineNestingDepth = 10;
}
