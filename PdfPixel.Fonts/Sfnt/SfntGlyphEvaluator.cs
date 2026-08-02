using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Evaluates a single "glyf" glyph into a <see cref="SfntGlyphCharacter"/>: collects the glyph's
/// points, resolving a composite glyph's components - whether they carry an x/y offset or match a
/// pair of points - into one flat outline, then builds that outline via
/// <see cref="PdfFontPathBuilder"/> (converting TrueType's native quadratic curves to the cubic
/// curves that format stores) and repacks it as a simple glyph.
/// Mirrors <c>PdfPixel.Fonts.Cff.CffCharStringEvaluator</c>'s role for CFF charstrings.
/// </summary>
public class SfntGlyphEvaluator
{
    private const int MaxComponentNestingDepth = 8;

    private const byte FlagOnCurve = 0x01;
    private const byte FlagXShort = 0x02;
    private const byte FlagYShort = 0x04;
    private const byte FlagRepeat = 0x08;
    private const byte FlagXSame = 0x10;
    private const byte FlagYSame = 0x20;

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
    /// Evaluates a single glyph. Returns null if the glyph has no outline (e.g. space, an empty
    /// <paramref name="glyphData"/>) or is malformed, or (for a composite glyph) any component it
    /// references is malformed.
    /// </summary>
    /// <param name="glyphData">This glyph's raw bytes, as sliced out of "glyf" via "loca".</param>
    /// <param name="glyfProcessor">Resolves a component's raw bytes by glyph ID, on demand.</param>
    /// <param name="loca">This font's parsed "loca" table.</param>
    /// <param name="source">The stream and table range to read a component's raw bytes from.</param>
    /// <param name="matrix">Transform applied to every point of the resulting path.</param>
    public SfntGlyphCharacter? Evaluate(
        in ReadOnlyMemory<byte> glyphData,
        SfntGlyfProcessor glyfProcessor,
        SfntLoca loca,
        in SfntGlyfSource source,
        in PdfFontMatrix matrix)
    {
        if (glyfProcessor == null)
        {
            throw new ArgumentNullException(nameof(glyfProcessor));
        }

        if (glyphData.Length == 0)
        {
            return null;
        }

        GlyphOutline? outline = CollectOutline(glyphData.Span, glyfProcessor, loca, source, depth: 0);
        if (outline == null)
        {
            return null;
        }

        byte[]? repacked = RepackGlyph(glyphData.Span, outline);
        if (repacked == null)
        {
            return null;
        }

        PdfFontPathBuilder pathBuilder = new(matrix);
        EmitOutline(outline, pathBuilder);

        return new SfntGlyphCharacter(pathBuilder.ToPath(), repacked);
    }

    /// <summary>
    /// Collects a glyph's outline in its own coordinate space. A simple glyph contributes its points
    /// as stored; a composite glyph contributes each component's outline in component order, already
    /// placed by that component's own transform, which is exactly the order and numbering a
    /// component's point-matching arguments index. Returns null when the glyph data is truncated or
    /// its components nest too deeply.
    /// </summary>
    private GlyphOutline? CollectOutline(
        in ReadOnlySpan<byte> data,
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

        SfntReader reader = new(data);
        short numberOfContours = reader.ReadInt16OrDefault();
        reader.Skip(8); // xMin, yMin, xMax, yMax - recomputed from the collected points when needed.

        if (numberOfContours >= 0)
        {
            GlyphOutline simpleOutline = ReadSimpleOutline(ref reader, numberOfContours);

            return (reader.IsValid) ? simpleOutline : null;
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
        GlyphOutline outline = new();

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

            GlyphOutline? componentOutline = CollectOutline(componentData.Span, glyfProcessor, loca, source, depth + 1);
            if (componentOutline == null)
            {
                return null;
            }

            GlyphTransform? componentTransform = ResolveComponentTransform(component, outline, componentOutline);
            if (componentTransform == null)
            {
                continue;
            }

            outline.Append(componentOutline, componentTransform.Value);
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
    private GlyphTransform? ResolveComponentTransform(in ComponentRecord component, GlyphOutline outline, GlyphOutline componentOutline)
    {
        if (component.ArgsAreXyValues)
        {
            return new GlyphTransform(component.ScaleX, component.Scale01, component.Scale10, component.ScaleY, component.OffsetX, component.OffsetY);
        }

        if (component.CompositePointIndex >= outline.Points.Count || component.ComponentPointIndex >= componentOutline.Points.Count)
        {
            _logger.LogWarning(
                "Composite glyph component matches point {CompositePointIndex} of {CompositePointCount} to point {ComponentPointIndex} of {ComponentPointCount}, which is out of range; skipping this component.",
                component.CompositePointIndex,
                outline.Points.Count,
                component.ComponentPointIndex,
                componentOutline.Points.Count);

            return null;
        }

        GlyphTransform scale = new(component.ScaleX, component.Scale01, component.Scale10, component.ScaleY, 0f, 0f);

        (float X, float Y) compositePoint = outline.Points[component.CompositePointIndex];
        (float X, float Y) componentPoint = componentOutline.Points[component.ComponentPointIndex];
        (float X, float Y) scaledComponentPoint = scale.Apply(componentPoint.X, componentPoint.Y);

        return new GlyphTransform(
            component.ScaleX,
            component.Scale01,
            component.Scale10,
            component.ScaleY,
            compositePoint.X - scaledComponentPoint.X,
            compositePoint.Y - scaledComponentPoint.Y);
    }

    /// <summary>
    /// Reads a simple glyph's contour end points, per-point flags and delta-decoded coordinates,
    /// leaving <paramref name="reader"/> positioned after the glyph's point data.
    /// </summary>
    private static GlyphOutline ReadSimpleOutline(ref SfntReader reader, short numberOfContours)
    {
        var endPoints = new ushort[numberOfContours];
        for (int contourIndex = 0; contourIndex < numberOfContours; contourIndex++)
        {
            endPoints[contourIndex] = reader.ReadUInt16OrDefault();
        }

        ushort instructionLength = reader.ReadUInt16OrDefault();
        reader.Skip(instructionLength);

        int numPoints = (numberOfContours > 0) ? endPoints[numberOfContours - 1] + 1 : 0;

        var flags = new byte[numPoints];
        for (int pointIndex = 0; pointIndex < numPoints;)
        {
            byte flag = reader.ReadByteOrDefault();
            flags[pointIndex++] = flag;
            if ((flag & FlagRepeat) != 0)
            {
                byte repeatCount = reader.ReadByteOrDefault();
                for (int repeatIndex = 0; repeatIndex < repeatCount && pointIndex < numPoints; repeatIndex++)
                {
                    flags[pointIndex++] = flag;
                }
            }
        }

        var xs = new int[numPoints];
        int x = 0;
        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            x += ReadDelta(ref reader, flags[pointIndex], FlagXShort, FlagXSame);
            xs[pointIndex] = x;
        }

        var ys = new int[numPoints];
        int y = 0;
        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            y += ReadDelta(ref reader, flags[pointIndex], FlagYShort, FlagYSame);
            ys[pointIndex] = y;
        }

        GlyphOutline outline = new();
        outline.Flags.AddRange(flags);

        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            outline.Points.Add((xs[pointIndex], ys[pointIndex]));
        }

        foreach (ushort endPoint in endPoints)
        {
            outline.EndPoints.Add(endPoint);
        }

        return outline;
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

    private static void EmitOutline(GlyphOutline outline, PdfFontPathBuilder pathBuilder)
    {
        int startPoint = 0;
        foreach (int endPoint in outline.EndPoints)
        {
            EmitContour(pathBuilder, outline, startPoint, endPoint);
            startPoint = endPoint + 1;
        }
    }

    /// <summary>
    /// Emits one contour's on/off-curve points as path segments, synthesizing the implied on-curve
    /// midpoint between two consecutive off-curve points (TrueType allows omitting it) and converting
    /// each quadratic segment to the cubic curve <see cref="PdfFontPathBuilder"/> stores.
    /// </summary>
    private static void EmitContour(PdfFontPathBuilder pathBuilder, GlyphOutline outline, int startPoint, int endPoint)
    {
        int pointCount = endPoint - startPoint + 1;
        if (pointCount <= 0)
        {
            return;
        }

        int firstOnCurve = 0;
        while (firstOnCurve < pointCount && !IsContourPointOnCurve(outline, startPoint, pointCount, firstOnCurve))
        {
            firstOnCurve++;
        }

        (float X, float Y) startPointCoordinates;
        if (firstOnCurve == pointCount)
        {
            // All-off-curve contour: start at the implied midpoint of the first two points.
            (float x0, float y0) = GetContourPoint(outline, startPoint, pointCount, 0);
            (float x1, float y1) = GetContourPoint(outline, startPoint, pointCount, 1);
            startPointCoordinates = ((x0 + x1) / 2f, (y0 + y1) / 2f);
            firstOnCurve = 0;
        }
        else
        {
            startPointCoordinates = GetContourPoint(outline, startPoint, pointCount, firstOnCurve);
        }

        pathBuilder.MoveTo(startPointCoordinates.X, startPointCoordinates.Y);

        (float X, float Y) currentPoint = startPointCoordinates;
        (float X, float Y)? pendingControlPoint = null;

        for (int step = 1; step <= pointCount; step++)
        {
            int index = firstOnCurve + step;
            bool onCurve = IsContourPointOnCurve(outline, startPoint, pointCount, index);
            (float X, float Y) point = GetContourPoint(outline, startPoint, pointCount, index);

            if (onCurve)
            {
                if (pendingControlPoint.HasValue)
                {
                    (float X, float Y) control = pendingControlPoint.Value;
                    pathBuilder.QuadraticTo(currentPoint.X, currentPoint.Y, control.X, control.Y, point.X, point.Y);
                    pendingControlPoint = null;
                }
                else
                {
                    pathBuilder.LineTo(point.X, point.Y);
                }

                currentPoint = point;
            }
            else
            {
                if (pendingControlPoint.HasValue)
                {
                    (float X, float Y) previousControl = pendingControlPoint.Value;
                    (float X, float Y) implied = ((previousControl.X + point.X) / 2f, (previousControl.Y + point.Y) / 2f);
                    pathBuilder.QuadraticTo(currentPoint.X, currentPoint.Y, previousControl.X, previousControl.Y, implied.X, implied.Y);
                    currentPoint = implied;
                }

                pendingControlPoint = point;
            }
        }

        pathBuilder.Close();
    }

    private static bool IsContourPointOnCurve(GlyphOutline outline, int startPoint, int pointCount, int index)
        => (outline.Flags[startPoint + (index % pointCount)] & FlagOnCurve) != 0;

    private static (float X, float Y) GetContourPoint(GlyphOutline outline, int startPoint, int pointCount, int index)
        => outline.Points[startPoint + (index % pointCount)];

    /// <summary>
    /// Repacks a glyph. A simple glyph's structure is copied unchanged, hinting instructions
    /// included, since its points keep the numbers those instructions address; a composite glyph is
    /// written as a simple one, its components' contours stored inline, so the repacked glyph no
    /// longer refers to any other glyph - and loses its instructions, which addressed the point
    /// numbering that flattening replaces.
    /// </summary>
    private byte[]? RepackGlyph(in ReadOnlySpan<byte> data, GlyphOutline outline)
    {
        SfntReader reader = new(data);
        short numberOfContours = reader.ReadInt16OrDefault();
        short xMin = reader.ReadInt16OrDefault();
        short yMin = reader.ReadInt16OrDefault();
        short xMax = reader.ReadInt16OrDefault();
        short yMax = reader.ReadInt16OrDefault();

        if (numberOfContours < 0)
        {
            return WriteFlattenedGlyph(outline);
        }

        SfntWriter writer = new();
        writer.WriteInt16(numberOfContours);
        writer.WriteInt16(xMin);
        writer.WriteInt16(yMin);
        writer.WriteInt16(xMax);
        writer.WriteInt16(yMax);

        RepackSimpleGlyph(ref reader, numberOfContours, writer);

        if (!reader.IsValid)
        {
            _logger.LogWarning("Failed to repack glyph: data is truncated.");
            return null;
        }

        return writer.ToArray();
    }

    private static void RepackSimpleGlyph(ref SfntReader reader, short numberOfContours, SfntWriter writer)
    {
        var endPoints = new ushort[numberOfContours];
        for (int contourIndex = 0; contourIndex < numberOfContours; contourIndex++)
        {
            endPoints[contourIndex] = reader.ReadUInt16OrDefault();
            writer.WriteUInt16(endPoints[contourIndex]);
        }

        // Points are copied over verbatim below, so the instructions still address the same point
        // numbers they were compiled against and are carried through with them.
        ushort instructionLength = reader.ReadUInt16OrDefault();
        writer.WriteUInt16(instructionLength);
        writer.WriteBytes(reader.ReadBytes(instructionLength));

        int numPoints = (numberOfContours > 0) ? endPoints[numberOfContours - 1] + 1 : 0;

        var flags = new byte[numPoints];
        for (int pointIndex = 0; pointIndex < numPoints;)
        {
            byte flag = reader.ReadByteOrDefault();
            flags[pointIndex++] = flag;
            writer.WriteByte(flag);
            if ((flag & FlagRepeat) != 0)
            {
                byte repeatCount = reader.ReadByteOrDefault();
                writer.WriteByte(repeatCount);
                for (int repeatIndex = 0; repeatIndex < repeatCount && pointIndex < numPoints; repeatIndex++)
                {
                    flags[pointIndex++] = flag;
                }
            }
        }

        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            CopyDelta(ref reader, writer, flags[pointIndex], FlagXShort, FlagXSame);
        }

        for (int pointIndex = 0; pointIndex < numPoints; pointIndex++)
        {
            CopyDelta(ref reader, writer, flags[pointIndex], FlagYShort, FlagYSame);
        }
    }

    private static void CopyDelta(ref SfntReader reader, SfntWriter writer, byte flag, byte shortFlag, byte sameFlag)
    {
        if ((flag & shortFlag) != 0)
        {
            writer.WriteByte(reader.ReadByteOrDefault());
        }
        else if ((flag & sameFlag) == 0)
        {
            writer.WriteInt16(reader.ReadInt16OrDefault());
        }
    }

    /// <summary>
    /// Writes a collected outline as a simple glyph, with a bounding box recomputed from its points -
    /// the composite glyph's own box describes geometry this glyph no longer stores by reference.
    /// Coordinates are rounded to the integer font units "glyf" holds and every delta is written in
    /// the two-byte form, rather than the format's shorter ones. Returns an empty glyph when the
    /// outline has no points, which "loca" records as a glyph without an outline.
    /// </summary>
    private static byte[] WriteFlattenedGlyph(GlyphOutline outline)
    {
        int pointCount = outline.Points.Count;
        if (pointCount == 0 || outline.EndPoints.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var xCoordinates = new short[pointCount];
        var yCoordinates = new short[pointCount];
        short xMin = short.MaxValue;
        short yMin = short.MaxValue;
        short xMax = short.MinValue;
        short yMax = short.MinValue;

        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            (float x, float y) = outline.Points[pointIndex];
            short roundedX = ToFontUnits(x);
            short roundedY = ToFontUnits(y);
            xCoordinates[pointIndex] = roundedX;
            yCoordinates[pointIndex] = roundedY;

            xMin = Math.Min(xMin, roundedX);
            yMin = Math.Min(yMin, roundedY);
            xMax = Math.Max(xMax, roundedX);
            yMax = Math.Max(yMax, roundedY);
        }

        SfntWriter writer = new();
        writer.WriteInt16((short)outline.EndPoints.Count);
        writer.WriteInt16(xMin);
        writer.WriteInt16(yMin);
        writer.WriteInt16(xMax);
        writer.WriteInt16(yMax);

        foreach (int endPoint in outline.EndPoints)
        {
            writer.WriteUInt16((ushort)endPoint);
        }

        writer.WriteUInt16(0); // instructionLength: flattening renumbers the points the composite's instructions address.

        foreach (byte flag in outline.Flags)
        {
            writer.WriteByte((byte)(flag & FlagOnCurve));
        }

        WriteDeltas(writer, xCoordinates);
        WriteDeltas(writer, yCoordinates);

        return writer.ToArray();
    }

    private static void WriteDeltas(SfntWriter writer, short[] coordinates)
    {
        short previous = 0;
        foreach (short coordinate in coordinates)
        {
            writer.WriteInt16(ClampToFontUnits(coordinate - previous));
            previous = coordinate;
        }
    }

    private static short ToFontUnits(float value) => ClampToFontUnits((int)Math.Round(value, MidpointRounding.AwayFromZero));

    private static short ClampToFontUnits(int value)
    {
        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        return (short)value;
    }

    /// <summary>
    /// A glyph's outline as "glyf" describes it, flattened: every point in the order that a
    /// component's point-matching arguments number them, each point's flags, and the index of the
    /// last point of every contour.
    /// </summary>
    private sealed class GlyphOutline
    {
        public List<(float X, float Y)> Points { get; } = [];

        public List<byte> Flags { get; } = [];

        public List<int> EndPoints { get; } = [];

        /// <summary>
        /// Appends another outline's contours to this one, placing each of its points by
        /// <paramref name="transform"/> and shifting the appended contour ends past the points
        /// already collected.
        /// </summary>
        public void Append(GlyphOutline outline, in GlyphTransform transform)
        {
            int pointOffset = Points.Count;

            foreach ((float x, float y) in outline.Points)
            {
                Points.Add(transform.Apply(x, y));
            }

            Flags.AddRange(outline.Flags);

            foreach (int endPoint in outline.EndPoints)
            {
                EndPoints.Add(endPoint + pointOffset);
            }
        }
    }

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

    private readonly struct GlyphTransform
    {
        public GlyphTransform(float scaleX, float scale01, float scale10, float scaleY, float dx, float dy)
        {
            ScaleX = scaleX;
            Scale01 = scale01;
            Scale10 = scale10;
            ScaleY = scaleY;
            Dx = dx;
            Dy = dy;
        }

        public float ScaleX { get; }

        public float Scale01 { get; }

        public float Scale10 { get; }

        public float ScaleY { get; }

        public float Dx { get; }

        public float Dy { get; }

        public (float X, float Y) Apply(float x, float y) => ((x * ScaleX) + (y * Scale10) + Dx, (x * Scale01) + (y * ScaleY) + Dy);
    }
}
