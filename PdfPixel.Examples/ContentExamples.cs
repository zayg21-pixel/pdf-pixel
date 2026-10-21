using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Content;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Objects;
using PdfPixel.Skia;
using PdfPixel.Skia.Fonts;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Examples;

/// <summary>
/// Extracting the content of a PDF page: its text in reading order, and its images decoded to PNG files
/// with their alternate descriptions.
/// </summary>
internal static class ContentExamples
{
    private const string Format = "Content";
    private const string SourceFile = "hello-world.pdf";

    /// <summary>
    /// Runs every content extraction example.
    /// </summary>
    public static void Run() => ExtractPageContent();

    /// <summary>
    /// Replays the first page of the source document without drawing it, then extracts its words and images
    /// from the content tree the replay builds.
    /// </summary>
    private static void ExtractPageContent()
    {
        Console.WriteLine($"[Content] Extracting the text and images of {SourceFile}...");

        // PdfDocumentReader needs a logger factory and a font substitutor, as for rendering.
        ILoggerFactory loggerFactory = NullLoggerFactory.Instance;
        SkiaFontSubstitutor fontSubstitutor = new(loggerFactory);
        PdfDocumentReader reader = new(loggerFactory, fontSubstitutor);

        // The reader parses lazily from the stream it is handed, so give it the whole file in memory.
        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input("Pdf", SourceFile));
        using MemoryStream pdfStream = new(fileBytes);
        using IPdfDocument document = reader.Read(pdfStream);
        IPdfPage page = document.Pages[0];

        // Held while each command executes, and lets a long replay be cancelled; see the PDF examples.
        object contentLocker = new();
        IPdfExecutionObserver executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        // Replaying a page builds its content tree: every piece of text, path, image and shading with the
        // marked content around it. Null optional content states keep every layer in its default state.
        PdfCommandExecutionParameters executionParameters = new();
        using PdfCommandExecutionContext executionContext = new(
            document,
            executionParameters,
            contentLocker,
            null,
            executionObserver);

        // Extraction needs no pixels: the commands run against a canvas that draws nothing.
        using SKNoDrawCanvas canvas = new((int)page.CropBox.Width, (int)page.CropBox.Height);
        SkCanvasCommandProcessor processor = new(canvas, executionContext, NullLogger<SkCanvasCommandProcessor>.Instance);

        // Content is collected into the tree, and nothing is rendered.
        PdfRenderingParameters renderingParameters = new() { RenderContent = false, ExtractContent = true };

        processor.Process(SaveStateCommand.Instance);

        try
        {
            // Maps the page onto the canvas, so the content tree is measured in page units from the top-left.
            processor.ApplyPageTransformations(page.CropBox);
            page.Render(processor, renderingParameters, executionObserver);
        }
        finally
        {
            processor.Process(RestoreStateCommand.Instance);
        }

        PdfContentNode contentRoot = executionContext.ContentTree.Root;

        ExtractText(contentRoot);
        ExtractImages(contentRoot, executionObserver);
    }

    /// <summary>
    /// Extracts the words of a content tree in reading order and writes them to a text file.
    /// </summary>
    private static void ExtractText(PdfContentNode contentRoot)
    {
        // Words come in reading order, with /ActualText replacements and without artifacts by default.
        PdfTextExtractor textExtractor = new();
        PdfTextExtractionProperties textExtractionProperties = new();

        // The identity matrix keeps word bounds in the units of the content tree.
        PdfWord[] words = textExtractor.ExtractWords(contentRoot, PdfMatrix.Identity, textExtractionProperties);

        // Each word holds its characters, spaces and line breaks included as words of their own.
        StringBuilder text = new();

        foreach (PdfWord word in words)
        {
            foreach (PdfWordPart part in word.Parts)
            {
                foreach (PdfCharacter character in part.Characters)
                {
                    text.Append(character.Text);
                }
            }
        }

        string outputPath = ExamplePaths.Output(Format, "page-text.txt");
        File.WriteAllText(outputPath, text.ToString());

        Console.WriteLine($"[Content]   {words.Length} word(s) -> {ExamplePaths.Relative(outputPath)}");
    }

    /// <summary>
    /// Extracts the images of a content tree and writes each one as a PNG file.
    /// </summary>
    private static void ExtractImages(PdfContentNode contentRoot, IPdfExecutionObserver executionObserver)
    {
        // Only images are wanted here; the same extractor also returns paths, shadings and forms.
        PdfContentKind[] contentKinds = [PdfContentKind.Image];
        PdfContentExtractionParameters contentExtractionParameters = new() { ContentKinds = contentKinds };

        PdfContentExtractor contentExtractor = new();
        PdfContentEntry[] entries = contentExtractor.ExtractContent(contentRoot, contentExtractionParameters);

        // Default parameters apply the image's masks and decode at the stored size.
        PdfImageDecodingParameters decodingParameters = PdfImageDecodingParameters.Default;

        for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            PdfContentEntry entry = entries[entryIndex];

            // An image entry's command carries the image XObject it draws.
            if (entry.Item.Command is not PdfContentCommand<PdfImage> imageCommand)
            {
                continue;
            }

            PdfImage image = imageCommand.Content;

            // Decodes the whole image at its stored size and encodes it as a PNG file.
            byte[] png = image.DecodePng(decodingParameters, executionObserver);

            string outputPath = ExamplePaths.Output(Format, $"image-{entryIndex}.png");
            File.WriteAllBytes(outputPath, png);

            // /Alt of the /Span marked content or the structure element around the image.
            string description = entry.AlternateDescription?.DecodePdfString() ?? "no alternate description";

            Console.WriteLine($"[Content]   {image.Width}x{image.Height} image, {description} -> {ExamplePaths.Relative(outputPath)}");
        }
    }
}
