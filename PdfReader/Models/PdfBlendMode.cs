using SkiaSharp;

namespace PdfReader.Models
{
    /// <summary>
    /// PDF blend modes enumeration
    /// Represents the blend modes supported by PDF documents for transparency operations
    /// Based on PDF Reference Section 7.2.4
    /// </summary>
    public enum PdfBlendMode
    {
        /// <summary>
        /// Normal blend mode - default, no special blending (B = S)
        /// </summary>
        Normal,
        
        /// <summary>
        /// Multiply blend mode - multiply source and backdrop colors
        /// </summary>
        Multiply,
        
        /// <summary>
        /// Screen blend mode - inverse of multiply
        /// </summary>
        Screen,
        
        /// <summary>
        /// Overlay blend mode - multiply or screen depending on backdrop color
        /// </summary>
        Overlay,
        
        /// <summary>
        /// Soft light blend mode - darkens or lightens depending on source color
        /// </summary>
        SoftLight,
        
        /// <summary>
        /// Hard light blend mode - multiply or screen depending on source color
        /// </summary>
        HardLight,
        
        /// <summary>
        /// Color dodge blend mode - brightens backdrop color
        /// </summary>
        ColorDodge,
        
        /// <summary>
        /// Color burn blend mode - darkens backdrop color
        /// </summary>
        ColorBurn,
        
        /// <summary>
        /// Darken blend mode - selects darker color
        /// </summary>
        Darken,
        
        /// <summary>
        /// Lighten blend mode - selects lighter color
        /// </summary>
        Lighten,
        
        /// <summary>
        /// Difference blend mode - subtracts colors
        /// </summary>
        Difference,
        
        /// <summary>
        /// Exclusion blend mode - similar to difference but with lower contrast
        /// </summary>
        Exclusion,
        
        /// <summary>
        /// Hue blend mode - uses hue of source with saturation and luminosity of backdrop
        /// </summary>
        Hue,
        
        /// <summary>
        /// Saturation blend mode - uses saturation of source with hue and luminosity of backdrop
        /// </summary>
        Saturation,
        
        /// <summary>
        /// Color blend mode - uses hue and saturation of source with luminosity of backdrop
        /// </summary>
        Color,
        
        /// <summary>
        /// Luminosity blend mode - uses luminosity of source with hue and saturation of backdrop
        /// </summary>
        Luminosity,
        
        /// <summary>
        /// Compatible blend mode - implementation-defined blending
        /// </summary>
        Compatible
    }

    /// <summary>
    /// PDF blend mode name constants and utilities
    /// </summary>
    public static class PdfBlendModeNames
    {
        /// <summary>Normal blend mode</summary>
        public const string Normal = "Normal";
        
        /// <summary>Multiply blend mode</summary>
        public const string Multiply = "Multiply";
        
        /// <summary>Screen blend mode</summary>
        public const string Screen = "Screen";
        
        /// <summary>Overlay blend mode</summary>
        public const string Overlay = "Overlay";
        
        /// <summary>Soft light blend mode</summary>
        public const string SoftLight = "SoftLight";
        
        /// <summary>Hard light blend mode</summary>
        public const string HardLight = "HardLight";
        
        /// <summary>Color dodge blend mode</summary>
        public const string ColorDodge = "ColorDodge";
        
        /// <summary>Color burn blend mode</summary>
        public const string ColorBurn = "ColorBurn";
        
        /// <summary>Darken blend mode</summary>
        public const string Darken = "Darken";
        
        /// <summary>Lighten blend mode</summary>
        public const string Lighten = "Lighten";
        
        /// <summary>Difference blend mode</summary>
        public const string Difference = "Difference";
        
        /// <summary>Exclusion blend mode</summary>
        public const string Exclusion = "Exclusion";
        
        /// <summary>Hue blend mode</summary>
        public const string Hue = "Hue";
        
        /// <summary>Saturation blend mode</summary>
        public const string Saturation = "Saturation";
        
        /// <summary>Color blend mode</summary>
        public const string Color = "Color";
        
        /// <summary>Luminosity blend mode</summary>
        public const string Luminosity = "Luminosity";
        
        /// <summary>Compatible blend mode</summary>
        public const string Compatible = "Compatible";

        /// <summary>
        /// Parse PDF blend mode name to enum
        /// </summary>
        /// <param name="blendModeName">PDF blend mode name</param>
        /// <returns>Parsed blend mode enum value</returns>
        public static PdfBlendMode ParseBlendMode(string blendModeName)
        {
            if (string.IsNullOrEmpty(blendModeName))
                return PdfBlendMode.Normal; // Default to Normal if null/empty

            if (blendModeName.StartsWith("/"))
                blendModeName = blendModeName.Substring(1); // Remove leading slash if present

            return blendModeName switch
            {
                Normal => PdfBlendMode.Normal,
                Multiply => PdfBlendMode.Multiply,
                Screen => PdfBlendMode.Screen,
                Overlay => PdfBlendMode.Overlay,
                SoftLight => PdfBlendMode.SoftLight,
                HardLight => PdfBlendMode.HardLight,
                ColorDodge => PdfBlendMode.ColorDodge,
                ColorBurn => PdfBlendMode.ColorBurn,
                Darken => PdfBlendMode.Darken,
                Lighten => PdfBlendMode.Lighten,
                Difference => PdfBlendMode.Difference,
                Exclusion => PdfBlendMode.Exclusion,
                Hue => PdfBlendMode.Hue,
                Saturation => PdfBlendMode.Saturation,
                Color => PdfBlendMode.Color,
                Luminosity => PdfBlendMode.Luminosity,
                Compatible => PdfBlendMode.Compatible,
                _ => PdfBlendMode.Normal // Default fallback
            };
        }

        /// <summary>
        /// Convert PDF blend mode to SkiaSharp blend mode
        /// Note: Some PDF blend modes may not have exact SkiaSharp equivalents
        /// </summary>
        /// <param name="pdfBlendMode">PDF blend mode</param>
        /// <returns>Corresponding SkiaSharp blend mode</returns>
        public static SKBlendMode ToSkiaBlendMode(PdfBlendMode pdfBlendMode)
        {
            return pdfBlendMode switch
            {
                PdfBlendMode.Normal => SKBlendMode.SrcOver,
                PdfBlendMode.Multiply => SKBlendMode.Multiply,
                PdfBlendMode.Screen => SKBlendMode.Screen,
                PdfBlendMode.Overlay => SKBlendMode.Overlay,
                PdfBlendMode.SoftLight => SKBlendMode.SoftLight,
                PdfBlendMode.HardLight => SKBlendMode.HardLight,
                PdfBlendMode.ColorDodge => SKBlendMode.ColorDodge,
                PdfBlendMode.ColorBurn => SKBlendMode.ColorBurn,
                PdfBlendMode.Darken => SKBlendMode.Darken,
                PdfBlendMode.Lighten => SKBlendMode.Lighten,
                PdfBlendMode.Difference => SKBlendMode.Difference,
                PdfBlendMode.Exclusion => SKBlendMode.Exclusion,
                PdfBlendMode.Hue => SKBlendMode.Hue,
                PdfBlendMode.Saturation => SKBlendMode.Saturation,
                PdfBlendMode.Color => SKBlendMode.Color,
                PdfBlendMode.Luminosity => SKBlendMode.Luminosity,
                PdfBlendMode.Compatible => SKBlendMode.SrcOver, // Default to normal
                _ => SKBlendMode.SrcOver // Default fallback
            };
        }
    }
}