using PdfPixel.Text;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// PDF color spaces enumeration
/// Represents the various color spaces supported by PDF documents
/// </summary>
[PdfEnum]
public enum PdfColorSpaceType
{
    [PdfEnumDefaultValue]
    Unknown,

    /// <summary>
    /// Device-dependent gray color space - single component (0=black, 1=white)
    /// </summary>
    [PdfEnumValue("DeviceGray")]
    DeviceGray,
    
    /// <summary>
    /// Device-dependent RGB color space - three components (red, green, blue)
    /// </summary>
    [PdfEnumValue("DeviceRGB")]
    DeviceRGB,
    
    /// <summary>
    /// Device-dependent CMYK color space - four components (cyan, magenta, yellow, black)
    /// </summary>
    [PdfEnumValue("DeviceCMYK")]
    DeviceCMYK,
    
    /// <summary>
    /// ICC-based color space - uses ICC color profiles for device-independent color
    /// </summary>
    [PdfEnumValue("ICCBased")]
    ICCBased,
    
    /// <summary>
    /// Indexed color space - uses a color table to map indices to colors
    /// </summary>
    [PdfEnumValue("Indexed")]
    Indexed,
    
    /// <summary>
    /// Pattern color space - for tiling patterns and shading patterns
    /// </summary>
    [PdfEnumValue("Pattern")]
    Pattern,
    
    /// <summary>
    /// Separation color space - for spot colors and special inks
    /// </summary>
    [PdfEnumValue("Separation")]
    Separation,
    
    /// <summary>
    /// DeviceN color space - for multiple spot colors
    /// </summary>
    [PdfEnumValue("DeviceN")]
    DeviceN,
    
    /// <summary>
    /// Lab color space - CIE-based color space with lightness and chromaticity
    /// </summary>
    [PdfEnumValue("Lab")]
    Lab,
    
    /// <summary>
    /// CalGray color space - CIE-based gray color space
    /// </summary>
    [PdfEnumValue("CalGray")]
    CalGray,
    
    /// <summary>
    /// CalRGB color space - CIE-based RGB color space
    /// </summary>
    [PdfEnumValue("CalRGB")]
    CalRGB
}