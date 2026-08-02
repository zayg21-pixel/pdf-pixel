using System.Text.Json;

namespace PdfPixel.Gold;

/// <summary>
/// The list of PDFs the corpus is made of, stored as "gold.json" next to the executable. A PDF in
/// the source folder is only rendered once it appears here.
/// </summary>
internal sealed class GoldManifest
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Registered PDFs, kept sorted by file name.
    /// </summary>
    public List<GoldEntry> Files { get; set; } = new();

    /// <summary>
    /// Reads the manifest, returning an empty one when the file does not exist yet.
    /// </summary>
    public static GoldManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            return new();
        }

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

        using FileStream output = File.Create(path);
        JsonSerializer.Serialize(output, this, SerializerOptions);
    }
}
