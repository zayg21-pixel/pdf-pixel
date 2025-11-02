using PdfReader.Models;
using System;

namespace PdfReader
{
    /// <summary>
    /// Common PDF dictionary key constants for shading and mesh decoding.
    /// </summary>
    public static class PdfTokens
    {
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
        public const byte Reference = (byte)'R';
        public const byte Dot = (byte)'.';
        public const byte Backslash = (byte)'\\';
        public const byte Zero = (byte)'0';
        public const byte Nine = (byte)'9';

        // PDF Keywords
        public static ReadOnlySpan<byte> Endstream => "endstream"u8;
        public static ReadOnlySpan<byte> Obj => "obj"u8;
        public static ReadOnlySpan<byte> Endobj => "endobj"u8;
        public static ReadOnlySpan<byte> Xref => "xref"u8;
        public static ReadOnlySpan<byte> Trailer => "trailer"u8;
        public static ReadOnlySpan<byte> Startxref => "startxref"u8;
        public static ReadOnlySpan<byte> Stream => "stream"u8;
        
        // Dictionary Delimiters
        public static ReadOnlySpan<byte> DictStart => "<<"u8;
        public static ReadOnlySpan<byte> DictEnd => ">>"u8;
        public static ReadOnlySpan<byte> ArrayStart => "["u8;
        public static ReadOnlySpan<byte> ArrayEnd => "]"u8;
        
        // Dictionary Key Constants (as PdfString for dictionary lookups)
        public static readonly PdfString RootKey = "Root"u8;
        public static readonly PdfString PagesKey = "Pages"u8;
        public static readonly PdfString PageKey = "Page"u8;
        public static readonly PdfString TypeKey = "Type"u8;
        public static readonly PdfString CountKey = "Count"u8;
        public static readonly PdfString KidsKey = "Kids"u8;
        public static readonly PdfString MediaBoxKey = "MediaBox"u8;
        public static readonly PdfString ContentsKey = "Contents"u8;
        public static readonly PdfString ResourcesKey = "Resources"u8;
        public static readonly PdfString CatalogKey = "Catalog"u8;
        public static readonly PdfString FilterKey = "Filter"u8;
        public static readonly PdfString RotateKey = "Rotate"u8;
        public static readonly PdfString CropBoxKey = "CropBox"u8;
        public static readonly PdfString ParentKey = "Parent"u8;
        public static readonly PdfString LengthKey = "Length"u8; // also used in encryption dictionary
        public static readonly PdfString VKey = "V"u8;             // encryption version
        public static readonly PdfString RKey = "R"u8;             // encryption revision
        public static readonly PdfString PKey = "P"u8;             // permissions
        public static readonly PdfString EncryptMetadataKey = "EncryptMetadata"u8; // encrypt metadata flag
        public static readonly PdfString OKey = "O"u8;             // used in linearization and encryption dictionaries
        public static readonly PdfString UKey = "U"u8;             // encryption dictionary user entry
        public static readonly PdfString OEKey = "OE"u8;           // encryption dictionary owner encrypted key (R>=5)
        public static readonly PdfString UEKey = "UE"u8;           // encryption dictionary user encrypted key (R>=5)
        public static readonly PdfString PermsKey = "Perms"u8;     // encryption dictionary permissions (R>=5)
        public static readonly PdfString StmFKey = "StmF"u8;       // encryption stream crypt filter name
        public static readonly PdfString StrFKey = "StrF"u8;       // encryption string crypt filter name
        public static readonly PdfString EffKey = "EFF"u8;         // encryption embedded file crypt filter name
        public static readonly PdfString CFKey = "CF"u8;           // encryption crypt filter dictionary

        // Image/XObject specific
        public static readonly PdfString DecodeKey = "Decode"u8;
        public static readonly PdfString DecodeParmsKey = "DecodeParms"u8;
        public static readonly PdfString PredictorKey = "Predictor"u8;
        public static readonly PdfString ColorsKey = "Colors"u8;
        public static readonly PdfString ColumnsKey = "Columns"u8;
        public static readonly PdfString KKey = "K"u8;
        public static readonly PdfString EndOfLineKey = "EndOfLine"u8;
        public static readonly PdfString EncodedByteAlignKey = "EncodedByteAlign"u8;
        public static readonly PdfString RowsKey = "Rows"u8;
        public static readonly PdfString EndOfBlockKey = "EndOfBlock"u8;
        public static readonly PdfString BlackIs1Key = "BlackIs1"u8;
        public static readonly PdfString DamagedRowsBeforeErrorKey = "DamagedRowsBeforeError"u8;
        public static readonly PdfString EarlyChangeKey = "EarlyChange"u8;
        public static readonly PdfString ColorTransformKey = "ColorTransform"u8;
        public static readonly PdfString NameKey = "Name"u8;

        // Font-related Dictionary Keys
        public static readonly PdfString FontKey = "Font"u8;
        public static readonly PdfString BaseFontKey = "BaseFont"u8;
        public static readonly PdfString SubtypeKey = "Subtype"u8;
        public static readonly PdfString EncodingKey = "Encoding"u8;
        public static readonly PdfString BaseEncodingKey = "BaseEncoding"u8;
        public static readonly PdfString DifferencesKey = "Differences"u8;
        public static readonly PdfString FirstCharKey = "FirstChar"u8;
        public static readonly PdfString LastCharKey = "LastChar"u8;
        public static readonly PdfString WidthsKey = "Widths"u8;
        public static readonly PdfString FontDescriptorKey = "FontDescriptor"u8;
        public static readonly PdfString FontFileKey = "FontFile"u8;
        public static readonly PdfString FontFile2Key = "FontFile2"u8;
        public static readonly PdfString FontFile3Key = "FontFile3"u8;
        public static readonly PdfString FontNameKey = "FontName"u8;
        public static readonly PdfString FlagsKey = "Flags"u8;
        public static readonly PdfString FontBBoxKey = "FontBBox"u8;
        public static readonly PdfString ItalicAngleKey = "ItalicAngle"u8;
        public static readonly PdfString AscentKey = "Ascent"u8;
        public static readonly PdfString DescentKey = "Descent"u8;
        public static readonly PdfString CapHeightKey = "CapHeight"u8;
        public static readonly PdfString XHeightKey = "XHeight"u8;
        public static readonly PdfString StemVKey = "StemV"u8;
        public static readonly PdfString StemHKey = "StemH"u8;
        public static readonly PdfString AvgWidthKey = "AvgWidth"u8;
        public static readonly PdfString MaxWidthKey = "MaxWidth"u8;
        public static readonly PdfString MissingWidthKey = "MissingWidth"u8;
        public static readonly PdfString FontFamilyKey = "FontFamily"u8;     // PDF 1.5+
        public static readonly PdfString FontStretchKey = "FontStretch"u8;   // Name: UltraCondensed..UltraExpanded
        public static readonly PdfString FontWeightKey = "FontWeight"u8;     // 100..900
        public static readonly PdfString LeadingKey = "Leading"u8;           // Preferred line height
        public static readonly PdfString CharSetKey = "CharSet"u8;           // Glyph names present (string)
        public static readonly PdfString StemSnapHKey = "StemSnapH"u8;       // Array of stem widths (horizontal)
        public static readonly PdfString StemSnapVKey = "StemSnapV"u8;       // Array of stem widths (vertical)
        public static readonly PdfString PanoseKey = "Panose"u8;             // 12-byte classification
        public static readonly PdfString CharProcsKey = "CharProcs"u8;
        public static readonly PdfString FontMatrixKey = "FontMatrix"u8;
        public static readonly PdfString ToUnicodeKey = "ToUnicode"u8;
        public static readonly PdfString CIDFontTypeKey = "CIDFontType"u8;
        public static readonly PdfString CIDSystemInfoKey = "CIDSystemInfo"u8;
        public static readonly PdfString RegistryKey = "Registry"u8;
        public static readonly PdfString OrderingKey = "Ordering"u8;
        public static readonly PdfString SupplementKey = "Supplement"u8;
        public static readonly PdfString DWKey = "DW"u8;
        public static readonly PdfString WKey = "W"u8;
        public static readonly PdfString DescendantFontsKey = "DescendantFonts"u8;
        public static readonly PdfString CIDToGIDMapKey = "CIDToGIDMap"u8;
        public static readonly PdfString CMapNameKey = "CMapName"u8;        // For CMap streams
        public static readonly PdfString CMapTypeValue = "CMap"u8;

        // Font Types
        public static readonly PdfString Type1FontKey = "Type1"u8;
        public static readonly PdfString TrueTypeFontKey = "TrueType"u8;
        public static readonly PdfString Type3FontKey = "Type3"u8;
        public static readonly PdfString Type0FontKey = "Type0"u8;
        public static readonly PdfString CIDFontType0Key = "CIDFontType0"u8;
        public static readonly PdfString CIDFontType2Key = "CIDFontType2"u8;
        public static readonly PdfString MMType1FontKey = "MMType1"u8;
        
        // Font descriptor type
        public static readonly PdfString FontDescriptorTypeKey = "FontDescriptor"u8;

        // Standard Encodings
        public static readonly PdfString StandardEncodingKey = "StandardEncoding"u8;
        public static readonly PdfString MacRomanEncodingKey = "MacRomanEncoding"u8;
        public static readonly PdfString WinAnsiEncodingKey = "WinAnsiEncoding"u8;
        public static readonly PdfString MacExpertEncodingKey = "MacExpertEncoding"u8;
        
        // CID Font Encodings (CMaps)
        public static readonly PdfString IdentityKey = "Identity"u8;      // Identity CMap for CID fonts
        
        // Graphics State Dictionary Keys
        public static readonly PdfString LineWidthKey = "LW"u8;
        public static readonly PdfString LineCapKey = "LC"u8;
        public static readonly PdfString LineJoinKey = "LJ"u8;
        public static readonly PdfString MiterLimitKey = "ML"u8;
        public static readonly PdfString DashPatternKey = "D"u8;
        public static readonly PdfString StrokeAlphaKey = "CA"u8;
        public static readonly PdfString FillAlphaKey = "ca"u8;
        public static readonly PdfString BlendModeKey = "BM"u8;
        public static readonly PdfString MatrixKey = "Matrix"u8;
        public static readonly PdfString CTMKey = "CTM"u8;
        
        // Resource Dictionary Keys
        public static readonly PdfString ColorSpaceKey = "ColorSpace"u8;
        public static readonly PdfString ExtGStateKey = "ExtGState"u8;
        public static readonly PdfString XObjectKey = "XObject"u8;
        public static readonly PdfString ProcSetKey = "ProcSet"u8;
        public static readonly PdfString ShadingKey = "Shading"u8;
        
        // XObject Dictionary Keys
        public static readonly PdfString WidthKey = "Width"u8;
        public static readonly PdfString HeightKey = "Height"u8;
        public static readonly PdfString BitsPerComponentKey = "BitsPerComponent"u8;
        public static readonly PdfString BBoxKey = "BBox"u8;
        public static readonly PdfString ImageMaskKey = "ImageMask"u8;
        public static readonly PdfString MaskKey = "Mask"u8;
        public static readonly PdfString InterpolateKey = "Interpolate"u8;
        public static readonly PdfString IntentKey = "Intent"u8;
        public static readonly PdfString MatteKey = "Matte"u8; // Soft mask image dematting color components

        // Shading Dictionary Keys
        public static readonly PdfString ShadingTypeKey = "ShadingType"u8; // 1..7; we support 2 (axial) and 3 (radial) minimally
        public static readonly PdfString CoordsKey = "Coords"u8;           // coordinates array
        public static readonly PdfString C0Key = "C0"u8;                   // starting color components
        public static readonly PdfString C1Key = "C1"u8;                   // ending color components
        public static readonly PdfString FunctionKey = "Function"u8;       // function for color (not implemented)
        public static readonly PdfString DomainKey = "Domain"u8;           // optional domain for input variable
        public static readonly PdfString FunctionTypeKey = "FunctionType"u8; // function dictionaries only
        public static readonly PdfString FnNKey = "N"u8;                   // exponent for function type 2 (distinct from object-stream /N)
        public static readonly PdfString FunctionsKey = "Functions"u8;     // stitching function sub-functions
        public static readonly PdfString BoundsKey = "Bounds"u8;           // stitching function bounds
        public static readonly PdfString EncodeKey = "Encode"u8;           // stitching function encode array
        public static readonly PdfString ExtendKey = "Extend"u8;           // extend flags for shadings
        public static readonly PdfString BitsPerCoordinateKey = "BitsPerCoordinate"u8; // Dictionary key for bits per coordinate in mesh shading
        public static readonly PdfString BitsPerFlagKey = "BitsPerFlag"u8;           // Dictionary key for bits per flag in mesh shading
        public static readonly PdfString VerticesPerRowKey = "VerticesPerRow"u8; 

        // XObject Subtypes
        public static readonly PdfString ImageSubtype = "Image"u8;
        public static readonly PdfString FormSubtype = "Form"u8;
        public static readonly PdfString PSSubtype = "PS"u8;
        
        // Extended Graphics State Dictionary Keys (transparency)
        public static readonly PdfString SoftMaskKey = "SMask"u8;
        public static readonly PdfString GroupKey = "Group"u8;
        public static readonly PdfString KnockoutKey = "TK"u8;
        public static readonly PdfString OverprintModeKey = "OPM"u8;
        public static readonly PdfString OverprintStrokeKey = "OP"u8;
        public static readonly PdfString OverprintFillKey = "op"u8;
        public static readonly PdfString AlphaConstantKey = "AIS"u8;
        public static readonly PdfString GroupSubtypeKey = "S"u8;
        public static readonly PdfString GroupColorSpaceKey = "CS"u8;
        public static readonly PdfString GroupIsolatedKey = "I"u8;
        public static readonly PdfString GroupKnockoutKey = "K"u8;
        
        // Soft Mask Dictionary Keys
        public static readonly PdfString SoftMaskSubtypeKey = "S"u8;
        public static readonly PdfString SoftMaskGroupKey = "G"u8;
        public static readonly PdfString SoftMaskBCKey = "BC"u8;
        public static readonly PdfString SoftMaskTRKey = "TR"u8;
        
        // Transparency Group Subtypes
        public static readonly PdfString TransparencyGroupValue = "Transparency"u8;
        
        // Soft Mask Subtypes
        public static readonly PdfString AlphaSoftMask = "Alpha"u8;
        public static readonly PdfString LuminositySoftMask = "Luminosity"u8;

        // Object Stream Dictionary Keys
        public static readonly PdfString ObjStmKey = "ObjStm"u8;
        public static readonly PdfString NKey = "N"u8; // object stream key; do not use for functions
        public static readonly PdfString FirstKey = "First"u8;
        
        // Cross-Reference Stream Dictionary Keys (PDF 1.5+)
        public static readonly PdfString XRefKey = "XRef"u8;
        public static readonly PdfString IndexKey = "Index"u8;
        public static readonly PdfString PrevKey = "Prev"u8;
        public static readonly PdfString SizeKey = "Size"u8;
        public static readonly PdfString InfoKey = "Info"u8;
        public static readonly PdfString EncryptKey = "Encrypt"u8; // Trailer encryption dictionary reference key
        public static readonly PdfString IdKey = "ID"u8;           // Trailer file identifier array key
        
        // PDF Boolean Values
        public static readonly PdfString TrueValue = "true"u8;
        public static readonly PdfString FalseValue = "false"u8;
        
        // PDF Special Values
        public static readonly PdfString NoneValue = "None"u8;

        // Color/ICC and CIE /Lab related keys
        public static readonly PdfString AlternateKey = "Alternate"u8;
        public static readonly PdfString WhitePointKey = "WhitePoint"u8;
        public static readonly PdfString GammaKey = "Gamma"u8;
        public static readonly PdfString BlackPointKey = "BlackPoint"u8;
        public static readonly PdfString RangeKey = "Range"u8; // /Lab range specification
        public static readonly PdfString OutputIntentsKey = "OutputIntents"u8;        // Catalog array of output intents
        public static readonly PdfString DestOutputProfileKey = "DestOutputProfile"u8; // Output intent profile stream
        public static readonly PdfString DefaultCMYKKey = "DefaultCMYK"u8;            // Page resource default CMYK color space
        public static readonly PdfString DefaultGrayKey = "DefaultGray"u8;            // Page resource default Gray color space (PDF 1.5+)
        public static readonly PdfString DefaultRGBKey = "DefaultRGB"u8;              // Page resource default RGB color space (PDF 1.5+)

        // Pattern-related Dictionary Keys
        public static readonly PdfString PatternKey = "Pattern"u8; // Pattern resources dictionary key
        public static readonly PdfString PatternTypeKey = "PatternType"u8; // Pattern dictionary key
        public static readonly PdfString PaintTypeKey = "PaintType"u8; // Tiling pattern paint type (1 colored, 2 uncolored)
        public static readonly PdfString TilingTypeKey = "TilingType"u8; // Tiling pattern tiling type (1 constant spacing, etc.)
        public static readonly PdfString XStepKey = "XStep"u8; // Tiling pattern horizontal step
        public static readonly PdfString YStepKey = "YStep"u8; // Tiling pattern vertical step

        // Function related (added)
        public static readonly PdfString BitsPerSampleKey = "BitsPerSample"u8; // Function type 0 sampled function BPS
        public static readonly PdfString TintTransformKey = "TintTransform"u8; // Separation/DeviceN attribute (when dictionary form used)
    }
}