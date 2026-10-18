using System;
using System.IO;

namespace Benchmarks
{
    /// <summary>
    /// A page of a PDF from the local test corpus.
    /// </summary>
    public sealed class CorpusPage
    {
        private const string SolutionFileName = "PdfPixel.slnx";
        private const string CorpusDirectory = "PdfPixel.Demo.Wpf/bin/Debug/net10.0-windows/pdfs/OldCache";

        public CorpusPage(string fileName, int pageNumber)
        {
            FileName = fileName;
            PageNumber = pageNumber;
        }

        /// <summary>
        /// PDF file name inside the corpus directory, without extension.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// One-based page number.
        /// </summary>
        public int PageNumber { get; }

        /// <summary>
        /// Path-heavy pages used to measure the PDF value model.
        /// </summary>
        public static CorpusPage[] PathHeavyPages =>
        [
            new("CRH_2021", 117),
            new("KLM_2019", 135)
        ];

        /// <summary>
        /// Reads the whole PDF file into memory.
        /// </summary>
        public byte[] ReadAllBytes() => File.ReadAllBytes(Path.Combine(FindRepositoryRoot(), CorpusDirectory, FileName + ".pdf"));

        /// <inheritdoc />
        public override string ToString() => $"{FileName} p{PageNumber}";

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"{SolutionFileName} not found above {AppContext.BaseDirectory}.");
        }
    }
}
