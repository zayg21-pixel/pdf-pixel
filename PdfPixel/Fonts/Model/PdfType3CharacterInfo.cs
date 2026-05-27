using PdfPixel.Commands;
using SkiaSharp;

namespace PdfPixel.Fonts.Model
{
    /// <summary>
    /// Holds rendering result and metrics for a Type 3 character after executing its CharProc stream.
    /// Extracted from the graphics state (d0/d1 operators) after glyph content is rendered.
    /// </summary>
    public sealed class PdfType3CharacterInfo
    {
        /// <summary>
        /// Gets a singleton instance representing an undefined character (no recording/metrics available).
        /// </summary>
        public static PdfType3CharacterInfo Undefined { get; } = new(null, null, SKSize.Empty);

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfType3CharacterInfo"/> class.
        /// </summary>
        /// <param name="recording">Recorded drawing commands for the glyph; null when not defined.</param>
        /// <param name="bbox">Bounding box parsed by d1 (llx, lly, urx, ury). Null when not provided.</param>
        /// <param name="advancement">Glyph advancement vector parsed by d0/d1 (wx, wy). If not provided, use <see cref="SKSize.Empty"/>.</param>
        public PdfType3CharacterInfo(PdfCommandRecorder? recording, SKRect? bbox, SKSize advancement)
        {
            Recording = recording;
            BBox = bbox;
            Advancement = advancement;
        }

        /// <summary>
        /// True when the character procedure was found and rendered (i.e., recording is not null).
        /// </summary>
        public bool IsDefined => Recording != null;

        /// <summary>
        /// Gets the recorded commands that draw the glyph in glyph space.
        /// </summary>
        public PdfCommandRecorder? Recording { get; }

        /// <summary>
        /// Gets the character bounding box from d1 if provided; otherwise null.
        /// </summary>
        public SKRect? BBox { get; }

        /// <summary>
        /// Gets the glyph advancement vector (wx, wy) from d0/d1. Defaults to <see cref="SKSize.Empty"/> when not provided.
        /// </summary>
        public SKSize Advancement { get; }

        /// <summary>
        /// Gets a value indicating whether the character is colored. Defined as BBox being provided.
        /// </summary>
        public bool IsColored => !BBox.HasValue;
    }
}
