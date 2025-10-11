using System;
using System.Text;

namespace PdfReader
{
    public static class PdfTokens
    {
        // PDF Keywords
        public static ReadOnlySpan<byte> Endstream => "endstream"u8;
        public static ReadOnlySpan<byte> Obj => "obj"u8;
        public static ReadOnlySpan<byte> Endobj => "endobj"u8;
        public static ReadOnlySpan<byte> Xref => "xref"u8;
        public static ReadOnlySpan<byte> Trailer => "trailer"u8;
        public static ReadOnlySpan<byte> Startxref => "startxref"u8;
        public static ReadOnlySpan<byte> Stream => "stream"u8;
        public static ReadOnlySpan<byte> R => "R"u8;
        
        // Dictionary Delimiters
        public static ReadOnlySpan<byte> DictStart => "<<"u8;
        public static ReadOnlySpan<byte> DictEnd => ">>"u8;
        public static ReadOnlySpan<byte> ArrayStart => "["u8;
        public static ReadOnlySpan<byte> ArrayEnd => "]"u8;
        
        // Dictionary Key Constants (as strings for dictionary lookups)
        public const string RootKey = "/Root";
        public const string PagesKey = "/Pages";
        public const string PageKey = "/Page";
        public const string TypeKey = "/Type";
        public const string CountKey = "/Count";
        public const string KidsKey = "/Kids";
        public const string MediaBoxKey = "/MediaBox";
        public const string ContentsKey = "/Contents";
        public const string ResourcesKey = "/Resources";
        public const string CatalogKey = "/Catalog";
        public const string FilterKey = "/Filter";
        public const string RotateKey = "/Rotate";
        public const string CropBoxKey = "/CropBox";
        public const string ParentKey = "/Parent";
        public const string LengthKey = "/Length"; // also used in encryption dictionary
        public const string VKey = "/V";             // encryption version
        public const string RKey = "/R";             // encryption revision
        public const string PKey = "/P";             // permissions
        public const string EncryptMetadataKey = "/EncryptMetadata"; // encrypt metadata flag
        public const string OKey = "/O";             // used in linearization and encryption dictionaries
        public const string UKey = "/U";             // encryption dictionary user entry
        public const string OEKey = "/OE";           // encryption dictionary owner encrypted key (R>=5)
        public const string UEKey = "/UE";           // encryption dictionary user encrypted key (R>=5)
        public const string PermsKey = "/Perms";     // encryption dictionary permissions (R>=5)
        public const string StmFKey = "/StmF";       // encryption stream crypt filter name
        public const string StrFKey = "/StrF";       // encryption string crypt filter name
        public const string EffKey = "/EFF";         // encryption embedded file crypt filter name
        public const string CFKey = "/CF";           // encryption crypt filter dictionary

        // Image/XObject specific
        public const string DecodeKey = "/Decode";
        public const string DecodeParmsKey = "/DecodeParms";
        public const string PredictorKey = "/Predictor";
        public const string ColorsKey = "/Colors";
        public const string ColumnsKey = "/Columns";
        public const string KKey = "/K";
        public const string EndOfLineKey = "/EndOfLine";
        public const string EncodedByteAlignKey = "/EncodedByteAlign";
        public const string RowsKey = "/Rows";
        public const string EndOfBlockKey = "/EndOfBlock";
        public const string BlackIs1Key = "/BlackIs1";
        public const string DamagedRowsBeforeErrorKey = "/DamagedRowsBeforeError";
        public const string EarlyChangeKey = "/EarlyChange";
        public const string ColorTransformKey = "/ColorTransform";
        public const string NameKey = "/Name";

        // Font-related Dictionary Keys
        public const string FontKey = "/Font";
        public const string BaseFontKey = "/BaseFont";
        public const string SubtypeKey = "/Subtype";
        public const string EncodingKey = "/Encoding";
        public const string BaseEncodingKey = "/BaseEncoding";
        public const string DifferencesKey = "/Differences";
        public const string FirstCharKey = "/FirstChar";
        public const string LastCharKey = "/LastChar";
        public const string WidthsKey = "/Widths";
        public const string FontDescriptorKey = "/FontDescriptor";
        public const string FontFileKey = "/FontFile";
        public const string FontFile2Key = "/FontFile2";
        public const string FontFile3Key = "/FontFile3";
        public const string FontNameKey = "/FontName";
        public const string FlagsKey = "/Flags";
        public const string FontBBoxKey = "/FontBBox";
        public const string ItalicAngleKey = "/ItalicAngle";
        public const string AscentKey = "/Ascent";
        public const string DescentKey = "/Descent";
        public const string CapHeightKey = "/CapHeight";
        public const string XHeightKey = "/XHeight";
        public const string StemVKey = "/StemV";
        public const string StemHKey = "/StemH";
        public const string AvgWidthKey = "/AvgWidth";
        public const string MaxWidthKey = "/MaxWidth";
        public const string MissingWidthKey = "/MissingWidth";
        public const string FontFamilyKey = "/FontFamily";     // PDF 1.5+
        public const string FontStretchKey = "/FontStretch";   // Name: UltraCondensed..UltraExpanded
        public const string FontWeightKey = "/FontWeight";     // 100..900
        public const string LeadingKey = "/Leading";           // Preferred line height
        public const string CharSetKey = "/CharSet";           // Glyph names present (string)
        public const string StemSnapHKey = "/StemSnapH";       // Array of stem widths (horizontal)
        public const string StemSnapVKey = "/StemSnapV";       // Array of stem widths (vertical)
        public const string PanoseKey = "/Panose";             // 12-byte classification
        public const string CharProcsKey = "/CharProcs";
        public const string FontMatrixKey = "/FontMatrix";
        public const string ToUnicodeKey = "/ToUnicode";
        public const string CIDFontTypeKey = "/CIDFontType";
        public const string CIDSystemInfoKey = "/CIDSystemInfo";
        public const string RegistryKey = "/Registry";
        public const string OrderingKey = "/Ordering";
        public const string SupplementKey = "/Supplement";
        public const string DWKey = "/DW";
        public const string WKey = "/W";
        public const string DescendantFontsKey = "/DescendantFonts";
        public const string CIDToGIDMapKey = "/CIDToGIDMap";
        public const string CMapNameKey = "/CMapName";        // For CMap streams
        public const string CMapTypeValue = "/CMap";           // /Type /CMap

        // Font Types
        public const string Type1FontKey = "/Type1";
        public const string TrueTypeFontKey = "/TrueType";
        public const string Type3FontKey = "/Type3";
        public const string Type0FontKey = "/Type0";
        public const string CIDFontType0Key = "/CIDFontType0";
        public const string CIDFontType2Key = "/CIDFontType2";
        public const string MMType1FontKey = "/MMType1";
        
        // Font descriptor type
        public const string FontDescriptorTypeKey = "/FontDescriptor";

        // Standard Encodings
        public const string StandardEncodingKey = "/StandardEncoding";
        public const string MacRomanEncodingKey = "/MacRomanEncoding";
        public const string WinAnsiEncodingKey = "/WinAnsiEncoding";
        public const string MacExpertEncodingKey = "/MacExpertEncoding";
        
        // CID Font Encodings (CMaps)
        public const string IdentityHEncodingKey = "/Identity-H";  // Horizontal identity CMap for CID fonts
        public const string IdentityVEncodingKey = "/Identity-V";  // Vertical identity CMap for CID fonts
        
        // Predefined Unicode CMaps (CJK) - treated specially when ToUnicode is missing
        public const string UniJIS_UTF16_H_EncodingKey = "/UniJIS-UTF16-H";
        public const string UniJIS_UTF16_V_EncodingKey = "/UniJIS-UTF16-V";
        public const string UniGB_UTF16_H_EncodingKey = "/UniGB-UTF16-H";
        public const string UniGB_UTF16_V_EncodingKey = "/UniGB-UTF16-V";
        public const string UniCNS_UTF16_H_EncodingKey = "/UniCNS-UTF16-H";
        public const string UniCNS_UTF16_V_EncodingKey = "/UniCNS-UTF16-V";
        public const string UniKS_UTF16_H_EncodingKey = "/UniKS-UTF16-H";
        public const string UniKS_UTF16_V_EncodingKey = "/UniKS-UTF16-V";
        
        // Graphics State Dictionary Keys
        public const string LineWidthKey = "/LW";
        public const string LineCapKey = "/LC";
        public const string LineJoinKey = "/LJ";
        public const string MiterLimitKey = "/ML";
        public const string DashPatternKey = "/D";
        public const string StrokeAlphaKey = "/CA";
        public const string FillAlphaKey = "/ca";
        public const string BlendModeKey = "/BM";
        public const string MatrixKey = "/Matrix";
        public const string CTMKey = "/CTM";
        
        // Resource Dictionary Keys
        public const string ColorSpaceKey = "/ColorSpace";
        public const string ExtGStateKey = "/ExtGState";
        public const string XObjectKey = "/XObject";
        public const string ProcSetKey = "/ProcSet";
        public const string ShadingKey = "/Shading";
        
        // XObject Dictionary Keys
        public const string WidthKey = "/Width";
        public const string HeightKey = "/Height";
        public const string BitsPerComponentKey = "/BitsPerComponent";
        public const string BBoxKey = "/BBox";
        public const string ImageMaskKey = "/ImageMask";
        public const string MaskKey = "/Mask";
        public const string InterpolateKey = "/Interpolate";
        public const string IntentKey = "/Intent";
        public const string MatteKey = "/Matte"; // Soft mask image dematting color components

        // Shading Dictionary Keys
        public const string ShadingTypeKey = "/ShadingType"; // 1..7; we support 2 (axial) and 3 (radial) minimally
        public const string CoordsKey = "/Coords";           // coordinates array
        public const string C0Key = "/C0";                   // starting color components
        public const string C1Key = "/C1";                   // ending color components
        public const string FunctionKey = "/Function";       // function for color (not implemented)
        public const string DomainKey = "/Domain";           // optional domain for input variable
        public const string FunctionTypeKey = "/FunctionType"; // function dictionaries only
        public const string FnNKey = "/N";                   // exponent for function type 2 (distinct from object-stream /N)
        public const string FunctionsKey = "/Functions";     // stitching function sub-functions
        public const string BoundsKey = "/Bounds";           // stitching function bounds
        public const string EncodeKey = "/Encode";           // stitching function encode array
        public const string ExtendKey = "/Extend";           // extend flags for shadings

        // XObject Subtypes
        public const string ImageSubtype = "/Image";
        public const string FormSubtype = "/Form";
        public const string PSSubtype = "/PS";
        
        // Extended Graphics State Dictionary Keys (transparency)
        public const string SoftMaskKey = "/SMask";
        public const string GroupKey = "/Group";
        public const string KnockoutKey = "/TK";
        public const string OverprintModeKey = "/OPM";
        public const string OverprintStrokeKey = "/OP";
        public const string OverprintFillKey = "/op";
        public const string AlphaConstantKey = "/AIS";
        public const string GroupSubtypeKey = "/S";
        public const string GroupColorSpaceKey = "/CS";
        public const string GroupIsolatedKey = "/I";
        public const string GroupKnockoutKey = "/K";
        
        // Soft Mask Dictionary Keys
        public const string SoftMaskSubtypeKey = "/S";
        public const string SoftMaskGroupKey = "/G";
        public const string SoftMaskBCKey = "/BC";
        public const string SoftMaskTRKey = "/TR";
        
        // Transparency Group Subtypes
        public const string TransparencyGroupValue = "/Transparency";
        
        // Soft Mask Subtypes
        public const string AlphaSoftMask = "/Alpha";
        public const string LuminositySoftMask = "/Luminosity";

        // Object Stream Dictionary Keys
        public const string ObjStmKey = "/ObjStm";
        public const string NKey = "/N"; // object stream key; do not use for functions
        public const string FirstKey = "/First";
        
        // Cross-Reference Stream Dictionary Keys (PDF 1.5+)
        public const string XRefKey = "/XRef";
        public const string IndexKey = "/Index";
        public const string PrevKey = "/Prev";
        public const string SizeKey = "/Size";
        public const string InfoKey = "/Info";
        public const string EncryptKey = "/Encrypt"; // Trailer encryption dictionary reference key
        public const string IdKey = "/ID";           // Trailer file identifier array key
        
        // Object Stream Dictionary Keys (PDF 1.5+)
        public const string ExtendsKey = "/Extends";
        
        // Version and Extension Keys (PDF 1.7+)
        public const string VersionKey = "/Version";
        public const string ExtensionsKey = "/Extensions";
        public const string ExtensionLevelKey = "/ExtensionLevel";
        public const string BaseVersionKey = "/BaseVersion";
        
        // Metadata Stream Keys (PDF 1.4+)
        public const string MetadataKey = "/Metadata";
        
        // Linearization Dictionary Keys
        public const string LinearizedKey = "/Linearized";
        public const string LKey = "/L";
        public const string HKey = "/H";
        public const string EKey = "/E";
        public const string TKey = "/T";

        // Content Stream Text Operators
        public const string BeginTextKey = "BT";
        public const string ShowTextKey = "Tj";
        public const string ShowTextWithGlyphPositioningKey = "TJ";
        public const string SetFontKey = "Tf";
        public const string MoveTextPositionKey = "Td";
        public const string MoveTextPositionAndSetLeadingKey = "TD";
        public const string SetTextMatrixKey = "Tm";
        public const string SetTextRiseKey = "Ts";
        public const string SetWordSpacingKey = "Tw";
        public const string SetCharacterSpacingKey = "Tc";
        public const string SetHorizontalScalingKey = "Tz";
        public const string SetTextLeadingKey = "TL";
        public const string NextLineKey = "T*";
        public const string ShowTextNextLineKey = "'";
        public const string SetSpacingAndShowTextKey = "\"";
        
        // ToUnicode CMap Keywords (strings for dictionary keys)
        public const string BeginBfCharKey = "beginbfchar";
        public const string EndBfCharKey = "endbfchar";
        public const string BeginBfRangeKey = "beginbfrange";
        public const string EndBfRangeKey = "endbfrange";
        public const string BeginCodespaceRangeKey = "begincodespacerange";
        public const string EndCodespaceRangeKey = "endcodespacerange";
        public const string BeginCMapKey = "begincmap";
        public const string EndCMapKey = "endcmap";
        public const string UseCMapKey = "usecmap";
        
        // ToUnicode CMap Keywords (byte spans for efficient parsing)
        public static ReadOnlySpan<byte> BeginBfChar => "beginbfchar"u8;
        public static ReadOnlySpan<byte> EndBfChar => "endbfchar"u8;
        public static ReadOnlySpan<byte> BeginBfRange => "beginbfrange"u8;
        public static ReadOnlySpan<byte> EndBfRange => "endbfrange"u8;
        public static ReadOnlySpan<byte> BeginCodespaceRange => "begincodespacerange"u8;
        public static ReadOnlySpan<byte> EndCodespaceRange => "endcodespacerange"u8;
        public static ReadOnlySpan<byte> BeginCMap => "begincmap"u8;
        public static ReadOnlySpan<byte> EndCMap => "endcmap"u8;
        public static ReadOnlySpan<byte> UseCMap => "usecmap"u8;
        
        // CID CMap Keywords
        public const string BeginCidCharKey = "begincidchar";
        public const string EndCidCharKey = "endcidchar";
        public const string BeginCidRangeKey = "begincidrange";
        public const string EndCidRangeKey = "endcidrange";
        public static ReadOnlySpan<byte> BeginCidChar => "begincidchar"u8;
        public static ReadOnlySpan<byte> EndCidChar => "endcidchar"u8;
        public static ReadOnlySpan<byte> BeginCidRange => "begincidrange"u8;
        public static ReadOnlySpan<byte> EndCidRange => "endcidrange"u8;
        
        // PDF Boolean Values
        public const string TrueValue = "/true";
        public const string FalseValue = "/false";
        
        // PDF Special Values
        public const string NoneValue = "/None";
        
        // Common PDF Values
        public const string FlateDecode = "/FlateDecode";
        public const string LZWDecode = "/LZWDecode";
        public const string DCTDecode = "/DCTDecode";
        public const string ASCIIHexDecode = "/ASCIIHexDecode";
        public const string ASCII85Decode = "/ASCII85Decode";
        public const string JPXDecode = "/JPXDecode";
        public const string JBIG2Decode = "/JBIG2Decode";
        public const string CCITTFaxDecode = "/CCITTFaxDecode";
        public const string RunLengthDecode = "/RunLengthDecode";
        public const string Crypt = "/Crypt";

        // Parser Configuration Constants
        public const int MinBufferLengthForObjectParsing = 10;
        public const int MinBufferLengthForEndObj = 6; // "endobj".Length
        public const int EndstreamTokenLength = 9; // "endstream".Length
        public const int XrefSearchOffset = 20;
        public const int StartxrefSearchWindowSize = 1024; // Search in last 1024 bytes first for performance

        // Special Characters
        public const byte ForwardSlash = (byte)'/';
        public const byte LeftParen = (byte)'(';
        public const byte RightParen = (byte)')';
        public const byte LeftAngle = (byte)'<';
        public const byte RightAngle = (byte)'>';
        public const byte LeftSquare = (byte)'[';
        public const byte RightSquare = (byte)']';
        public const byte Plus = (byte)'+';
        public const byte Minus = (byte)'-';
        public const byte Dot = (byte)'.';
        public const byte Backslash = (byte)'\\';
        public const byte Zero = (byte)'0';
        public const byte Nine = (byte)'9';

        // Frequently used bytes for performance optimization
        public const byte ByteS = (byte)'s';
        public const byte ByteT = (byte)'t';
        public const byte ByteA = (byte)'a';
        public const byte ByteR = (byte)'r';
        public const byte ByteX = (byte)'x';
        public const byte ByteE = (byte)'e';
        public const byte ByteF = (byte)'f';

        // Color/ICC and CIE /Lab related keys
        public const string AlternateKey = "/Alternate";
        public const string WhitePointKey = "/WhitePoint";
        public const string GammaKey = "/Gamma";
        public const string BlackPointKey = "/BlackPoint";
        public const string RangeKey = "/Range"; // /Lab range specification
        public const string DefaultCMYKKey = "/DefaultCMYK";            // Page resource default CMYK color space
        public const string OutputIntentsKey = "/OutputIntents";        // Catalog array of output intents
        public const string DestOutputProfileKey = "/DestOutputProfile"; // Output intent profile stream
        public const string DefaultGrayKey = "/DefaultGray";            // Page resource default Gray color space (PDF 1.5+)
        public const string DefaultRGBKey = "/DefaultRGB";              // Page resource default RGB color space (PDF 1.5+)
        
        // Pattern-related Dictionary Keys
        public const string PatternKey = "/Pattern"; // Pattern resources dictionary key
        public const string PatternTypeKey = "/PatternType"; // Pattern dictionary key
        public const string PaintTypeKey = "/PaintType"; // Tiling pattern paint type (1 colored, 2 uncolored)
        public const string TilingTypeKey = "/TilingType"; // Tiling pattern tiling type (1 constant spacing, etc.)
        public const string XStepKey = "/XStep"; // Tiling pattern horizontal step
        public const string YStepKey = "/YStep"; // Tiling pattern vertical step

        // Function related (added)
        public const string BitsPerSampleKey = "/BitsPerSample"; // Function type 0 sampled function BPS
        public const string TintTransformKey = "/TintTransform"; // Separation/DeviceN attribute (when dictionary form used)
    }
}