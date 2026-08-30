using System;
using System.IO;

namespace PdfPixel.Examples;

/// <summary>
/// Resolves the <c>Input</c> and <c>Output</c> folders the examples read from and write to.
/// Both sit next to the executable; <c>Input</c> is copied there on build.
/// </summary>
internal static class ExamplePaths
{
    /// <summary>
    /// Root folder holding the source files of every example.
    /// </summary>
    public static string InputRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Input");

    /// <summary>
    /// Root folder every example writes its images to.
    /// </summary>
    public static string OutputRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Output");

    /// <summary>
    /// Returns the full path of an input file in the given format folder.
    /// </summary>
    /// <param name="format">Format folder name, such as <c>Pdf</c> or <c>Jpg</c>.</param>
    /// <param name="fileName">File name inside that folder.</param>
    public static string Input(string format, string fileName) => Path.Combine(InputRoot, format, fileName);

    /// <summary>
    /// Returns the full path of an output file in the given format folder, creating the folder.
    /// </summary>
    /// <param name="format">Format folder name, such as <c>Pdf</c> or <c>Jpg</c>.</param>
    /// <param name="fileName">File name inside that folder.</param>
    public static string Output(string format, string fileName)
    {
        string directory = Path.Combine(OutputRoot, format);
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, fileName);
    }

    /// <summary>
    /// Returns <paramref name="path"/> relative to the executable folder.
    /// </summary>
    /// <param name="path">Absolute path to shorten.</param>
    public static string Relative(string path) => Path.GetRelativePath(AppContext.BaseDirectory, path);
}
