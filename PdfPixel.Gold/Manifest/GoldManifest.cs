using System.IO.Enumeration;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfPixel.Gold.Manifest;

/// <summary>
/// The list of PDFs the corpus is made of, stored as "gold.json" in the working directory.
/// </summary>
internal sealed class GoldManifest
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    /// <summary>
    /// Registered PDFs, sorted by file name.
    /// </summary>
    public List<GoldEntry> Files { get; set; } = new();

    /// <summary>
    /// Reads the manifest.
    /// </summary>
    public static GoldManifest Load(string path)
    {
        using FileStream input = File.OpenRead(path);
        GoldManifest? manifest = JsonSerializer.Deserialize<GoldManifest>(input, SerializerOptions);

        if (manifest == null)
        {
            throw new InvalidDataException($"{path} does not contain a manifest.");
        }

        return manifest;
    }

    /// <summary>
    /// Writes the manifest back, sorted by file name.
    /// </summary>
    public void Save(string path)
    {
        Files.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions) + Environment.NewLine);
    }

    // A short run leaves out the PDFs registered as full-run members; an explicit filter overrides
    // that and selects registered PDFs by file name with or without the extension, so both
    // "basicapi" and "bug1*.pdf" work.
    public List<GoldEntry> SelectEntries(bool fullRun, List<string> filters)
    {
        List<GoldEntry> entries = new();

        foreach (GoldEntry entry in Files)
        {
            if (filters.Count == 0)
            {
                if (fullRun || entry.FullRun != true)
                {
                    entries.Add(entry);
                }
            }
            else if (Matches(entry.Name, filters))
            {
                entries.Add(entry);
            }
        }

        entries.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name));

        return entries;
    }

    private static bool Matches(string fileName, List<string> filters)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);

        foreach (string filter in filters)
        {
            if (FileSystemName.MatchesSimpleExpression(filter, fileName, ignoreCase: true)
                || FileSystemName.MatchesSimpleExpression(filter, name, ignoreCase: true))
            {
                return true;
            }
        }

        return false;
    }
}
