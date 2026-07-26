using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Evaluates a single "glyf" glyph into a <see cref="SfntGlyphCharacter"/>: builds its outline via
/// <see cref="PdfFontPathBuilder"/> (recursively flattening composite glyphs' components, converting
/// TrueType's native quadratic curves to the cubic curves that format stores), and emits a repacked
/// glyph with hinting instructions stripped. Mirrors
/// <c>PdfPixel.Fonts.CffV2.CffCharStringEvaluator</c>'s role for CFF charstrings.
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

        PdfFontPathBuilder pathBuilder = new(matrix);
        if (!EmitPath(glyphData.Span, pathBuilder, GlyphTransform.Identity, glyfProcessor, loca, source, depth: 0))
        {
            return null;
        }

        byte[]? repacked = RepackGlyph(glyphData.Span);
        if (repacked == null)
        {
            return null;
        }

        return new SfntGlyphCharacter(pathBuilder.ToPath(), repacked);
    }

    private bool EmitPath(
        in ReadOnlySpan<byte> data,
        PdfFontPathBuilder pathBuilder,
        in GlyphTransform transform,
        SfntGlyfProcessor glyfProcessor,
        SfntLoca loca,
        in SfntGlyfSource source,
        int depth)
    {
        if (depth > MaxComponentNestingDepth)
        {
            _logger.LogWarning("Composite glyph exceeded max component nesting depth ({MaxDepth}); aborting this branch.", MaxComponentNestingDepth);
            return false;
        }

        SfntReader reader = new(data);
        short numberOfContours = reader.ReadInt16OrDefault();
        reader.Skip(8); // xMin, yMin, xMax, yMax - not needed to build the path.

        if (numberOfContours >= 0)
        {
            EmitSimpleContours(ref reader, numberOfContours, pathBuilder, transform);
            return reader.IsValid;
        }

        return EmitComponents(ref reader, pathBuilder, transform, glyfProcessor, loca, source, depth);
    }

    private static void EmitSimpleContours(ref SfntReader reader, short numberOfContours, PdfFontPathBuilder pathBuilder, in GlyphTransform transform)
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

        int startPoint = 0;
        for (int contourIndex = 0; contourIndex < numberOfContours; contourIndex++)
        {
            int endPoint = endPoints[contourIndex];
            EmitContour(pathBuilder, transform, flags, xs, ys, startPoint, endPoint);
            startPoint = endPoint + 1;
        }
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
    /// Emits one contour's on/off-curve points as path segments, synthesizing the implied on-curve
    /// midpoint between two consecutive off-curve points (TrueType allows omitting it) and converting
    /// each quadratic segment to the cubic curve <see cref="PdfFontPathBuilder"/> stores.
    /// </summary>
    private static void EmitContour(PdfFontPathBuilder pathBuilder, in GlyphTransform transform, byte[] flags, int[] xs, int[] ys, int startPoint, int endPoint)
    {
        int pointCount = endPoint - startPoint + 1;
        if (pointCount <= 0)
        {
            return;
        }

        int firstOnCurve = 0;
        while (firstOnCurve < pointCount && !IsContourPointOnCurve(flags, startPoint, pointCount, firstOnCurve))
        {
            firstOnCurve++;
        }

        (float X, float Y) startPointCoordinates;
        if (firstOnCurve == pointCount)
        {
            // All-off-curve contour: start at the implied midpoint of the first two points.
            (float x0, float y0) = GetContourPoint(xs, ys, startPoint, pointCount, 0, transform);
            (float x1, float y1) = GetContourPoint(xs, ys, startPoint, pointCount, 1, transform);
            startPointCoordinates = ((x0 + x1) / 2f, (y0 + y1) / 2f);
            firstOnCurve = 0;
        }
        else
        {
            startPointCoordinates = GetContourPoint(xs, ys, startPoint, pointCount, firstOnCurve, transform);
        }

        pathBuilder.MoveTo(startPointCoordinates.X, startPointCoordinates.Y);

        (float X, float Y) currentPoint = startPointCoordinates;
        (float X, float Y)? pendingControlPoint = null;

        for (int step = 1; step <= pointCount; step++)
        {
            int index = firstOnCurve + step;
            bool onCurve = IsContourPointOnCurve(flags, startPoint, pointCount, index);
            (float X, float Y) point = GetContourPoint(xs, ys, startPoint, pointCount, index, transform);

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

    private static bool IsContourPointOnCurve(byte[] flags, int startPoint, int pointCount, int index)
        => (flags[startPoint + (index % pointCount)] & FlagOnCurve) != 0;

    private static (float X, float Y) GetContourPoint(int[] xs, int[] ys, int startPoint, int pointCount, int index, in GlyphTransform transform)
    {
        int wrapped = startPoint + (index % pointCount);
        return transform.Apply(xs[wrapped], ys[wrapped]);
    }

    private bool EmitComponents(
        ref SfntReader reader,
        PdfFontPathBuilder pathBuilder,
        in GlyphTransform transform,
        SfntGlyfProcessor glyfProcessor,
        SfntLoca loca,
        in SfntGlyfSource source,
        int depth)
    {
        bool moreComponents;
        do
        {
            ushort flags = reader.ReadUInt16OrDefault();
            ushort glyphIndex = reader.ReadUInt16OrDefault();

            short arg1;
            short arg2;
            if ((flags & ComponentArg1And2AreWords) != 0)
            {
                arg1 = reader.ReadInt16OrDefault();
                arg2 = reader.ReadInt16OrDefault();
            }
            else
            {
                arg1 = (sbyte)reader.ReadByteOrDefault();
                arg2 = (sbyte)reader.ReadByteOrDefault();
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

            moreComponents = (flags & ComponentMoreComponents) != 0;

            if (!moreComponents && (flags & ComponentWeHaveInstructions) != 0)
            {
                ushort instructionLength = reader.ReadUInt16OrDefault();
                reader.Skip(instructionLength);
            }

            if (!reader.IsValid)
            {
                return false;
            }

            if ((flags & ComponentArgsAreXyValues) == 0)
            {
                _logger.LogWarning("Composite glyph component uses point-matching alignment, which is not supported; skipping this component.");
                continue;
            }

            int numGlyphs = Math.Max(0, loca.Offsets.Count - 1);
            if (glyphIndex >= numGlyphs)
            {
                _logger.LogWarning("Composite glyph references out-of-range component glyph {GlyphIndex}; skipping this component.", glyphIndex);
                continue;
            }

            GlyphTransform componentTransform = new(scaleX, scale01, scale10, scaleY, arg1, arg2);
            GlyphTransform combinedTransform = transform.Combine(componentTransform);

            ReadOnlyMemory<byte> componentData = glyfProcessor.FetchRawGlyph(glyphIndex, loca, source);
            if (componentData.Length > 0 && !EmitPath(componentData.Span, pathBuilder, combinedTransform, glyfProcessor, loca, source, depth + 1))
            {
                return false;
            }
        }
        while (moreComponents);

        return true;
    }

    private static float ReadF2Dot14(ref SfntReader reader) => reader.ReadInt16OrDefault() / 16384f;

    /// <summary>
    /// Copies a glyph's structure (contours or component list) unchanged, dropping only its hinting
    /// instructions. Unlike <see cref="EmitPath"/>, this never recurses into referenced components -
    /// a composite glyph's component records (including their glyph ID references) are copied as-is.
    /// </summary>
    private byte[]? RepackGlyph(in ReadOnlySpan<byte> data)
    {
        SfntReader reader = new(data);
        short numberOfContours = reader.ReadInt16OrDefault();
        short xMin = reader.ReadInt16OrDefault();
        short yMin = reader.ReadInt16OrDefault();
        short xMax = reader.ReadInt16OrDefault();
        short yMax = reader.ReadInt16OrDefault();

        SfntWriter writer = new();
        writer.WriteInt16(numberOfContours);
        writer.WriteInt16(xMin);
        writer.WriteInt16(yMin);
        writer.WriteInt16(xMax);
        writer.WriteInt16(yMax);

        if (numberOfContours >= 0)
        {
            RepackSimpleGlyph(ref reader, numberOfContours, writer);
        }
        else
        {
            RepackComponents(ref reader, writer);
        }

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

        ushort instructionLength = reader.ReadUInt16OrDefault();
        reader.Skip(instructionLength);
        writer.WriteUInt16(0); // instructionLength: hinting instructions are always stripped.

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

    private static void RepackComponents(ref SfntReader reader, SfntWriter writer)
    {
        bool moreComponents;
        do
        {
            ushort flags = reader.ReadUInt16OrDefault();
            ushort glyphIndex = reader.ReadUInt16OrDefault();

            bool argsAreWords = (flags & ComponentArg1And2AreWords) != 0;
            short arg1;
            short arg2;
            if (argsAreWords)
            {
                arg1 = reader.ReadInt16OrDefault();
                arg2 = reader.ReadInt16OrDefault();
            }
            else
            {
                arg1 = (sbyte)reader.ReadByteOrDefault();
                arg2 = (sbyte)reader.ReadByteOrDefault();
            }

            int scaleFieldCount = 0;
            if ((flags & ComponentWeHaveAScale) != 0)
            {
                scaleFieldCount = 1;
            }
            else if ((flags & ComponentWeHaveAnXAndYScale) != 0)
            {
                scaleFieldCount = 2;
            }
            else if ((flags & ComponentWeHaveATwoByTwo) != 0)
            {
                scaleFieldCount = 4;
            }

            var scales = new short[scaleFieldCount];
            for (int scaleIndex = 0; scaleIndex < scaleFieldCount; scaleIndex++)
            {
                scales[scaleIndex] = reader.ReadInt16OrDefault();
            }

            moreComponents = (flags & ComponentMoreComponents) != 0;

            // Hinting instructions (if WE_HAVE_INSTRUCTIONS is set on the last component) are read
            // and discarded below, after the component loop, since they always trail the last one.
            var strippedFlags = (ushort)(flags & ~ComponentWeHaveInstructions);
            writer.WriteUInt16(strippedFlags);
            writer.WriteUInt16(glyphIndex);

            if (argsAreWords)
            {
                writer.WriteInt16(arg1);
                writer.WriteInt16(arg2);
            }
            else
            {
                writer.WriteByte((byte)(sbyte)arg1);
                writer.WriteByte((byte)(sbyte)arg2);
            }

            for (int scaleIndex = 0; scaleIndex < scaleFieldCount; scaleIndex++)
            {
                writer.WriteInt16(scales[scaleIndex]);
            }

            if (!moreComponents && (flags & ComponentWeHaveInstructions) != 0)
            {
                ushort instructionLength = reader.ReadUInt16OrDefault();
                reader.Skip(instructionLength);
            }
        }
        while (moreComponents);
    }

    private readonly struct GlyphTransform
    {
        public static readonly GlyphTransform Identity = new(1f, 0f, 0f, 1f, 0f, 0f);

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

        /// <summary>
        /// Composes this transform (applied second, mapping a component's parent space onward) with
        /// <paramref name="inner"/> (applied first, mapping the component's own local space into that
        /// parent space), so applying the result to a point in the component's local space lands
        /// directly in this transform's target space.
        /// </summary>
        public GlyphTransform Combine(in GlyphTransform inner)
        {
            float a = (inner.ScaleX * ScaleX) + (inner.Scale01 * Scale10);
            float b = (inner.ScaleX * Scale01) + (inner.Scale01 * ScaleY);
            float c = (inner.Scale10 * ScaleX) + (inner.ScaleY * Scale10);
            float d = (inner.Scale10 * Scale01) + (inner.ScaleY * ScaleY);
            float e = (inner.Dx * ScaleX) + (inner.Dy * Scale10) + Dx;
            float f = (inner.Dx * Scale01) + (inner.Dy * ScaleY) + Dy;
            return new GlyphTransform(a, b, c, d, e, f);
        }
    }
}
