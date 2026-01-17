namespace PdfRender.Color.Icc.Model;

internal static class IccConstants
{
    // Color spaces (profile header)
    public const string SpaceGray = "GRAY";
    public const string SpaceRgb  = "RGB ";
    public const string SpaceCmyk = "CMYK";
    public const string SpaceLab  = "Lab ";
    public const string SpaceXyz  = "XYZ ";
    public const string SpaceLuv  = "Luv ";
    public const string SpaceYcbcr = "YCbr";
    public const string SpaceYxy  = "Yxy ";
    public const string SpaceHsv  = "HSV ";
    public const string SpaceHls  = "HLS ";
    public const string SpaceCmy  = "CMY ";

    // Type signatures (4CC)
    public const string TypeXYZ   = "XYZ ";
    public const string TypeCurv  = "curv";
    public const string TypePara  = "para";
    public const string TypeDesc  = "desc";
    public const string TypeMluc  = "mluc";
    public const string TypeMAB   = "mAB ";
    public const string TypeMBA   = "mBA ";
    // Legacy LUT tag types per ICC spec: mft1 == lut8Type, mft2 == lut16Type
    public const string TypeLut8  = "mft1"; // formerly incorrectly named "lut8"
    public const string TypeLut16 = "mft2"; // formerly incorrectly named "lut16"

    // Tag signatures (also 4CC but compared as strings in our model)
    public const string TagWtpt = "wtpt";
    public const string TagBkpt = "bkpt";
    public const string Tag_rXYZ = "rXYZ";
    public const string Tag_gXYZ = "gXYZ";
    public const string Tag_bXYZ = "bXYZ";
    public const string Tag_rTRC = "rTRC";
    public const string Tag_gTRC = "gTRC";
    public const string Tag_bTRC = "bTRC";
    public const string Tag_kTRC = "kTRC";
    public const string TagChad = "chad";
    public const string TagDesc = "desc";
    public const string TagMluc = "mluc";

    // Pipeline tags (intents 0..2 variants)
    public const string TagA2B0 = "A2B0";
    public const string TagA2B1 = "A2B1";
    public const string TagA2B2 = "A2B2";
    public const string TagB2A0 = "B2A0";
    public const string TagB2A1 = "B2A1";
    public const string TagB2A2 = "B2A2";
}
