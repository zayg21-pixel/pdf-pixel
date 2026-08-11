using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Collects a single "glyf" glyph's points into one flat <see cref="GlyphOutline"/>, resolving a
/// composite glyph's components - whether they carry an x/y offset or match a pair of points - into
/// that same outline. What the outline is then used for is the caller's choice:
/// <see cref="SfntGlyphRepacker"/> writes it back as glyph bytes,
/// <see cref="SfntGlyphPathEmitter"/> turns it into a path.
/// </summary>
public class SfntGlyphEvaluator
{
    private const int MaxComponentNestingDepth = 8;

    private const ushort ComponentArg1And2AreWords = 0x0001;
    private const ushort ComponentArgsAreXyValues = 0x0002;
    private const ushort ComponentWeHaveAScale = 0x0008;
    private const ushort ComponentMoreComponents = 0x0020;
    private const ushort ComponentWeHaveAnXAndYScale = 0x0040;
    private const ushort ComponentWeHaveATwoByTwo = 0x0080;
    private const ushort ComponentWeHaveInstructions = 0x0100;

    private readonly ILogger<SfntGlyphEvaluator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntGlyphEvaluator"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during evaluation.</param>
    public SfntGlyphEvaluator(ILogger<SfntGlyphEvaluator> logger) => _logger = logger;

    /// <summary>
    /// Evaluates a single glyph into its outline. Returns null if the glyph has no outline (e.g. a
    /// space, an empty <paramref name="glyphData"/>) or is malformed, or (for a composite glyph) any
    /// component it references is malformed.
    /// </summary>
    /// <param name="glyphData">This glyph's raw bytes, as sliced out of "glyf" via "loca".</param>
    /// <param name="glyfProcessor">Resolves a component's raw bytes by glyph ID, on demand.</param>
    /// <param name="loca">This font's parsed "loca" table.</param>
    /// <param name="source">The stream and table range to read a component's raw bytes from.</param>
    public GlyphOutline? Evaluate(
        in ReadOnlyMemory<byte> glyphData,
        SfntGlyfProcessor glyfProcessor,
        SfntLoca loca,
        in SfntGlyfSource source)
    {
        if (glyfProcessor == null)
        {
            throw new ArgumentNullException(nameof(glyfProcessor));
        }

        if (glyphData.Length == 0)
        {
            return null;
        }

        return CollectOutline(glyphData, glyfProcessor, loca, source, depth: 0);
    }

    /// <summary>
    /// Collects a glyph's outline in its own coordinate space. A simple glyph contributes its points
    /// as stored; a composite glyph contributes each component's outline in component order, already
    /// placed by that component's own transform, which is exactly the order and numbering a
    /// component's point-matching arguments index. Returns null when the glyph data is truncated or
    /// its components nest too deeply.
    /// </summary>
    private GlyphOutline? CollectOutline(
        in ReadOnlyMemory<byte> data,
        SfntGlyfProcessor glyfProcessor,
        SfntLoca loca,
        in SfntGlyfSource source,
        int depth)
    {
        if (depth > MaxComponentNestingDepth)
        {
            _logger.LogWarning("Composite glyph exceeded max component nesting depth ({MaxDepth}); aborting this branch.", MaxComponentNestingDepth);
            return null;
        }

        SfntReader reader = new(data.Span);
        short numberOfContours = reader.ReadInt16OrDefault();
        reader.Skip(8); // xMin, yMin, xMax, yMax - recomputed from the collected points when written back.

        if (numberOfContours >= 0)
        {
            return ReadSimpleOutline(ref reader, numberOfContours, data);
        }

        return CollectComponents(ref reader, glyfProcessor, loca, source, depth);
    }

    /// <summary>
    /// Collects a composite glyph's components into a single outline, placing each one as its record
    /// asks: a component carrying x/y values is translated by them, one using point matching is
    /// translated so that its own point lands on the point of the given number in the outline
    /// collected so far.
    /// </summary>
    private GlyphOutline? CollectComponents(
        ref SfntReader reader,
        SfntGlyfProcessor glyfProcessor,
        SfntLoca loca,
        in SfntGlyfSource source,
        int depth)
    {
        GlyphOutline outline = GlyphOutline.Empty;

        bool moreComponents;
        do
        {
            ComponentRecord component = ReadComponent(ref reader);
            moreComponents = component.HasMoreComponents;

            if (!moreComponents && component.HasInstructions)
            {
                ushort instructionLength = reader.ReadUInt16OrDefault();
                reader.Skip(instructionLength);
            }

            if (!reader.IsValid)
            {
                return null;
            }

            if (component.GlyphIndex >= loca.Ranges.Count)
            {
                _logger.LogWarning("Composite glyph references out-of-range component glyph {GlyphIndex}; skipping this component.", component.GlyphIndex);
                continue;
            }

            ReadOnlyMemory<byte> componentData = glyfProcessor.FetchRawGlyph(component.GlyphIndex, loca, source);
            if (componentData.Length == 0)
            {
                continue;
            }

            GlyphOutline? componentOutline = CollectOutline(componentData, glyfProcessor, loca, source, depth + 1);
            if (componentOutline == null)
            {
                return null;
            }

            PdfFontMatrix? componentTransform = ResolveComponentTransform(component, outline, componentOutline);
            if (componentTransform == null)
            {
                continue;
            }

            outline = outline.Merge(componentOutline, componentTransform.Value);
        }
        while (moreComponents);

        return outline;
    }

    /// <summary>
    /// Resolves the transform placing a component within the composite glyph that references it.
    /// A point-matching component's two points are compared after its scale is applied to its own
    /// point, so the translation lands the scaled point exactly on the one it matches. Returns null
    /// when either point number is out of range, leaving the component unplaced.
    /// </summary>
    private PdfFontMatrix? ResolveComponentTransform(in ComponentRecord component, GlyphOutline outline, GlyphOutline componentOutline)
    {
        if (component.ArgsAreXyValues)
        {
            return new PdfFontMatrix(component.ScaleX, component.Scale10, component.OffsetX, component.Scale01, component.ScaleY, component.OffsetY);
        }

        if (component.CompositePointIndex >= outline.Points.Length || component.ComponentPointIndex >= componentOutline.Points.Length)
        {
            _logger.LogWarning(
                "Composite glyph component matches point {CompositePointIndex} of {CompositePointCount} to point {ComponentPointIndex} of {ComponentPointCount}, which is out of range; skipping this component.",
                component.CompositePointIndex,
                outline.Points.Length,
                component.ComponentPointIndex,
                componentOutline.Points.Length);

            return null;
        }

        PdfFontMatrix scale = new(component.ScaleX, component.Scale10, 0f, component.Scale01, component.ScaleY, 0f);

        GlyphPoint compositePoint = outline.Points[component.CompositePointIndex];
        GlyphPoint scaledComponentPoint = GlyphOutline.Place(componentOutline.Points[component.ComponentPointIndex], scale);

        return new PdfFontMatrix(
            component.ScaleX,
            component.Scale10,
            compositePoint.X - scaledComponentPoint.X,
            component.Scale01,
            component.ScaleY,
            compositePoint.Y - scaledComponentPoint.Y);
    }

    /// <summary>
    /// Reads a simple glyph's contour end points, hinting instructions, per-point flags and
    /// delta-decoded coordinates, leaving <paramref name="reader"/> positioned after the glyph's point
    /// data. Only the on-curve bit of each flag is kept: the rest describe how the source encoded its
    /// deltas, which a writer decides for itself from the coordinates it is given.
    /// </summary>
    private static GlyphOutline? ReadSimpleOutline(ref SfntReader reader, short numberOfContours, in ReadOnlyMemory<byte> data)
    {
        var endPoints = new int[numberOfContours];
        for (int contourIndex = 0; contourIndex < numberOfContours; contourIndex++)
        {
            endPoints[contourIndex] = reader.ReadUInt16OrDefault();
        }

        ushort instructionLength = reader.ReadUInt16OrDefault();
        int instructionOffset = reader.Position;
        reader.Skip(instructionLength);

        // The point data below is sized from the last contour end, so a glyph truncated before it is
        // rejected here rather than after allocating for points the data cannot hold.
        if (!reader.IsValid)
        {
            return null;
        }

        ReadOnlyMemory<byte> instructions = (instructionLength > 0)
            ? data.Slice(instructionOffset, instructionLength)
            : default;

        int numPoints = (numberOfContours > 0) ? endPoints[numberOfContours - 1] + 1 : 0;

        var flags = new byte[numPoints];
        for (int pointIndex = 0; pointIndex < numPoints;)
        {
            byte flag = reader.ReadByteOrDefault();
            flags[pointIndex++] = flag;
            if ((flag & SfntGlyphFlags.Repeat) != 0)
            {
                byte repeatCount = reader.ReadByteOrDefault();
                int repeatEnd = Math.Min(pointIndex + repeatCount, numPoints);
                flags.AsSpan(pointIndex, repeatEnd - pointIndex).Fill(flag);
                pointIndex = repeatEnd;
            }
        }

        // Both coordinate runs are delta-encoded and stored one axis after the other, so x is
        // accumulated into the points first and y is folded into them on the second pass.
        var points = new GlyphPoint[numPoints];

        int x = 0;
        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            x += ReadDelta(ref reader, flags[pointIndex], SfntGlyphFlags.XShort, SfntGlyphFlags.XSame);
            points[pointIndex] = GlyphPoint.Clamped(x, 0);
        }

        int y = 0;
        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            y += ReadDelta(ref reader, flags[pointIndex], SfntGlyphFlags.YShort, SfntGlyphFlags.YSame);
            points[pointIndex] = GlyphPoint.Clamped(points[pointIndex].X, y);
            flags[pointIndex] &= SfntGlyphFlags.OnCurve;
        }

        return (reader.IsValid) ? new GlyphOutline(points, flags, endPoints, instructions) : null;
    }

    private static int ReadDelta(ref SfntReader reader, byte flag, byte shortFlag, byte sameFlag)
    {
        if ((flag & shortFlag) != 0)
        {
            byte value = reader.ReadByteOrDefault();
            return ((flag & sameFlag) != 0) ? value : -value;
        }

        if ((flag & sameFlag) == 0)
        {
            return reader.ReadInt16OrDefault();
        }

        return 0;
    }

    /// <summary>
    /// Reads one component record of a composite glyph, leaving <paramref name="reader"/> positioned
    /// after the record's transform fields - before the hinting instructions that trail the last
    /// component, if any.
    /// </summary>
    private static ComponentRecord ReadComponent(ref SfntReader reader)
    {
        ushort flags = reader.ReadUInt16OrDefault();
        ushort glyphIndex = reader.ReadUInt16OrDefault();

        ushort rawArgument1;
        ushort rawArgument2;
        if ((flags & ComponentArg1And2AreWords) != 0)
        {
            rawArgument1 = reader.ReadUInt16OrDefault();
            rawArgument2 = reader.ReadUInt16OrDefault();
        }
        else
        {
            rawArgument1 = reader.ReadByteOrDefault();
            rawArgument2 = reader.ReadByteOrDefault();
        }

        float scaleX = 1f;
        float scale01 = 0f;
        float scale10 = 0f;
        float scaleY = 1f;
        if ((flags & ComponentWeHaveAScale) != 0)
        {
            scaleX = ReadF2Dot14(ref reader);
            scaleY = scaleX;
        }
        else if ((flags & ComponentWeHaveAnXAndYScale) != 0)
        {
            scaleX = ReadF2Dot14(ref reader);
            scaleY = ReadF2Dot14(ref reader);
        }
        else if ((flags & ComponentWeHaveATwoByTwo) != 0)
        {
            scaleX = ReadF2Dot14(ref reader);
            scale01 = ReadF2Dot14(ref reader);
            scale10 = ReadF2Dot14(ref reader);
            scaleY = ReadF2Dot14(ref reader);
        }

        return new ComponentRecord(flags, glyphIndex, rawArgument1, rawArgument2, scaleX, scale01, scale10, scaleY);
    }

    private static float ReadF2Dot14(ref SfntReader reader) => reader.ReadInt16OrDefault() / 16384f;

    /// <summary>
    /// One component record of a composite glyph: the glyph it references, its 2x2 scale, and its two
    /// arguments - which are either an x/y offset or the pair of point numbers to align, depending on
    /// <see cref="ComponentArgsAreXyValues"/>.
    /// </summary>
    private readonly struct ComponentRecord
    {
        private readonly ushort _flags;
        private readonly ushort _rawArgument1;
        private readonly ushort _rawArgument2;

        public ComponentRecord(ushort flags, ushort glyphIndex, ushort rawArgument1, ushort rawArgument2, float scaleX, float scale01, float scale10, float scaleY)
        {
            _flags = flags;
            _rawArgument1 = rawArgument1;
            _rawArgument2 = rawArgument2;
            GlyphIndex = glyphIndex;
            ScaleX = scaleX;
            Scale01 = scale01;
            Scale10 = scale10;
            ScaleY = scaleY;
        }

        public ushort GlyphIndex { get; }

        public float ScaleX { get; }

        public float Scale01 { get; }

        public float Scale10 { get; }

        public float ScaleY { get; }

        public bool ArgsAreXyValues => (_flags & ComponentArgsAreXyValues) != 0;

        public bool HasMoreComponents => (_flags & ComponentMoreComponents) != 0;

        public bool HasInstructions => (_flags & ComponentWeHaveInstructions) != 0;

        /// <summary>
        /// The horizontal offset placing this component, as a signed value in the parent's units.
        /// Only meaningful while <see cref="ArgsAreXyValues"/> is set.
        /// </summary>
        public short OffsetX => ((_flags & ComponentArg1And2AreWords) != 0) ? (short)_rawArgument1 : (sbyte)(byte)_rawArgument1;

        /// <summary>
        /// The vertical offset placing this component, as a signed value in the parent's units.
        /// Only meaningful while <see cref="ArgsAreXyValues"/> is set.
        /// </summary>
        public short OffsetY => ((_flags & ComponentArg1And2AreWords) != 0) ? (short)_rawArgument2 : (sbyte)(byte)_rawArgument2;

        /// <summary>
        /// The point number, within the composite glyph collected so far, this component aligns to.
        /// Only meaningful while <see cref="ArgsAreXyValues"/> is clear.
        /// </summary>
        public ushort CompositePointIndex => _rawArgument1;

        /// <summary>
        /// The point number, within this component, aligned to <see cref="CompositePointIndex"/>.
        /// Only meaningful while <see cref="ArgsAreXyValues"/> is clear.
        /// </summary>
        public ushort ComponentPointIndex => _rawArgument2;
    }
}
