using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Management;
using PdfPixel.Models;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.Console.Demo
{
    class Program
    {
        private static readonly ILoggerFactory LoggerFactoryInstance = LoggerFactory.Create(builder => builder.AddConsole(LogLevel.Information));
        private static readonly ILogger Logger = LoggerFactoryInstance.CreateLogger<Program>();
        private static readonly ISkiaFontProvider FontProvider = new WindowsSkiaFontProvider();

        static async Task Main(string[] args)
        {
            Logger.LogInformation("PDF Direct Rendering Library");
            Logger.LogInformation("===========================");
            Logger.LogInformation(string.Empty);

            // Test with some sample PDFs
            string[] testFiles = {
                //"pdfs//freeculture.pdf"
                //"pdfs//100mb.pdf",
                //"pdfs//PDF-Horizontal-Scaling.pdf",
                //"pdfs//pattern_text_embedded_font.pdf",
                //"pdfs//textframe-gradient.pdf",
                //"pdfs//chrome-text-selection-markedContent.pdf", // interesting, some odd boxes on text
                //"pdfs//colorkeymask.pdf",
                "pdfs//coons-allflags-withfunction.pdf",
                //"pdfs//lamp_cairo.pdf",
                //"pdfs//tensor4-nofunction.pdf",
                //"pdfs//LATTICE1.pdf",
                //"pdfs//inks.pdf",
                //"pdfs//canvas.pdf",
                //"pdfs//personwithdog.pdf",
                //"pdfs//alphatrans.pdf",
                //"pdfs//ArabicCIDTrueType.pdf",
                //"pdfs//asciihexdecode.pdf",
                //"pdfs//complex_ttf_font.pdf",
                //"pdfs//complex_ttf_font_ed.pdf",
                //"pdfs//icc-lab-8bit.pdf",
                //"pdfs//devicen.pdf",
                //"pdfs//icc-xyz.pdf",
                //"pdfs//icc-lab4.pdf",
                //"pdfs//icc-lab2.pdf",
                //"pdf-example-password.pdf",
                //"pdfs//mixedfonts.pdf", // came a bit broken
                //"pdfs//mixedfonts_ed.pdf",
                //"pdfs//blendmode.pdf",
                //"pdfs//calgray.pdf",
                //"pdfs//calrgb.pdf",
                //"pdfs//colorspace_cos.pdf",
                //"pdfs//cmykjpeg.pdf",
                //"pdfs//IndexedCS_negative_and_high.pdf",
                //"pdfs//tiling-pattern-box.pdf",
                //"pdfs//gradientfill.pdf",
                //"pdfs//ccitt_EndOfBlock_false.pdf",
                //"pdfs//images_1bit_grayscale.pdf",
                //"pdfs//shading_extend.pdf",
                //"pdfs//pdf_c.pdf",
                //"pdfs//1208.0264v4.pdf",
                //"pdfs//806-5413-10.pdf",
                //"pdfs//1208.0264v4_ed.pdf",
                //"pdfs//1405.2785v3.pdf",
                //"pdfs//5091.Design_MM_Fonts.pdf",
                //"pdfs//2009science11_12.pdf",
                //"PDF32000_2008.pdf",
                "ch14.pdf"
                //@"documentS.pdf",
                //@"documentC.pdf",
                //@"sample.pdf",
                //"Adyen.pdf",
                //"Adyen 2023.pdf",
                //"adyen_2020.pdf",
                //"adyen_2020_debug.pdf",
                //"pdfs\\emojies.pdf",
                //"documentEd.pdf",
                //@"document_1.pdf"
            };

            foreach (var file in testFiles)
            {
                await TestPdfFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file));
                //TextTextExtraction(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file));
            }
        }

        static void TextTextExtraction(string fileName)
        {
            if (!File.Exists(fileName))
            {
                Logger.LogWarning("File not found: {File}", fileName);
                Logger.LogInformation(string.Empty);
                return;
            }

            Stopwatch sw = Stopwatch.StartNew();
            Logger.LogInformation("Extracting text from file: {File}", fileName);

            var reader = new PdfDocumentReader(LoggerFactoryInstance, FontProvider);
            using var file = File.OpenRead(fileName);
            using var document = reader.Read(file);

            Dictionary<int, List<PdfCharacter>> extractedText = new Dictionary<int, List<PdfCharacter>>();

            for (int i = 0; i < document.Pages.Count; i++)
            {
                var page = document.Pages[i];
                var textContent = page.ExtractText();
                extractedText[page.PageNumber] = textContent;
            }

            sw.Stop();

            Logger.LogInformation("Extracted text from PDF: {File} in {ElapsedMilliseconds} ms", fileName, sw.ElapsedMilliseconds);
        }

        static async Task TestPdfFile(string fileName)
        {
            await Task.Yield();

            // Create a D3D11 device

            //using var d3dContext = new VorticeDirect3DContext();
            //using var backend = d3dContext.CreateBackendContext();

            //// Create GRContext for Direct3D
            //using var grContext = GRContext.CreateDirect3D(backend);

            Logger.LogInformation("Testing file: {File}", fileName);
            Logger.LogInformation(new string('=', 50));

            if (!File.Exists(fileName))
            {
                Logger.LogWarning("File not found: {File}", fileName);
                Logger.LogInformation(string.Empty);
                return;
            }

            try
            {
                
                var reader = new PdfDocumentReader(LoggerFactoryInstance, FontProvider);
                using var file = File.OpenRead(fileName);
                using var document = reader.Read(file);

                Logger.LogInformation("Successfully read PDF: {File}", fileName);
                Logger.LogInformation("Total pages: {Count}", document.Pages.Count);

                var start = 0;
                var max = 1000;
                float scaleX = 1f; // Scale factor for rendering

                // Analyze pages with detailed content stream debugging
                for (int i = start; i < Math.Min(max, document.Pages.Count); i++)
                {
                    var page = document.Pages[i];
                    Logger.LogInformation("Page {PageNumber}:", page.PageNumber);

                    // Demonstrate rendering to a bitmap
                    try
                    {
                        var renderingBounds = page.CropBox;

                        var renderWidth = (int)Math.Max(renderingBounds.Width, 100); // Minimum 100px
                        var renderHeight = (int)Math.Max(renderingBounds.Height, 100); // Minimum 100px

                        var info = new SKImageInfo((int)(renderWidth * scaleX), (int)(renderHeight * scaleX), SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
                        //using var surface = SKSurface.Create(grContext, false, info);
                        using var surface = SKSurface.Create(info);

                        using var canvas = surface.Canvas;
                        canvas.Scale(scaleX, scaleX); // Apply scaling for high-res rendering
                        canvas.Clear(SKColors.White);

                        // Render the page (this will show transformation debug info)
                        page.Draw(canvas, new PdfRenderingParameters(), CancellationToken.None);

                        Logger.LogInformation("  === PAGE RENDERING COMPLETE ===");

                        var basePath = Path.Combine(Path.GetDirectoryName(fileName), "Test");
                        var name = Path.GetFileNameWithoutExtension(fileName);

                        if (!Directory.Exists(basePath))
                        {
                            Directory.CreateDirectory(basePath);
                        }

                        //if (page.PageNumber == document.PageCount)
                        //{
                        //    Console.WriteLine();
                        //}

                        // Save as PNG (optional)
                        //var filename_png = $"{basePath}\\{name}_page_{page.PageNumber}.jpg";
                        //using (var image = surface.Snapshot())
                        //using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 100))
                        //using (var fileStream = File.OpenWrite(filename_png))
                        //{
                        //    data.SaveTo(fileStream);
                        //}

                        using var recording = CreateRecording(page);
                        SaveSkp(recording, $"{basePath}\\{name}_page_{page.PageNumber}.skp");
                        //SaveRecording(recording, scaleX, filename_png);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "    Rendering error on page {PageNumber}.", page.PageNumber);
                    }

                    Logger.LogInformation(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading PDF: {File}", fileName);
                Logger.LogError(ex, "Stack trace logged");
            }
        }

        private static SKPicture CreateRecording(PdfPage pdfPage)
        {
            using var recorder = new SKPictureRecorder();
            using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            canvas.Clear(SKColors.White);
            pdfPage.Draw(canvas, new PdfRenderingParameters(), CancellationToken.None);

            canvas.Flush();
            return recorder.EndRecording();
        }

        private static void SaveSkp(SKPicture picture, string path)
        {
            using var fileStream = File.OpenWrite(path);
            picture.Serialize(fileStream);
        }

        private static void SaveRecording(SKPicture picture, float scale, string path)
        {
            var matrix = SKMatrix.CreateScale(scale, scale);
            using var image = SKImage.FromPicture(picture, new SKSizeI((int)(picture.CullRect.Width * scale), (int)(picture.CullRect.Height * scale)), matrix);
            using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 100))
            using (var fileStream = File.OpenWrite(path))
            {
                data.SaveTo(fileStream);
            }
        }
    }
}
