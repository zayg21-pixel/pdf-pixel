using PdfPixel.Models;
using System;

namespace PdfPixel.Text;

/// <summary>
/// PDF keyword and dictionary key constants.
/// </summary>
internal static class PdfTokens
{
    // PDF Keywords

    public static ReadOnlySpan<byte> Header => "%PDF-"u8;
    public static ReadOnlySpan<byte> Xref => "xref"u8;
    public static ReadOnlySpan<byte> Startxref => "startxref"u8;

    public static readonly PdfString Stream = (PdfString)"stream"u8;
    public static readonly PdfString EndStream = (PdfString)"endstream"u8;

    public static readonly PdfString Obj = (PdfString)"obj"u8;
    public static readonly PdfString EndObj = (PdfString)"endobj"u8;

    public static readonly PdfString Trailer = (PdfString)"trailer"u8;

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
    public static readonly PdfString CfmKey = (PdfString)"CFM"u8;         // crypt filter method (e.g., /AESV2)
    public static readonly PdfString AuthEventKey = (PdfString)"AuthEvent"u8; // crypt filter authentication event
    public static readonly PdfString EmbeddedFileKey = (PdfString)"EmbeddedFile"u8; // embedded file stream type
    public static readonly PdfString MetadataKey = (PdfString)"Metadata"u8; // metadata stream type

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
    public static readonly PdfString Jbig2GlobalsKey = (PdfString)"JBIG2Globals"u8;

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
    public static readonly PdfString FieldFlagsKey = (PdfString)"Ff"u8;
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
    public static readonly PdfString CidFontTypeKey = (PdfString)"CIDFontType"u8;
    public static readonly PdfString CidSystemInfoKey = (PdfString)"CIDSystemInfo"u8;
    public static readonly PdfString RegistryKey = (PdfString)"Registry"u8;
    public static readonly PdfString OrderingKey = (PdfString)"Ordering"u8;
    public static readonly PdfString SupplementKey = (PdfString)"Supplement"u8;
    public static readonly PdfString DWKey = (PdfString)"DW"u8;
    public static readonly PdfString WKey = (PdfString)"W"u8;
    public static readonly PdfString DW2Key = (PdfString)"DW2"u8;
    public static readonly PdfString W2Key = (PdfString)"W2"u8;
    public static readonly PdfString DescendantFontsKey = (PdfString)"DescendantFonts"u8;
    public static readonly PdfString CidToGidMapKey = (PdfString)"CIDToGIDMap"u8;
    public static readonly PdfString CMapNameKey = (PdfString)"CMapName"u8;
    public static readonly PdfString WModeKey = (PdfString)"WMode"u8;
    public static readonly PdfString UseCMapKey = (PdfString)"UseCMap"u8;
    public static readonly PdfString Length1 = (PdfString)"Length1"u8;
    public static readonly PdfString Length2 = (PdfString)"Length2"u8;
    public static readonly PdfString Length3 = (PdfString)"Length3"u8;

    // Font Types
    public static readonly PdfString Type1FontKey = (PdfString)"Type1"u8;
    public static readonly PdfString TrueTypeFontKey = (PdfString)"TrueType"u8;
    public static readonly PdfString Type3FontKey = (PdfString)"Type3"u8;
    public static readonly PdfString Type0FontKey = (PdfString)"Type0"u8;
    public static readonly PdfString CidFontType0Key = (PdfString)"CIDFontType0"u8;
    public static readonly PdfString CidFontType2Key = (PdfString)"CIDFontType2"u8;
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

    public static readonly PdfString NewWindowKey = (PdfString)"NewWindow"u8; // GoToRemote action window flag

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
    public static readonly PdfString AlphaIsShapeKey = (PdfString)"AIS"u8;

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
    public static readonly PdfString SoftMaskInDataKey = (PdfString)"SMaskInData"u8; // JPX opacity channel handling, 0..2

    // Shading Dictionary Keys
    public static readonly PdfString ShadingTypeKey = (PdfString)"ShadingType"u8; // 1..7
    public static readonly PdfString CoordsKey = (PdfString)"Coords"u8;           // coordinates array
    public static readonly PdfString C0Key = (PdfString)"C0"u8;                   // starting color components
    public static readonly PdfString C1Key = (PdfString)"C1"u8;                   // ending color components
    public static readonly PdfString FunctionKey = (PdfString)"Function"u8;       // function for color
    public static readonly PdfString DomainKey = (PdfString)"Domain"u8;           // optional domain for input variable
    public static readonly PdfString FunctionTypeKey = (PdfString)"FunctionType"u8; // function dictionaries only
    public static readonly PdfString FnNKey = (PdfString)"N"u8;                   // exponent for function type 2 (distinct from object-stream /N)
    public static readonly PdfString FunctionsKey = (PdfString)"Functions"u8;     // stitching function sub-functions
    public static readonly PdfString BoundsKey = (PdfString)"Bounds"u8;           // stitching function bounds
    public static readonly PdfString EncodeKey = (PdfString)"Encode"u8;           // stitching function encode array
    public static readonly PdfString ExtendKey = (PdfString)"Extend"u8;           // extend flags for shadings
    public static readonly PdfString BitsPerCoordinateKey = (PdfString)"BitsPerCoordinate"u8; // mesh shading
    public static readonly PdfString BitsPerFlagKey = (PdfString)"BitsPerFlag"u8;             // mesh shading
    public static readonly PdfString VerticesPerRowKey = (PdfString)"VerticesPerRow"u8;
    public static readonly PdfString AntiAliasKey = (PdfString)"AntiAlias"u8;                 // mesh shading anti-aliasing flag
    public static readonly PdfString BackgroundKey = (PdfString)"Background"u8;               // mesh shading background color

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
    public static readonly PdfString StateIntentKey = (PdfString)"RI"u8;
    public static readonly PdfString AlphaConstantKey = (PdfString)"AIS"u8;
    public static readonly PdfString GroupSubtypeKey = (PdfString)"S"u8;
    public static readonly PdfString GroupColorSpaceKey = (PdfString)"CS"u8;
    public static readonly PdfString GroupIsolatedKey = (PdfString)"I"u8;
    public static readonly PdfString GroupKnockoutKey = (PdfString)"K"u8;

    // Soft Mask Dictionary Keys
    public static readonly PdfString SoftMaskSubtypeKey = (PdfString)"S"u8;
    public static readonly PdfString SoftMaskGroupKey = (PdfString)"G"u8;
    public static readonly PdfString SoftMaskBCKey = (PdfString)"BC"u8;
    public static readonly PdfString TransferFunctionKey = (PdfString)"TR"u8;
    public static readonly PdfString TransferFunction2Key = (PdfString)"TR2"u8;

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
    public static readonly PdfString XRefStmKey = (PdfString)"XRefStm"u8; // Trailer key of a hybrid-reference file, pointing at the stream that indexes its compressed objects
    public static readonly PdfString IndexKey = (PdfString)"Index"u8;
    public static readonly PdfString PrevKey = (PdfString)"Prev"u8;
    public static readonly PdfString SizeKey = (PdfString)"Size"u8;
    public static readonly PdfString InfoKey = (PdfString)"Info"u8;
    public static readonly PdfString EncryptKey = (PdfString)"Encrypt"u8; // Trailer encryption dictionary reference key
    public static readonly PdfString IdKey = (PdfString)"ID"u8;           // Trailer file identifier array key

    // Named Destinations
    public static readonly PdfString DestsKey = (PdfString)"Dests"u8;     // Catalog named destinations dictionary
    public static readonly PdfString NamesKey = (PdfString)"Names"u8;     // Catalog name dictionary

    // PDF Special Values
    public static readonly PdfString NoneValue = (PdfString)"None"u8;
    public static readonly PdfString DefaultValue = (PdfString)"Default"u8;

    // Color/ICC and CIE /Lab related keys
    public static readonly PdfString AlternateKey = (PdfString)"Alternate"u8;
    public static readonly PdfString WhitePointKey = (PdfString)"WhitePoint"u8;
    public static readonly PdfString GammaKey = (PdfString)"Gamma"u8;
    public static readonly PdfString BlackPointKey = (PdfString)"BlackPoint"u8;
    public static readonly PdfString RangeKey = (PdfString)"Range"u8; // /Lab range specification
    public static readonly PdfString OutputIntentsKey = (PdfString)"OutputIntents"u8;        // Catalog array of output intents
    public static readonly PdfString DestOutputProfileKey = (PdfString)"DestOutputProfile"u8; // Output intent profile stream
    public static readonly PdfString OutputIntentSubtypeKey = (PdfString)"S"u8;               // Output intent subtype
    public static readonly PdfString OutputConditionKey = (PdfString)"OutputCondition"u8;
    public static readonly PdfString OutputConditionIdentifierKey = (PdfString)"OutputConditionIdentifier"u8;
    public static readonly PdfString RegistryNameKey = (PdfString)"RegistryName"u8;
    public static readonly PdfString OutputIntentInfoKey = (PdfString)"Info"u8;               // Output intent text string, unlike the trailer /Info dictionary
    public static readonly PdfString DestOutputProfileRefKey = (PdfString)"DestOutputProfileRef"u8; // Output intent referenced ICC profile information (PDF 2.0)
    public static readonly PdfString ColorantTableKey = (PdfString)"ColorantTable"u8;
    public static readonly PdfString ICCVersionKey = (PdfString)"ICCVersion"u8;
    public static readonly PdfString ProfileCSKey = (PdfString)"ProfileCS"u8;
    public static readonly PdfString ProfileNameKey = (PdfString)"ProfileName"u8;
    public static readonly PdfString URLsKey = (PdfString)"URLs"u8;
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

    // Function related
    public static readonly PdfString BitsPerSampleKey = (PdfString)"BitsPerSample"u8; // Function type 0 sampled function BPS
    public static readonly PdfString TintTransformKey = (PdfString)"TintTransform"u8; // Separation/DeviceN attribute (when dictionary form used)

    // Annotation-related Dictionary Keys
    public static readonly PdfString AnnotationKey = (PdfString)"Annot"u8;            // Annotation type
    public static readonly PdfString AnnotsKey = (PdfString)"Annots"u8;               // Page annotations array
    public static readonly PdfString RectKey = (PdfString)"Rect"u8;                   // Annotation rectangle
    public static readonly PdfString ModificationDateKey = (PdfString)"M"u8;          // Modification date
    public static readonly PdfString AppearanceKey = (PdfString)"AP"u8;               // Appearance dictionary
    public static readonly PdfString AppearanceStateKey = (PdfString)"AS"u8;          // Appearance state
    public static readonly PdfString RolloverKey = (PdfString)"R"u8;                  // Rollover appearance state
    public static readonly PdfString DownKey = (PdfString)"D"u8;                      // Down appearance state
    public static readonly PdfString BorderStyleKey = (PdfString)"BS"u8;              // Border style dictionary
    public static readonly PdfString ColorKey = (PdfString)"C"u8;                     // Color array
    public static readonly PdfString StructParentKey = (PdfString)"StructParent"u8;   // Structural parent
    public static readonly PdfString OptionalContentKey = (PdfString)"OC"u8;          // Optional content
    public static readonly PdfString OptionalContentGroupsKey = (PdfString)"OCGs"u8;  // Optional content groups
    public static readonly PdfString VisibilityPolicyKey = (PdfString)"P"u8;           // Visibility policy
    public static readonly PdfString VisibilityExpressionKey = (PdfString)"VE"u8;      // Visibility expression (OCMD)
    public static readonly PdfString PropertiesKey = (PdfString)"Properties"u8;        // Properties subdictionary
    public static readonly PdfString OCPropertiesKey = (PdfString)"OCProperties"u8;    // Catalog optional content properties
    public static readonly PdfString OnKey = (PdfString)"ON"u8;                        // Default-on OCG array
    public static readonly PdfString OffKey = (PdfString)"OFF"u8;                      // Default-off OCG array
    public static readonly PdfString OrderKey = (PdfString)"Order"u8;                  // OCG presentation order array
    public static readonly PdfString DefaultConfigKey = (PdfString)"D"u8;              // Default OC configuration dictionary
    public static readonly PdfString ActualTextKey = (PdfString)"ActualText"u8;        // Replacement text for marked content
    public static readonly PdfString LangKey = (PdfString)"Lang"u8;                    // Language tag for marked content
    public static readonly PdfString MCIDKey = (PdfString)"MCID"u8;                    // Marked content identifier

    // Border style dictionary keys
    public static readonly PdfString SKey = (PdfString)"S"u8;                         // Style (also used for action subtype)
    public static readonly PdfString DashArrayKey = (PdfString)"D"u8;                 // Dash array (reuses "D")

    // Border effect dictionary keys
    public static readonly PdfString BorderEffectKey = (PdfString)"BE"u8;             // Border effect dictionary
    public static readonly PdfString IntensityKey = (PdfString)"I"u8;                 // Border effect intensity (0–2)

    // Text Annotation specific keys
    public static readonly PdfString OpenKey = (PdfString)"Open"u8;                   // Text annotation open state
    public static readonly PdfString StateModelKey = (PdfString)"StateModel"u8;       // Annotation state model
    public static readonly PdfString StateKey = (PdfString)"State"u8;                 // Annotation state

    // Caret Annotation specific keys
    public static readonly PdfString SymbolKey = (PdfString)"Sy"u8;                   // Caret symbol type (P = paragraph, None = plain)
    public static readonly PdfString RectDifferencesKey = (PdfString)"RD"u8;          // Rectangle differences [left top right bottom] inset from /Rect

    // Ink Annotation specific keys
    public static readonly PdfString InkListKey = (PdfString)"InkList"u8;             // Ink annotation path list
    public static readonly PdfString BorderKey = (PdfString)"Border"u8;               // Annotation border style

    // Circle/Square Annotation specific keys
    public static readonly PdfString InteriorColorKey = (PdfString)"IC"u8;            // Interior color for circle/square annotations

    // Line Annotation specific keys
    public static readonly PdfString LKey = (PdfString)"L"u8;                         // Line coordinates [x1, y1, x2, y2]
    public static readonly PdfString LineEndingKey = (PdfString)"LE"u8;               // Line ending styles array
    public static readonly PdfString LeaderLineKey = (PdfString)"LL"u8;               // Leader line length
    public static readonly PdfString LeaderLineExtensionKey = (PdfString)"LLE"u8;     // Leader line extension
    public static readonly PdfString LeaderLineOffsetKey = (PdfString)"LLO"u8;        // Leader line offset
    public static readonly PdfString CaptionKey = (PdfString)"Cap"u8;                 // Caption flag
    public static readonly PdfString CaptionPositionKey = (PdfString)"CP"u8;          // Caption position
    public static readonly PdfString MeasureKey = (PdfString)"Measure"u8;             // Measure dictionary
    public static readonly PdfString CaptionOffsetKey = (PdfString)"CO"u8;            // Caption offset

    // Polygon/PolyLine Annotation specific keys
    public static readonly PdfString VerticesKey = (PdfString)"Vertices"u8;           // Polygon/PolyLine vertices array

    // Text markup annotation keys
    public static readonly PdfString QuadPointsKey = (PdfString)"QuadPoints"u8;       // Text markup quadrilaterals array

    // Annotation popup and metadata keys
    public static readonly PdfString TitleKey = (PdfString)"T"u8;                     // Annotation title/author
    public static readonly PdfString SubjectKey = (PdfString)"Subj"u8;               // Annotation subject
    public static readonly PdfString RichContentsKey = (PdfString)"RC"u8;            // Annotation rich text contents
    public static readonly PdfString CreationDateKey = (PdfString)"CreationDate"u8;  // Annotation creation date
    public static readonly PdfString PopupKey = (PdfString)"Popup"u8;                // Reference to popup annotation
    public static readonly PdfString InReplyToKey = (PdfString)"IRT"u8;              // Annotation reply reference
    public static readonly PdfString ReplyTypeKey = (PdfString)"RT"u8;               // Annotation reply type

    // File attachment / filespec keys
    public static readonly PdfString FSKey = (PdfString)"FS"u8;                      // File specification dictionary key (for file attachment annotations)
    public static readonly PdfString EFKey = (PdfString)"EF"u8;                      // Embedded file dictionary key inside a filespec
    public static readonly PdfString UFKey = (PdfString)"UF"u8;                      // Unicode file name key inside filespec
    public static readonly PdfString FileSystemKey = (PdfString)"FS"u8;              // File system of a file specification dictionary
    public static readonly PdfString DosKey = (PdfString)"DOS"u8;                    // DOS file name inside filespec (deprecated)
    public static readonly PdfString MacKey = (PdfString)"Mac"u8;                    // Mac OS file name inside filespec (deprecated)
    public static readonly PdfString UnixKey = (PdfString)"Unix"u8;                  // UNIX file name inside filespec (deprecated)
    public static readonly PdfString VolatileKey = (PdfString)"V"u8;                 // Volatile flag of a file specification
    public static readonly PdfString RelatedFilesKey = (PdfString)"RF"u8;            // Related files arrays inside filespec
    public static readonly PdfString EncryptedPayloadKey = (PdfString)"EP"u8;        // Encrypted payload dictionary inside filespec
    public static readonly PdfString ThumbnailKey = (PdfString)"Thumb"u8;            // Thumbnail image
    public static readonly PdfString EmbeddedFilesKey = (PdfString)"EmbeddedFiles"u8; // Name tree of document-level embedded files
    public static readonly PdfString AssociatedFileRelationshipKey = (PdfString)"AFRelationship"u8;
    public static readonly PdfString ParamsKey = (PdfString)"Params"u8;              // Embedded file parameter dictionary
    public static readonly PdfString ModDateKey = (PdfString)"ModDate"u8;            // Modification date of an embedded file
    public static readonly PdfString CheckSumKey = (PdfString)"CheckSum"u8;          // MD5 checksum of an embedded file
    public static readonly PdfString VersionKey = (PdfString)"Version"u8;            // Version of an encrypted payload's crypt filter
    public static readonly PdfString FKey = (PdfString)"F"u8;                        // File name key inside filespec

    // Link annotation specific keys
    public static readonly PdfString AKey = (PdfString)"A"u8;                         // Action dictionary
    public static readonly PdfString DestKey = (PdfString)"Dest"u8;                   // Destination (for link annotations)
    public static readonly PdfString DKey = (PdfString)"D"u8;                         // Destination (for GoTo actions)
    public static readonly PdfString HighlightModeKey = (PdfString)"H"u8;             // Highlight mode
    public static readonly PdfString PAKey = (PdfString)"PA"u8;                       // URI action dictionary
    public static readonly PdfString URIKey = (PdfString)"URI"u8;                     // URI string
    public static readonly PdfString SActionKey = (PdfString)"S"u8;                   // Action subtype (alias for SKey)
    public static readonly PdfString NextKey = (PdfString)"Next"u8;                   // Next action
    public static readonly PdfString IsMapKey = (PdfString)"IsMap"u8;                 // URI action IsMap flag

    // Form field and widget annotation keys
    public static readonly PdfString AcroFormKey = (PdfString)"AcroForm"u8;           // Catalog AcroForm dictionary
    public static readonly PdfString FieldsKey = (PdfString)"Fields"u8;               // Form fields array
    public static readonly PdfString FieldTypeKey = (PdfString)"FT"u8;                // Field type
    public static readonly PdfString ValueKey = (PdfString)"V"u8;                     // Field value (reuses "V")
    public static readonly PdfString DefaultValueKey = (PdfString)"DV"u8;             // Default field value
    public static readonly PdfString QuadKey = (PdfString)"Q"u8;                      // Quadding/alignment (0=left, 1=center, 2=right)
    public static readonly PdfString DefaultAppearanceKey = (PdfString)"DA"u8;        // Default appearance string
    public static readonly PdfString MaxLenKey = (PdfString)"MaxLen"u8;               // Maximum length for text field
    public static readonly PdfString OptKey = (PdfString)"Opt"u8;                     // Options array for choice fields
    public static readonly PdfString TopIndexKey = (PdfString)"TI"u8;                 // Top index for list box
    public static readonly PdfString IndicesKey = (PdfString)"I"u8;                   // Selected indices for list box
    public static readonly PdfString AppearanceCharacteristicsKey = (PdfString)"MK"u8; // Appearance characteristics dictionary

    // Structure tree keys
    public static readonly PdfString StructTreeRootKey = (PdfString)"StructTreeRoot"u8;
    public static readonly PdfString PgKey = (PdfString)"Pg"u8;                           // Page reference in structure element
    public static readonly PdfString AltKey = (PdfString)"Alt"u8;                         // Alternative text in structure element
    public static readonly PdfString ExpandedFormKey = (PdfString)"E"u8;                  // Expanded form of abbreviation in structure element
    public static readonly PdfString ParentTreeKey = (PdfString)"ParentTree"u8;           // Number tree mapping structural parent keys to structure elements
    public static readonly PdfString IdTreeKey = (PdfString)"IDTree"u8;                   // Name tree mapping element identifiers to structure elements
    public static readonly PdfString StructParentsKey = (PdfString)"StructParents"u8;     // Structural parent key of a content stream holding marked content
    public static readonly PdfString ParentTreeNextKeyKey = (PdfString)"ParentTreeNextKey"u8; // Next structural parent key to assign
    public static readonly PdfString RoleMapKey = (PdfString)"RoleMap"u8;                 // Maps non-standard structure types onto standard ones
    public static readonly PdfString StructureTypeKey = (PdfString)"S"u8;                 // Structure type of a structure element
    public static readonly PdfString StructureParentKey = (PdfString)"P"u8;               // Parent structure element
    public static readonly PdfString ReferencedKey = (PdfString)"Ref"u8;                  // Structure elements this element references
    public static readonly PdfString PhonemeKey = (PdfString)"Phoneme"u8;                 // Pronunciation of the element's content
    public static readonly PdfString PhoneticAlphabetKey = (PdfString)"PhoneticAlphabet"u8;
    public static readonly PdfString MarkedContentReferenceKey = (PdfString)"MCR"u8;      // /Type of a marked content reference
    public static readonly PdfString ObjectReferenceKey = (PdfString)"OBJR"u8;            // /Type of an object reference
    public static readonly PdfString StreamKey = (PdfString)"Stm"u8;                      // Content stream holding the referenced marked content
    public static readonly PdfString StreamOwnerKey = (PdfString)"StmOwn"u8;              // Object owning the content stream
    public static readonly PdfString ObjectKey = (PdfString)"Obj"u8;                      // Object a structure element refers to
    public static readonly PdfString AttributesKey = (PdfString)"A"u8;                    // Attribute objects of a structure element
    public static readonly PdfString ClassKey = (PdfString)"C"u8;                         // Attribute class names of a structure element
    public static readonly PdfString ClassMapKey = (PdfString)"ClassMap"u8;               // Maps attribute class names onto attribute objects
    public static readonly PdfString NamespaceKey = (PdfString)"NS"u8;                    // Namespace of a structure element or attribute object; name of a namespace
    public static readonly PdfString NamespacesKey = (PdfString)"Namespaces"u8;           // Namespaces used in the structure tree
    public static readonly PdfString RoleMapNamespaceKey = (PdfString)"RoleMapNS"u8;      // Maps structure types of a namespace onto another namespace
    public static readonly PdfString SchemaKey = (PdfString)"Schema"u8;                   // Schema file of a namespace
    public static readonly PdfString PronunciationLexiconKey = (PdfString)"PronunciationLexicon"u8;
    public static readonly PdfString AssociatedFilesKey = (PdfString)"AF"u8;              // Associated files (PDF 2.0)

    // Structure attribute keys
    public static readonly PdfString AttributeOwnerKey = (PdfString)"O"u8;                // Owner of an attribute object
    public static readonly PdfString UserPropertiesKey = (PdfString)"P"u8;                // User properties of a UserProperties attribute object
    public static readonly PdfString UserPropertyNameKey = (PdfString)"N"u8;
    public static readonly PdfString UserPropertyFormattedValueKey = (PdfString)"F"u8;
    public static readonly PdfString UserPropertyHiddenKey = (PdfString)"H"u8;
    public static readonly PdfString PlacementKey = (PdfString)"Placement"u8;
    public static readonly PdfString WritingModeKey = (PdfString)"WritingMode"u8;
    public static readonly PdfString BackgroundColorKey = (PdfString)"BackgroundColor"u8;
    public static readonly PdfString BorderColorKey = (PdfString)"BorderColor"u8;
    public static readonly PdfString LayoutBorderStyleKey = (PdfString)"BorderStyle"u8;
    public static readonly PdfString BorderThicknessKey = (PdfString)"BorderThickness"u8;
    public static readonly PdfString PaddingKey = (PdfString)"Padding"u8;
    public static readonly PdfString LayoutColorKey = (PdfString)"Color"u8;
    public static readonly PdfString SpaceBeforeKey = (PdfString)"SpaceBefore"u8;
    public static readonly PdfString SpaceAfterKey = (PdfString)"SpaceAfter"u8;
    public static readonly PdfString StartIndentKey = (PdfString)"StartIndent"u8;
    public static readonly PdfString EndIndentKey = (PdfString)"EndIndent"u8;
    public static readonly PdfString TextIndentKey = (PdfString)"TextIndent"u8;
    public static readonly PdfString TextAlignKey = (PdfString)"TextAlign"u8;
    public static readonly PdfString BlockAlignKey = (PdfString)"BlockAlign"u8;
    public static readonly PdfString InlineAlignKey = (PdfString)"InlineAlign"u8;
    public static readonly PdfString TableBorderStyleKey = (PdfString)"TBorderStyle"u8;
    public static readonly PdfString TablePaddingKey = (PdfString)"TPadding"u8;
    public static readonly PdfString BaselineShiftKey = (PdfString)"BaselineShift"u8;
    public static readonly PdfString LineHeightKey = (PdfString)"LineHeight"u8;
    public static readonly PdfString TextPositionKey = (PdfString)"TextPosition"u8;
    public static readonly PdfString TextDecorationColorKey = (PdfString)"TextDecorationColor"u8;
    public static readonly PdfString TextDecorationThicknessKey = (PdfString)"TextDecorationThickness"u8;
    public static readonly PdfString TextDecorationTypeKey = (PdfString)"TextDecorationType"u8;
    public static readonly PdfString RubyAlignKey = (PdfString)"RubyAlign"u8;
    public static readonly PdfString RubyPositionKey = (PdfString)"RubyPosition"u8;
    public static readonly PdfString GlyphOrientationVerticalKey = (PdfString)"GlyphOrientationVertical"u8;
    public static readonly PdfString ColumnCountKey = (PdfString)"ColumnCount"u8;
    public static readonly PdfString ColumnGapKey = (PdfString)"ColumnGap"u8;
    public static readonly PdfString ColumnWidthsKey = (PdfString)"ColumnWidths"u8;
    public static readonly PdfString ListNumberingKey = (PdfString)"ListNumbering"u8;
    public static readonly PdfString ContinuedListKey = (PdfString)"ContinuedList"u8;
    public static readonly PdfString ContinuedFromKey = (PdfString)"ContinuedFrom"u8;
    public static readonly PdfString RoleKey = (PdfString)"Role"u8;
    public static readonly PdfString CheckedKey = (PdfString)"Checked"u8;
    public static readonly PdfString CheckedLowercaseKey = (PdfString)"checked"u8;        // Deprecated in PDF 2.0
    public static readonly PdfString DescriptionKey = (PdfString)"Desc"u8;
    public static readonly PdfString RowSpanKey = (PdfString)"RowSpan"u8;
    public static readonly PdfString ColumnSpanKey = (PdfString)"ColSpan"u8;
    public static readonly PdfString HeadersKey = (PdfString)"Headers"u8;
    public static readonly PdfString ScopeKey = (PdfString)"Scope"u8;
    public static readonly PdfString SummaryKey = (PdfString)"Summary"u8;
    public static readonly PdfString ShortKey = (PdfString)"Short"u8;
    public static readonly PdfString NoteTypeKey = (PdfString)"NoteType"u8;

    // Page label number tree keys
    public static readonly PdfString PageLabelsKey = (PdfString)"PageLabels"u8;
    public static readonly PdfString NumsKey = (PdfString)"Nums"u8;
    public static readonly PdfString LimitsKey = (PdfString)"Limits"u8;
    public static readonly PdfString PrefixKey = (PdfString)"P"u8;
    public static readonly PdfString StyleKey = (PdfString)"S"u8;
    public static readonly PdfString StartKey = (PdfString)"St"u8;

    // Document information dictionary keys
    public static readonly PdfString InformationTitleKey = (PdfString)"Title"u8;
    public static readonly PdfString AuthorKey = (PdfString)"Author"u8;
    public static readonly PdfString InformationSubjectKey = (PdfString)"Subject"u8;
    public static readonly PdfString KeywordsKey = (PdfString)"Keywords"u8;
    public static readonly PdfString CreatorKey = (PdfString)"Creator"u8;
    public static readonly PdfString ProducerKey = (PdfString)"Producer"u8;
    public static readonly PdfString TrappedKey = (PdfString)"Trapped"u8;

    // Catalog keys
    public static readonly PdfString CatalogVersionKey = (PdfString)"Version"u8;          // Catalog version name
    public static readonly PdfString ExtensionsKey = (PdfString)"Extensions"u8;
    public static readonly PdfString BaseVersionKey = (PdfString)"BaseVersion"u8;
    public static readonly PdfString ExtensionLevelKey = (PdfString)"ExtensionLevel"u8;
    public static readonly PdfString UrlKey = (PdfString)"URL"u8;
    public static readonly PdfString ExtensionRevisionKey = (PdfString)"ExtensionRevision"u8;
    public static readonly PdfString MarkInfoKey = (PdfString)"MarkInfo"u8;
    public static readonly PdfString MarkedKey = (PdfString)"Marked"u8;
    public static readonly PdfString MarkInfoUserPropertiesKey = (PdfString)"UserProperties"u8; // Mark information user properties flag
    public static readonly PdfString SuspectsKey = (PdfString)"Suspects"u8;
    public static readonly PdfString UriDictionaryKey = (PdfString)"URI"u8;              // Catalog URI dictionary
    public static readonly PdfString BaseKey = (PdfString)"Base"u8;
    public static readonly PdfString PageModeKey = (PdfString)"PageMode"u8;
    public static readonly PdfString PageLayoutKey = (PdfString)"PageLayout"u8;
    public static readonly PdfString NeedsRenderingKey = (PdfString)"NeedsRendering"u8;
    public static readonly PdfString SigFlagsKey = (PdfString)"SigFlags"u8;

    // Optional content keys
    public static readonly PdfString OptionalContentIntentKey = (PdfString)"Intent"u8;   // Optional content intent name or array
    public static readonly PdfString UsageKey = (PdfString)"Usage"u8;
    public static readonly PdfString ConfigsKey = (PdfString)"Configs"u8;
    public static readonly PdfString BaseStateKey = (PdfString)"BaseState"u8;
    public static readonly PdfString ListModeKey = (PdfString)"ListMode"u8;
    public static readonly PdfString RBGroupsKey = (PdfString)"RBGroups"u8;
    public static readonly PdfString LockedKey = (PdfString)"Locked"u8;
    public static readonly PdfString AutoStateKey = (PdfString)"AS"u8;                   // Optional content usage application array
    public static readonly PdfString EventKey = (PdfString)"Event"u8;
    public static readonly PdfString CategoryKey = (PdfString)"Category"u8;
    public static readonly PdfString CreatorInfoKey = (PdfString)"CreatorInfo"u8;
    public static readonly PdfString LanguageKey = (PdfString)"Language"u8;
    public static readonly PdfString PreferredKey = (PdfString)"Preferred"u8;
    public static readonly PdfString ExportKey = (PdfString)"Export"u8;
    public static readonly PdfString ExportStateKey = (PdfString)"ExportState"u8;
    public static readonly PdfString ZoomKey = (PdfString)"Zoom"u8;
    public static readonly PdfString MinKey = (PdfString)"min"u8;
    public static readonly PdfString MaxKey = (PdfString)"max"u8;
    public static readonly PdfString PrintKey = (PdfString)"Print"u8;
    public static readonly PdfString PrintStateKey = (PdfString)"PrintState"u8;
    public static readonly PdfString ViewKey = (PdfString)"View"u8;
    public static readonly PdfString ViewStateKey = (PdfString)"ViewState"u8;
    public static readonly PdfString UserKey = (PdfString)"User"u8;
    public static readonly PdfString UserNameKey = (PdfString)"Name"u8;                 // Usage user name text string or array
    public static readonly PdfString PageElementKey = (PdfString)"PageElement"u8;

    // Page object keys
    public static readonly PdfString LastModifiedKey = (PdfString)"LastModified"u8;
    public static readonly PdfString DurKey = (PdfString)"Dur"u8;
    public static readonly PdfString TabsKey = (PdfString)"Tabs"u8;
    public static readonly PdfString TemplateInstantiatedKey = (PdfString)"TemplateInstantiated"u8;
    public static readonly PdfString WebCaptureIdKey = (PdfString)"ID"u8;                // Page web capture identifier
    public static readonly PdfString PZKey = (PdfString)"PZ"u8;
    public static readonly PdfString UserUnitKey = (PdfString)"UserUnit"u8;
}
