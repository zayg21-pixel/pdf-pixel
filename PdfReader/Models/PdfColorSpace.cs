namespace PdfReader.Models
{
    /// <summary>
    /// PDF color spaces enumeration
    /// Represents the various color spaces supported by PDF documents
    /// </summary>
    public enum PdfColorSpace
    {
        /// <summary>
        /// Device-dependent gray color space - single component (0=black, 1=white)
        /// </summary>
        DeviceGray,
        
        /// <summary>
        /// Device-dependent RGB color space - three components (red, green, blue)
        /// </summary>
        DeviceRGB,
        
        /// <summary>
        /// Device-dependent CMYK color space - four components (cyan, magenta, yellow, black)
        /// </summary>
        DeviceCMYK,
        
        /// <summary>
        /// ICC-based color space - uses ICC color profiles for device-independent color
        /// </summary>
        ICCBased,
        
        /// <summary>
        /// Indexed color space - uses a color table to map indices to colors
        /// </summary>
        Indexed,
        
        /// <summary>
        /// Pattern color space - for tiling patterns and shading patterns
        /// </summary>
        Pattern,
        
        /// <summary>
        /// Separation color space - for spot colors and special inks
        /// </summary>
        Separation,
        
        /// <summary>
        /// DeviceN color space - for multiple spot colors
        /// </summary>
        DeviceN,
        
        /// <summary>
        /// Lab color space - CIE-based color space with lightness and chromaticity
        /// </summary>
        Lab,
        
        /// <summary>
        /// CalGray color space - CIE-based gray color space
        /// </summary>
        CalGray,
        
        /// <summary>
        /// CalRGB color space - CIE-based RGB color space
        /// </summary>
        CalRGB,
        
        /// <summary>
        /// Unknown or custom color space
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Color space name constants for PDF color spaces
    /// </summary>
    public static class PdfColorSpaceNames
    {
        /// <summary>Device-dependent gray color space</summary>
        public const string DeviceGray = "/DeviceGray";
        
        /// <summary>Device-dependent RGB color space</summary>
        public const string DeviceRGB = "/DeviceRGB";
        
        /// <summary>Device-dependent CMYK color space</summary>
        public const string DeviceCMYK = "/DeviceCMYK";
        
        /// <summary>ICC-based color space</summary>
        public const string ICCBased = "/ICCBased";
        
        /// <summary>Indexed color space</summary>
        public const string Indexed = "/Indexed";
        
        /// <summary>Pattern color space</summary>
        public const string Pattern = "/Pattern";
        
        /// <summary>Separation color space</summary>
        public const string Separation = "/Separation";
        
        /// <summary>DeviceN color space</summary>
        public const string DeviceN = "/DeviceN";
        
        /// <summary>Lab color space</summary>
        public const string Lab = "/Lab";
        
        /// <summary>CalGray color space</summary>
        public const string CalGray = "/CalGray";
        
        /// <summary>CalRGB color space</summary>
        public const string CalRGB = "/CalRGB";
    }
}