using PdfReader.Models;
using System;

namespace PdfReader.Text
{
    /// <summary>
    /// Common PDF dictionary key constants for shading and mesh decoding.
    /// </summary>
    public static class PdfTokens
    {
        // PDF Keywords

        public static ReadOnlySpan<byte> Xref => "xref"u8;
        public static ReadOnlySpan<byte> Startxref => "startxref"u8;


        public static PdfString Stream = (PdfString)"stream"u8;
        public static PdfString Endstream = (PdfString)"endstream"u8;

        public static PdfString Obj = (PdfString)"obj"u8;
        public static PdfString Endobj = (PdfString)"endobj"u8;

        public static PdfString Trailer = (PdfString)"trailer"u8;

        // Dictionary Key Constants (as PdfString for dictionary lookups)
        public static readonly PdfString RootKey = (PdfString)"Root"u8;
        public static readonly PdfString PagesKey = (PdfString)"Pages"u8;
        public static readonly PdfString PageKey = (PdfString)"Page"u8;
        public static readonly PdfString TypeKey = (PdfString)"Type"u8;
        public static readonly PdfString CountKey = (PdfString)"Count"u8;
        public static readonly PdfString KidsKey = (PdfString)"Kids"u8;
        public static readonly PdfString MediaBoxKey = (PdfString)"MediaBox"u8;
        public static readonly PdfString ContentsKey = (PdfString)"Contents"u8;
        public static readonly PdfString ResourcesKey = (PdfString)"Resources"u8;
        public static readonly PdfString CatalogKey = (PdfString)"Catalog"u8;
        public static readonly PdfString FilterKey = (PdfString)"Filter"u8;
        public static readonly PdfString RotateKey = (PdfString)"Rotate"u8;
        public static readonly PdfString CropBoxKey = (PdfString)"CropBox"u8;
        // Added missing page box keys (override semantics top-down)
        public static readonly PdfString BleedBoxKey = (PdfString)"BleedBox"u8;
        public static readonly PdfString TrimBoxKey = (PdfString)"TrimBox"u8;
        public static readonly PdfString ArtBoxKey = (PdfString)"ArtBox"u8;
        public static readonly PdfString ParentKey = (PdfString)"Parent"u8;
        public static readonly PdfString LengthKey = (PdfString)"Length"u8; // also used in encryption dictionary
        public static readonly PdfString VKey = (PdfString)"V"u8;             // encryption version
        public static readonly PdfString RKey = (PdfString)"R"u8;             // encryption revision
        public static readonly PdfString PKey = (PdfString)"P"u8;             // permissions
        public static readonly PdfString EncryptMetadataKey = (PdfString)"EncryptMetadata"u8; // encrypt metadata flag
        public static readonly PdfString OKey = (PdfString)"O"u8;             // used in linearization and encryption dictionaries
        public static readonly PdfString UKey = (PdfString)"U"u8;             // encryption dictionary user entry
        public static readonly PdfString OEKey = (PdfString)"OE"u8;           // encryption dictionary owner encrypted key (R>=5)
        public static readonly PdfString UEKey = (PdfString)"UE"u8;           // encryption dictionary user encrypted key (R>=5)
        public static readonly PdfString PermsKey = (PdfString)"Perms"u8;     // encryption dictionary permissions (R>=5)
        public static readonly PdfString StmFKey = (PdfString)"StmF"u8;       // encryption stream crypt filter name
        public static readonly PdfString StrFKey = (PdfString)"StrF"u8;       // encryption string crypt filter name
        public static readonly PdfString EffKey = (PdfString)"EFF"u8;         // encryption embedded file crypt filter name
        public static readonly PdfString CFKey = (PdfString)"CF"u8;           // encryption crypt filter dictionary

        // Image/XObject specific
        public static readonly PdfString DecodeKey = (PdfString)"Decode"u8;
        public static readonly PdfString DecodeParmsKey = (PdfString)"DecodeParms"u8;
        public static readonly PdfString PredictorKey = (PdfString)"Predictor"u8;
        public static readonly PdfString ColorsKey = (PdfString)"Colors"u8;
        public static readonly PdfString ColumnsKey = (PdfString)"Columns"u8;
        public static readonly PdfString KKey = (PdfString)"K"u8;
        public static readonly PdfString EndOfLineKey = (PdfString)"EndOfLine"u8;
        public static readonly PdfString EncodedByteAlignKey = (PdfString)"EncodedByteAlign"u8;
        public static readonly PdfString RowsKey = (PdfString)"Rows"u8;
        public static readonly PdfString EndOfBlockKey = (PdfString)"EndOfBlock"u8;
        public static readonly PdfString BlackIs1Key = (PdfString)"BlackIs1"u8;
        public static readonly PdfString DamagedRowsBeforeErrorKey = (PdfString)"DamagedRowsBeforeError"u8;
        public static readonly PdfString EarlyChangeKey = (PdfString)"EarlyChange"u8;
        public static readonly PdfString ColorTransformKey = (PdfString)"ColorTransform"u8;
        public static readonly PdfString NameKey = (PdfString)"Name"u8;

        // Font-related Dictionary Keys
        public static readonly PdfString FontKey = (PdfString)"Font"u8;
        public static readonly PdfString BaseFontKey = (PdfString)"BaseFont"u8;
        public static readonly PdfString SubtypeKey = (PdfString)"Subtype"u8;
        public static readonly PdfString EncodingKey = (PdfString)"Encoding"u8;
        public static readonly PdfString BaseEncodingKey = (PdfString)"BaseEncoding"u8;
        public static readonly PdfString DifferencesKey = (PdfString)"Differences"u8;
        public static readonly PdfString FirstCharKey = (PdfString)"FirstChar"u8;
        public static readonly PdfString LastCharKey = (PdfString)"LastChar"u8;
        public static readonly PdfString WidthsKey = (PdfString)"Widths"u8;
        public static readonly PdfString FontDescriptorKey = (PdfString)"FontDescriptor"u8;
        public static readonly PdfString FontFileKey = (PdfString)"FontFile"u8;
        public static readonly PdfString FontFile2Key = (PdfString)"FontFile2"u8;
        public static readonly PdfString FontFile3Key = (PdfString)"FontFile3"u8;
        public static readonly PdfString FontNameKey = (PdfString)"FontName"u8;
        public static readonly PdfString FlagsKey = (PdfString)"Flags"u8;
        public static readonly PdfString FontBBoxKey = (PdfString)"FontBBox"u8;
        public static readonly PdfString ItalicAngleKey = (PdfString)"ItalicAngle"u8;
        public static readonly PdfString AscentKey = (PdfString)"Ascent"u8;
        public static readonly PdfString DescentKey = (PdfString)"Descent"u8;
        public static readonly PdfString CapHeightKey = (PdfString)"CapHeight"u8;
        public static readonly PdfString XHeightKey = (PdfString)"XHeight"u8;
        public static readonly PdfString StemVKey = (PdfString)"StemV"u8;
        public static readonly PdfString StemHKey = (PdfString)"StemH"u8;
        public static readonly PdfString AvgWidthKey = (PdfString)"AvgWidth"u8;
        public static readonly PdfString MaxWidthKey = (PdfString)"MaxWidth"u8;
        public static readonly PdfString MissingWidthKey = (PdfString)"MissingWidth"u8;
        public static readonly PdfString FontFamilyKey = (PdfString)"FontFamily"u8;     // PDF 1.5+
        public static readonly PdfString FontStretchKey = (PdfString)"FontStretch"u8;   // Name: UltraCondensed..UltraExpanded
        public static readonly PdfString FontWeightKey = (PdfString)"FontWeight"u8;     // 100..900
        public static readonly PdfString LeadingKey = (PdfString)"Leading"u8;           // Preferred line height
        public static readonly PdfString CharSetKey = (PdfString)"CharSet"u8;           // Glyph names present (string)
        public static readonly PdfString StemSnapHKey = (PdfString)"StemSnapH"u8;       // Array of stem widths (horizontal)
        public static readonly PdfString StemSnapVKey = (PdfString)"StemSnapV"u8;       // Array of stem widths (vertical)
        public static readonly PdfString PanoseKey = (PdfString)"Panose"u8;             // 12-byte classification
        public static readonly PdfString CharProcsKey = (PdfString)"CharProcs"u8;
        public static readonly PdfString FontMatrixKey = (PdfString)"FontMatrix"u8;
        public static readonly PdfString ToUnicodeKey = (PdfString)"ToUnicode"u8;
        public static readonly PdfString CIDFontTypeKey = (PdfString)"CIDFontType"u8;
        public static readonly PdfString CIDSystemInfoKey = (PdfString)"CIDSystemInfo"u8;
        public static readonly PdfString RegistryKey = (PdfString)"Registry"u8;
        public static readonly PdfString OrderingKey = (PdfString)"Ordering"u8;
        public static readonly PdfString SupplementKey = (PdfString)"Supplement"u8;
        public static readonly PdfString DWKey = (PdfString)"DW"u8;
        public static readonly PdfString WKey = (PdfString)"W"u8;
        public static readonly PdfString DescendantFontsKey = (PdfString)"DescendantFonts"u8;
        public static readonly PdfString CIDToGIDMapKey = (PdfString)"CIDToGIDMap"u8;
        public static readonly PdfString CMapNameKey = (PdfString)"CMapName"u8;        // For CMap streams
        public static readonly PdfString CMapTypeValue = (PdfString)"CMap"u8;
        public static readonly PdfString Length1 = (PdfString)"Length1"u8;
        public static readonly PdfString Length2 = (PdfString)"Length2"u8;
        public static readonly PdfString Length3 = (PdfString)"Length3"u8;

        // Font Types
        public static readonly PdfString Type1FontKey = (PdfString)"Type1"u8;
        public static readonly PdfString TrueTypeFontKey = (PdfString)"TrueType"u8;
        public static readonly PdfString Type3FontKey = (PdfString)"Type3"u8;
        public static readonly PdfString Type0FontKey = (PdfString)"Type0"u8;
        public static readonly PdfString CIDFontType0Key = (PdfString)"CIDFontType0"u8;
        public static readonly PdfString CIDFontType2Key = (PdfString)"CIDFontType2"u8;
        public static readonly PdfString MMType1FontKey = (PdfString)"MMType1"u8;
        
        // Font descriptor type
        public static readonly PdfString FontDescriptorTypeKey = (PdfString)"FontDescriptor"u8;

        // Standard Encodings
        public static readonly PdfString StandardEncodingKey = (PdfString)"StandardEncoding"u8;
        public static readonly PdfString MacRomanEncodingKey = (PdfString)"MacRomanEncoding"u8;
        public static readonly PdfString WinAnsiEncodingKey = (PdfString)"WinAnsiEncoding"u8;
        public static readonly PdfString MacExpertEncodingKey = (PdfString)"MacExpertEncoding"u8;
        
        // CID Font Encodings (CMaps)
        public static readonly PdfString IdentityKey = (PdfString)"Identity";      // Identity CMap for CID fonts
        
        // Graphics State Dictionary Keys
        public static readonly PdfString LineWidthKey = (PdfString)"LW"u8;
        public static readonly PdfString LineCapKey = (PdfString)"LC"u8;
        public static readonly PdfString LineJoinKey = (PdfString)"LJ"u8;
        public static readonly PdfString MiterLimitKey = (PdfString)"ML"u8;
        public static readonly PdfString DashPatternKey = (PdfString)"D"u8;
        public static readonly PdfString StrokeAlphaKey = (PdfString)"CA"u8;
        public static readonly PdfString FillAlphaKey = (PdfString)"ca"u8;
        public static readonly PdfString BlendModeKey = (PdfString)"BM"u8;
        public static readonly PdfString MatrixKey = (PdfString)"Matrix"u8;
        public static readonly PdfString CTMKey = (PdfString)"CTM"u8;
        
        // Resource Dictionary Keys
        public static readonly PdfString ColorSpaceKey = (PdfString)"ColorSpace"u8;
        public static readonly PdfString ExtGStateKey = (PdfString)"ExtGState"u8;
        public static readonly PdfString XObjectKey = (PdfString)"XObject"u8;
        public static readonly PdfString ProcSetKey = (PdfString)"ProcSet"u8;
        public static readonly PdfString ShadingKey = (PdfString)"Shading"u8;
        
        // XObject Dictionary Keys
        public static readonly PdfString WidthKey = (PdfString)"Width"u8;
        public static readonly PdfString HeightKey = (PdfString)"Height"u8;
        public static readonly PdfString BitsPerComponentKey = (PdfString)"BitsPerComponent"u8;
        public static readonly PdfString BBoxKey = (PdfString)"BBox"u8;
        public static readonly PdfString ImageMaskKey = (PdfString)"ImageMask"u8;
        public static readonly PdfString MaskKey = (PdfString)"Mask"u8;
        public static readonly PdfString InterpolateKey = (PdfString)"Interpolate"u8;
        public static readonly PdfString IntentKey = (PdfString)"Intent"u8;
        public static readonly PdfString MatteKey = (PdfString)"Matte"u8; // Soft mask image dematting color components

        // Shading Dictionary Keys
        public static readonly PdfString ShadingTypeKey = (PdfString)"ShadingType"u8; // 1..7; we support 2 (axial) and 3 (radial) minimally
        public static readonly PdfString CoordsKey = (PdfString)"Coords"u8;           // coordinates array
        public static readonly PdfString C0Key = (PdfString)"C0"u8;                   // starting color components
        public static readonly PdfString C1Key = (PdfString)"C1"u8;                   // ending color components
        public static readonly PdfString FunctionKey = (PdfString)"Function"u8;       // function for color (not implemented)
        public static readonly PdfString DomainKey = (PdfString)"Domain"u8;           // optional domain for input variable
        public static readonly PdfString FunctionTypeKey = (PdfString)"FunctionType"u8; // function dictionaries only
        public static readonly PdfString FnNKey = (PdfString)"N"u8;                   // exponent for function type 2 (distinct from object-stream /N)
        public static readonly PdfString FunctionsKey = (PdfString)"Functions"u8;     // stitching function sub-functions
        public static readonly PdfString BoundsKey = (PdfString)"Bounds"u8;           // stitching function bounds
        public static readonly PdfString EncodeKey = (PdfString)"Encode"u8;           // stitching function encode array
        public static readonly PdfString ExtendKey = (PdfString)"Extend"u8;           // extend flags for shadings
        public static readonly PdfString BitsPerCoordinateKey = (PdfString)"BitsPerCoordinate"u8; // Dictionary key for bits per coordinate in mesh shading
        public static readonly PdfString BitsPerFlagKey = (PdfString)"BitsPerFlag"u8;           // Dictionary key for bits per flag in mesh shading
        public static readonly PdfString VerticesPerRowKey = (PdfString)"VerticesPerRow"u8; 

        // XObject Subtypes
        public static readonly PdfString ImageSubtype = (PdfString)"Image"u8;
        public static readonly PdfString FormSubtype = (PdfString)"Form"u8;
        public static readonly PdfString PSSubtype = (PdfString)"PS"u8;
        
        // Extended Graphics State Dictionary Keys (transparency)
        public static readonly PdfString SoftMaskKey = (PdfString)"SMask"u8;
        public static readonly PdfString GroupKey = (PdfString)"Group"u8;
        public static readonly PdfString KnockoutKey = (PdfString)"TK"u8;
        public static readonly PdfString OverprintModeKey = (PdfString)"OPM"u8;
        public static readonly PdfString OverprintStrokeKey = (PdfString)"OP"u8;
        public static readonly PdfString OverprintFillKey = (PdfString)"op"u8;
        public static readonly PdfString AlphaConstantKey = (PdfString)"AIS"u8;
        public static readonly PdfString GroupSubtypeKey = (PdfString)"S"u8;
        public static readonly PdfString GroupColorSpaceKey = (PdfString)"CS"u8;
        public static readonly PdfString GroupIsolatedKey = (PdfString)"I"u8;
        public static readonly PdfString GroupKnockoutKey = (PdfString)"K"u8;
        
        // Soft Mask Dictionary Keys
        public static readonly PdfString SoftMaskSubtypeKey = (PdfString)"S"u8;
        public static readonly PdfString SoftMaskGroupKey = (PdfString)"G"u8;
        public static readonly PdfString SoftMaskBCKey = (PdfString)"BC"u8;
        
        // Transparency Group Subtypes
        public static readonly PdfString TransparencyGroupValue = (PdfString)"Transparency"u8;
        
        // Soft Mask Subtypes
        public static readonly PdfString AlphaSoftMask = (PdfString)"Alpha"u8;
        public static readonly PdfString LuminositySoftMask = (PdfString)"Luminosity"u8;

        // Object Stream Dictionary Keys
        public static readonly PdfString ObjStmKey = (PdfString)"ObjStm"u8;
        public static readonly PdfString NKey = (PdfString)"N"u8; // object stream key; do not use for functions
        public static readonly PdfString FirstKey = (PdfString)"First"u8;
        
        // Cross-Reference Stream Dictionary Keys (PDF 1.5+)
        public static readonly PdfString XRefKey = (PdfString)"XRef"u8;
        public static readonly PdfString IndexKey = (PdfString)"Index"u8;
        public static readonly PdfString PrevKey = (PdfString)"Prev"u8;
        public static readonly PdfString SizeKey = (PdfString)"Size"u8;
        public static readonly PdfString InfoKey = (PdfString)"Info"u8;
        public static readonly PdfString EncryptKey = (PdfString)"Encrypt"u8; // Trailer encryption dictionary reference key
        public static readonly PdfString IdKey = (PdfString)"ID"u8;           // Trailer file identifier array key
        
        // PDF Special Values
        public static readonly PdfString NoneValue = (PdfString)"None"u8;

        // Color/ICC and CIE /Lab related keys
        public static readonly PdfString AlternateKey = (PdfString)"Alternate"u8;
        public static readonly PdfString WhitePointKey = (PdfString)"WhitePoint"u8;
        public static readonly PdfString GammaKey = (PdfString)"Gamma"u8;
        public static readonly PdfString BlackPointKey = (PdfString)"BlackPoint"u8;
        public static readonly PdfString RangeKey = (PdfString)"Range"u8; // /Lab range specification
        public static readonly PdfString OutputIntentsKey = (PdfString)"OutputIntents"u8;        // Catalog array of output intents
        public static readonly PdfString DestOutputProfileKey = (PdfString)"DestOutputProfile"u8; // Output intent profile stream
        public static readonly PdfString DefaultCMYKKey = (PdfString)"DefaultCMYK"u8;            // Page resource default CMYK color space
        public static readonly PdfString DefaultGrayKey = (PdfString)"DefaultGray"u8;            // Page resource default Gray color space (PDF 1.5+)
        public static readonly PdfString DefaultRGBKey = (PdfString)"DefaultRGB"u8;              // Page resource default RGB color space (PDF 1.5+)

        // Pattern-related Dictionary Keys
        public static readonly PdfString PatternKey = (PdfString)"Pattern"u8; // Pattern resources dictionary key
        public static readonly PdfString PatternTypeKey = (PdfString)"PatternType"u8; // Pattern dictionary key
        public static readonly PdfString PaintTypeKey = (PdfString)"PaintType"u8; // Tiling pattern paint type (1 colored, 2 uncolored)
        public static readonly PdfString TilingTypeKey = (PdfString)"TilingType"u8; // Tiling pattern tiling type (1 constant spacing, etc.)
        public static readonly PdfString XStepKey = (PdfString)"XStep"u8; // Tiling pattern horizontal step
        public static readonly PdfString YStepKey = (PdfString)"YStep"u8; // Tiling pattern vertical step

        // Function related (added)
        public static readonly PdfString BitsPerSampleKey = (PdfString)"BitsPerSample"u8; // Function type 0 sampled function BPS
        public static readonly PdfString TintTransformKey = (PdfString)"TintTransform"u8; // Separation/DeviceN attribute (when dictionary form used)
    }
}