namespace PdfReader.Models
{
    /// <summary>
    /// PDF rendering intents as defined in PDF specification section 11.7.5
    /// Controls how colors are mapped between different color spaces and devices
    /// </summary>
    public enum PdfRenderingIntent
    {
        /// <summary>
        /// RelativeColorimetric intent - preserves color accuracy within the target gamut.
        /// Out-of-gamut colors are mapped to the closest reproducible color.
        /// Preserves the white point relationship between source and destination.
        /// Best for images where color accuracy is important but some gamut compression is acceptable.
        /// </summary>
        RelativeColorimetric,
        
        /// <summary>
        /// AbsoluteColorimetric intent - preserves exact color values.
        /// Maintains absolute color accuracy without white point adaptation.
        /// Best for proofing applications where exact color reproduction is critical.
        /// </summary>
        AbsoluteColorimetric,
        
        /// <summary>
        /// Perceptual intent - optimizes for overall image appearance.
        /// Compresses the entire source gamut to fit the destination gamut.
        /// Best for photographic images where overall appearance matters more than exact colors.
        /// </summary>
        Perceptual,
        
        /// <summary>
        /// Saturation intent - optimizes for vivid colors.
        /// Preserves color saturation at the expense of color accuracy.
        /// Best for business graphics where vivid, saturated colors are desired.
        /// </summary>
        Saturation
    }

    /// <summary>
    /// Constants for PDF rendering intent names as they appear in PDF documents
    /// </summary>
    public static class PdfRenderingIntentNames
    {
        /// <summary>RelativeColorimetric rendering intent</summary>
        public const string RelativeColorimetric = "/RelativeColorimetric";
        
        /// <summary>AbsoluteColorimetric rendering intent</summary>
        public const string AbsoluteColorimetric = "/AbsoluteColorimetric";
        
        /// <summary>Perceptual rendering intent</summary>
        public const string Perceptual = "/Perceptual";
        
        /// <summary>Saturation rendering intent</summary>
        public const string Saturation = "/Saturation";
    }

    /// <summary>
    /// Utility methods for working with PDF rendering intents
    /// </summary>
    public static class PdfRenderingIntentUtilities
    {
        /// <summary>
        /// Parse a PDF rendering intent name to enum value
        /// </summary>
        /// <param name="intentName">PDF rendering intent name (e.g., "/RelativeColorimetric")</param>
        /// <returns>Parsed rendering intent, defaults to RelativeColorimetric if unknown</returns>
        public static PdfRenderingIntent ParseRenderingIntent(string intentName)
        {
            return intentName switch
            {
                PdfRenderingIntentNames.RelativeColorimetric => PdfRenderingIntent.RelativeColorimetric,
                PdfRenderingIntentNames.AbsoluteColorimetric => PdfRenderingIntent.AbsoluteColorimetric,
                PdfRenderingIntentNames.Perceptual => PdfRenderingIntent.Perceptual,
                PdfRenderingIntentNames.Saturation => PdfRenderingIntent.Saturation,
                _ => PdfRenderingIntent.RelativeColorimetric // Default per PDF spec
            };
        }
    }
}