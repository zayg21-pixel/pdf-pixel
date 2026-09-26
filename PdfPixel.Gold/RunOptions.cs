namespace PdfPixel.Gold;

/// <summary>
/// Switches and PDF name filters of a compare, generate or analyze run.
/// </summary>
internal sealed class RunOptions
{
    /// <summary>
    /// True when the run takes in the full-run members too.
    /// </summary>
    public bool FullRun { get; private set; }

    /// <summary>
    /// True when a comparison collects every differing page into the inspect folder.
    /// </summary>
    public bool Inspect { get; private set; }

    /// <summary>
    /// True when an analysis measures the memory of every page.
    /// </summary>
    public bool MeasureMemory { get; private set; }

    /// <summary>
    /// File name filters selecting the PDFs to process; empty when the run is not filtered.
    /// </summary>
    public List<string> Filters { get; } = new();

    /// <summary>
    /// Name of the run's scope as printed in its summary: "selected", "full" or "short".
    /// </summary>
    public string Scope
    {
        get
        {
            if (Filters.Count > 0)
            {
                return "selected";
            }

            return FullRun ? "full" : "short";
        }
    }

    /// <summary>
    /// Reads the switches, taking every other argument as a filter.
    /// </summary>
    public static RunOptions Parse(IEnumerable<string> arguments)
    {
        RunOptions options = new();

        foreach (string argument in arguments)
        {
            if (string.Equals(argument, "--full", StringComparison.OrdinalIgnoreCase))
            {
                options.FullRun = true;
            }
            else if (string.Equals(argument, "--inspect", StringComparison.OrdinalIgnoreCase))
            {
                options.Inspect = true;
            }
            else if (string.Equals(argument, "--memory", StringComparison.OrdinalIgnoreCase))
            {
                options.MeasureMemory = true;
            }
            else
            {
                options.Filters.Add(argument);
            }
        }

        return options;
    }
}
