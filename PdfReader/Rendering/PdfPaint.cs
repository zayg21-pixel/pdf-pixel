using System;
using SkiaSharp;
using PdfReader.Rendering.Pattern;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Represents the current paint (color or pattern) for stroke/fill operations.
    /// Aggregates solid color, tiling pattern reference, and optional pattern components (tint values for uncolored patterns).
    /// Immutable value object; create a new instance when paint changes.
    /// </summary>
    public sealed class PdfPaint
    {
        /// <summary>
        /// Gets the resolved sRGB color for solid color paints, or a placeholder color for pattern paints.
        /// For uncolored (stencil) patterns this color typically represents the resolved tint in the base color space.
        /// </summary>
        public SKColor Color { get; }

        /// <summary>
        /// Gets the pattern definition when the paint represents a pattern; null for solid color paints.
        /// </summary>
        public PdfPattern Pattern { get; }

        /// <summary>
        /// Gets a copy of the pattern tint components (base color space components) for uncolored patterns.
        /// Null for solid color paints or colored patterns.
        /// </summary>
        public float[] PatternComponents { get; }

        /// <summary>
        /// True if this paint represents a pattern (colored or uncolored), otherwise false for solid colors.
        /// </summary>
        public bool IsPattern => Pattern != null;

        private PdfPaint(SKColor color, PdfPattern pattern, float[] patternComponents)
        {
            Color = color;
            Pattern = pattern;
            if (patternComponents != null && patternComponents.Length > 0)
            {
                PatternComponents = (float[])patternComponents.Clone();
            }
        }

        /// <summary>
        /// Create a solid color paint.
        /// </summary>
        public static PdfPaint Solid(SKColor color)
        {
            return new PdfPaint(color, null, null);
        }

        /// <summary>
        /// Create a pattern paint (colored or uncolored). For uncolored patterns provide tint components.
        /// </summary>
        public static PdfPaint PatternFill(PdfPattern pattern, float[] patternComponents, SKColor resolvedTintColor)
        {
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }
            return new PdfPaint(resolvedTintColor, pattern, patternComponents);
        }

        /// <summary>
        /// For uncolored patterns, returns the resolved tint color if available (Color property), otherwise black.
        /// For colored patterns or solid colors returns Color.
        /// </summary>
        public SKColor GetEffectiveColor()
        {
            return Color;
        }
    }
}
