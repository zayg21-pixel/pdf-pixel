using System.Globalization;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// One command of a panel script: its name and numeric arguments.
/// </summary>
internal sealed class PanelScriptLine
{
    private static readonly Dictionary<string, int> ArgumentCounts = new()
    {
        ["size"] = 2,
        ["zoom"] = 1,
        ["scroll"] = 2,
        ["scroll_relative"] = 2,
        ["page"] = 1,
        ["sleep"] = 1,
    };

    private PanelScriptLine(string text, string name, float[] arguments)
    {
        Text = text;
        Name = name;
        Arguments = arguments;
    }

    /// <summary>
    /// The line as written in the script.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The command name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The command arguments, in script order.
    /// </summary>
    public float[] Arguments { get; }

    /// <summary>
    /// Reads every non-empty line of the script at <paramref name="path"/>.
    /// </summary>
    public static List<PanelScriptLine> ParseFile(string path)
    {
        string[] sourceLines = File.ReadAllLines(path);
        List<PanelScriptLine> lines = [];

        for (int lineIndex = 0; lineIndex < sourceLines.Length; lineIndex++)
        {
            string text = sourceLines[lineIndex].Trim();

            if (text.Length == 0)
            {
                continue;
            }

            string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string name = tokens[0];

            if (!ArgumentCounts.TryGetValue(name, out int argumentCount))
            {
                throw new FormatException($"Line {lineIndex + 1}: unknown command '{name}'.");
            }

            if (tokens.Length - 1 != argumentCount)
            {
                throw new FormatException($"Line {lineIndex + 1}: '{name}' takes {argumentCount} argument(s).");
            }

            float[] arguments = new float[argumentCount];

            for (int argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                arguments[argumentIndex] = float.Parse(tokens[argumentIndex + 1], CultureInfo.InvariantCulture);
            }

            lines.Add(new PanelScriptLine(text, name, arguments));
        }

        return lines;
    }
}
