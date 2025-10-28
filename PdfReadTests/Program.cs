using Microsoft.Extensions.Logging;
using PdfReader;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReadTests
{
    class Program
    {
        private static readonly ILoggerFactory LoggerFactoryInstance = LoggerFactory.Create(builder => builder.AddConsole(LogLevel.Information));
        private static readonly ILogger Logger = LoggerFactoryInstance.CreateLogger<Program>();
        static async Task Main(string[] args)
        {
            Logger.LogInformation("PDF Direct Rendering Library");
            Logger.LogInformation("===========================");
            Logger.LogInformation(string.Empty);

            // Test with some sample PDFs
            string[] testFiles = {
                //"pdfs//pattern_text_embedded_font.pdf",
                //"pdfs//textframe-gradient.pdf",
                //"pdfs//chrome-text-selection-markedContent.pdf", // interesting, some odd boxes on text
                //"pdfs//colorkeymask.pdf",
                //"pdfs//coons-allflags-withfunction.pdf",
                //"pdfs//lamp_cairo.pdf",
                //"pdfs//tensor4-nofunction.pdf",
                //"pdfs//LATTICE1.pdf",
                //"pdfs//inks.pdf",
                //"pdfs//canvas.pdf",
                //"pdfs//alphatrans.pdf",
                //"pdfs//ArabicCIDTrueType.pdf",
                //"pdfs//asciihexdecode.pdf",
                //"pdfs//complex_ttf_font.pdf",
                //"pdfs//complex_ttf_font_ed.pdf",
                //"//pdfs//icc-lab-8bit.pdf",
                //"pdfs//devicen.pdf",
                //"pdfs//icc-xyz.pdf",
                //"pdfs//icc-lab4.pdf",
                //"pdfs//icc-lab2.pdf",
                //"pdf-example-password.pdf",
                //"pdfs//mixedfonts.pdf", // came a bit broken
                "pdfs//mixedfonts_ed.pdf",
                //"pdfs//blendmode.pdf",
                //"pdfs//calgray.pdf",
                //"pdfs//calrgb.pdf",
                //"pdfs//cmykjpeg.pdf",
                //"pdfs//IndexedCS_negative_and_high.pdf",
                //"pdfs//tiling-pattern-box.pdf",
                //"pdfs//gradientfill.pdf",
                //"pdfs//ccitt_EndOfBlock_false.pdf",
                //"pdfs//images_1bit_grayscale.pdf",
                //"pdfs//shading_extend.pdf",
                //"pdfs//pdf_c.pdf",
                //"PDF32000_2008.pdf", // many exceptions (mostly in JPG, skia also falls)
                //"ch14.pdf"
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
            }
        }

        static async Task TestPdfFile(string filename)
        {
            await Task.Yield();

            // Create a D3D11 device

            using var d3dContext = new VorticeDirect3DContext();
            using var backend = d3dContext.CreateBackendContext();

            // Create GRContext for Direct3D
            using var grContext = GRContext.CreateDirect3D(backend);

            Logger.LogInformation("Testing file: {File}", filename);
            Logger.LogInformation(new string('=', 50));

            if (!File.Exists(filename))
            {
                Logger.LogWarning("File not found: {File}", filename);
                Logger.LogInformation(string.Empty);
                return;
            }

            try
            {
                using (var stream = File.OpenRead(filename))
                {
                    var reader = new PdfDocumentReader(LoggerFactoryInstance);
                    using var document = reader.Read(stream, "test");

                    Logger.LogInformation("Successfully read PDF: {File}", filename);
                    Logger.LogInformation("Total pages: {Count}", document.PageCount);
                    Logger.LogInformation("Actual pages found: {Count}", document.Pages.Count);
                    Logger.LogInformation("Root object: {Root}", document.RootObject);

                    var start = 0;
                    var max = 900;
                    float scaleX = 1f; // Scale factor for rendering

                    var memory = GC.GetTotalMemory(true) / 1024 / 1024;

                    // Analyze pages with detailed content stream debugging
                    for (int i = start; i < Math.Min(max, document.PageCount); i++)
                    {
                        var page = document.Pages[i];
                        Logger.LogInformation("Page {PageNumber}:", page.PageNumber);

                        // Demonstrate rendering to a bitmap
                        try
                        {
                            var renderingBounds = page.CropBox;
                            
                            var renderWidth = (int)Math.Max(renderingBounds.Width, 100); // Minimum 100px
                            var renderHeight = (int)Math.Max(renderingBounds.Height, 100); // Minimum 100px

                            var info = new SKImageInfo((int)(renderWidth * scaleX), (int)(renderHeight * scaleX));
                            //using var surface = SKSurface.Create(grContext, false, info);
                            using var surface = SKSurface.Create(info);

                            using var canvas = surface.Canvas;
                            canvas.ClipRect(new SKRect(0, 0, renderWidth * scaleX, renderHeight * scaleX));
                            canvas.Scale(scaleX, scaleX); // Apply scaling for high-res rendering
                            canvas.Clear(SKColors.White);

                            // Render the page (this will show transformation debug info)
                            page.Draw(canvas);

                            Logger.LogInformation("  === PAGE RENDERING COMPLETE ===");

                            var basePath = Path.Combine(Path.GetDirectoryName(filename), "Test");
                            var name = Path.GetFileNameWithoutExtension(filename);

                            if (!Directory.Exists(basePath))
                            {
                                Directory.CreateDirectory(basePath);
                            }

                            // Save as PNG (optional)
                            var filename_png = $"{basePath}\\{name}_page_{page.PageNumber}.jpg";
                            using (var image = surface.Snapshot())
                            using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 100))
                            using (var fileStream = File.OpenWrite(filename_png))
                            {
                                data.SaveTo(fileStream);
                            }

                            //var recording = CreateRecording(page, scaleX);
                            //var filename_png = $"Test\\{filename}_page_{page.PageNumber}.sk";
                            //using (var image = recording.Serialize())
                            //using (var fileStream = File.OpenWrite(filename_png))
                            //{
                            //    image.SaveTo(fileStream);
                            //}

                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "    Rendering error on page {PageNumber}.", page.PageNumber);
                        }

                        Logger.LogInformation(string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reading PDF: {File}", filename);
                Logger.LogError(ex, "Stack trace logged");
            }

            //while (true)
            //{
            //    await Task.Delay(1000);
            //}
        }

        private static SKPicture CreateRecording(PdfPage pdfPage, float scaleFactor)
        {
            using var recorder = new SKPictureRecorder();
            using var canvas = recorder.BeginRecording(SKRect.Create((float)(pdfPage.CropBox.Width * scaleFactor), (float)(pdfPage.CropBox.Height * scaleFactor)));
            canvas.ClipRect(new SKRect(0, 0, (float)(pdfPage.CropBox.Width * scaleFactor), (float)(pdfPage.CropBox.Height * scaleFactor)));

            pdfPage.Draw(canvas);

            canvas.Flush();
            return recorder.EndRecording();
        }
    }
}
