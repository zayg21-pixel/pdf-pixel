namespace PdfPixel.Gold;

/// <summary>
/// Locations inside the working directory: "gold.json" at its root, and the downloaded PDFs, their
/// golden snapshots and the inspect folder in fixed subfolders.
/// </summary>
internal sealed class GoldPaths
{
    public GoldPaths(string workingDirectory)
    {
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        ManifestPath = Path.Combine(WorkingDirectory, "gold.json");
        SourceDirectory = Path.Combine(WorkingDirectory, "Source");
        SnapshotsDirectory = Path.Combine(WorkingDirectory, "Snapshots");
        InspectDirectory = Path.Combine(WorkingDirectory, "Inspect");
    }

    /// <summary>
    /// The working directory given on the command line, as a full path.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The "gold.json" manifest.
    /// </summary>
    public string ManifestPath { get; }

    /// <summary>
    /// Folder the PDFs are downloaded into.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    /// Folder holding one subfolder of golden snapshots per PDF.
    /// </summary>
    public string SnapshotsDirectory { get; }

    /// <summary>
    /// Folder a comparison collects its differing pages into.
    /// </summary>
    public string InspectDirectory { get; }

    /// <summary>
    /// Path of the downloaded PDF with the given file name.
    /// </summary>
    public string GetSourcePath(string pdfName) => Path.Combine(SourceDirectory, pdfName);

    /// <summary>
    /// Folder of the golden snapshots of the PDF with the given file name.
    /// </summary>
    public string GetSnapshotDirectory(string pdfName) => Path.Combine(SnapshotsDirectory, Path.GetFileNameWithoutExtension(pdfName));
}
