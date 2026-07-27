using PdfPixel.Geometry;
using System;
using System.Globalization;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Parses SVG path data (the <c>d</c> attribute of an <c>&lt;Icon&gt;</c> path element) into a
/// <see cref="PdfPath"/>.
/// </summary>
/// <remarks>
/// Supports the commands used by the annotation icon resource: <c>M</c>, <c>L</c>, <c>H</c>, <c>V</c>,
/// <c>C</c>, <c>Q</c>, <c>T</c>, and <c>Z</c>, in both absolute and relative form. Quadratic curves
/// (<c>Q</c>/<c>T</c>) are elevated to the cubic curves <see cref="PdfPath"/> supports.
/// </remarks>
internal static class PdfAnnotationSvgPathParser
{
    private const string CommandLetters = "MmLlHhVvCcQqTtZz";
    private const float TwoThirds = 2f / 3f;

    /// <summary>
    /// Parses the given SVG path data string into a <see cref="PdfPath"/> with the given fill type.
    /// </summary>
    public static PdfPath Parse(string data, PdfPathFillType fillType)
    {
        PdfPathBuilder path = new();
        int index = 0;
        var command = '\0';
        PdfPoint current = PdfPoint.Empty;
        PdfPoint subPathStart = PdfPoint.Empty;
        PdfPoint? previousQuadraticControl = null;

        while (true)
        {
            SkipSeparators(data, ref index);

            if (index >= data.Length)
            {
                break;
            }

            char character = data[index];

            if (IsCommandLetter(character))
            {
                command = character;
                index++;
            }
            else if (command == '\0')
            {
                throw new InvalidOperationException($"SVG path data does not start with a command: '{data}'");
            }
            else
            {
                command = ImplicitRepeatCommand(command);
            }

            switch (command)
            {
                case 'M':
                case 'm':
                {
                    current = ReadPoint(data, ref index, command == 'm', current);
                    subPathStart = current;
                    path.MoveTo(current);
                    previousQuadraticControl = null;
                    break;
                }
                case 'L':
                case 'l':
                {
                    current = ReadPoint(data, ref index, command == 'l', current);
                    path.LineTo(current);
                    previousQuadraticControl = null;
                    break;
                }
                case 'H':
                case 'h':
                {
                    float x = ReadNumber(data, ref index);
                    current = new PdfPoint((command == 'h') ? current.X + x : x, current.Y);
                    path.LineTo(current);
                    previousQuadraticControl = null;
                    break;
                }
                case 'V':
                case 'v':
                {
                    float y = ReadNumber(data, ref index);
                    current = new PdfPoint(current.X, (command == 'v') ? current.Y + y : y);
                    path.LineTo(current);
                    previousQuadraticControl = null;
                    break;
                }
                case 'C':
                case 'c':
                {
                    bool relative = command == 'c';
                    PdfPoint control1 = ReadPoint(data, ref index, relative, current);
                    PdfPoint control2 = ReadPoint(data, ref index, relative, current);
                    PdfPoint end = ReadPoint(data, ref index, relative, current);
                    path.CubicTo(control1, control2, end);
                    current = end;
                    previousQuadraticControl = null;
                    break;
                }
                case 'Q':
                case 'q':
                {
                    bool relative = command == 'q';
                    PdfPoint control = ReadPoint(data, ref index, relative, current);
                    PdfPoint end = ReadPoint(data, ref index, relative, current);
                    AddQuadratic(path, current, control, end);
                    current = end;
                    previousQuadraticControl = control;
                    break;
                }
                case 'T':
                case 't':
                {
                    PdfPoint end = ReadPoint(data, ref index, command == 't', current);
                    PdfPoint control = (previousQuadraticControl != null)
                        ? Reflect(previousQuadraticControl.Value, current)
                        : current;
                    AddQuadratic(path, current, control, end);
                    current = end;
                    previousQuadraticControl = control;
                    break;
                }
                case 'Z':
                case 'z':
                {
                    path.Close();
                    current = subPathStart;
                    previousQuadraticControl = null;
                    break;
                }
                default:
                {
                    throw new NotSupportedException($"Unsupported SVG path command '{command}'.");
                }
            }
        }

        return path.ToPath(fillType);
    }

    private static void AddQuadratic(PdfPathBuilder path, in PdfPoint start, in PdfPoint control, in PdfPoint end)
    {
        PdfPoint control1 = new(start.X + (TwoThirds * (control.X - start.X)), start.Y + (TwoThirds * (control.Y - start.Y)));
        PdfPoint control2 = new(end.X + (TwoThirds * (control.X - end.X)), end.Y + (TwoThirds * (control.Y - end.Y)));

        path.CubicTo(control1, control2, end);
    }

    private static PdfPoint Reflect(in PdfPoint point, in PdfPoint about)
        => new((2f * about.X) - point.X, (2f * about.Y) - point.Y);

    private static char ImplicitRepeatCommand(char command)
    {
        return command switch
        {
            'M' => 'L',
            'm' => 'l',
            _ => command
        };
    }

    private static bool IsCommandLetter(char character) => CommandLetters.IndexOf(character) >= 0;

    private static PdfPoint ReadPoint(string data, ref int index, bool relative, in PdfPoint current)
    {
        float x = ReadNumber(data, ref index);
        float y = ReadNumber(data, ref index);

        return relative ? new PdfPoint(current.X + x, current.Y + y) : new PdfPoint(x, y);
    }

    private static float ReadNumber(string data, ref int index)
    {
        SkipSeparators(data, ref index);

        int start = index;

        if (index < data.Length && (data[index] == '+' || data[index] == '-'))
        {
            index++;
        }

        while (index < data.Length && char.IsDigit(data[index]))
        {
            index++;
        }

        if (index < data.Length && data[index] == '.')
        {
            index++;

            while (index < data.Length && char.IsDigit(data[index]))
            {
                index++;
            }
        }

        if (index < data.Length && (data[index] == 'e' || data[index] == 'E'))
        {
            int exponentStart = index;
            index++;

            if (index < data.Length && (data[index] == '+' || data[index] == '-'))
            {
                index++;
            }

            if (index < data.Length && char.IsDigit(data[index]))
            {
                while (index < data.Length && char.IsDigit(data[index]))
                {
                    index++;
                }
            }
            else
            {
                index = exponentStart;
            }
        }

        string token = data.Substring(start, index - start);
        return float.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static void SkipSeparators(string data, ref int index)
    {
        while (index < data.Length && (char.IsWhiteSpace(data[index]) || data[index] == ','))
        {
            index++;
        }
    }
}
