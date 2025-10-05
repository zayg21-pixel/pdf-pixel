using Microsoft.Extensions.Logging;
using PdfReader;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReadTests
{
    class Program
    {
        private static readonly ILoggerFactory LoggerFactoryInstance = LoggerFactory.Create(builder => builder.AddConsole());
        private static readonly ILogger Logger = LoggerFactoryInstance.CreateLogger<Program>();
        public static int ToDecimal(string octal)
        {
            if (string.IsNullOrWhiteSpace(octal))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(octal));

            if (!TryToDecimal(octal.AsSpan().Trim(), out var value))
                throw new FormatException($"Invalid octal number: '{octal}'.");

            return value;
        }

        public static bool TryToDecimal(ReadOnlySpan<char> octal, out int value)
        {
            value = 0;

            // optional 0o/0O prefix
            if (octal.StartsWith("0o".AsSpan(), StringComparison.OrdinalIgnoreCase))
                octal = octal.Slice(2);

            if (octal.Length == 0)
                return false;

            try
            {
                foreach (char c in octal)
                {
                    if (c < '0' || c > '7') return false;
                    checked { value = value * 8 + (c - '0'); }
                }
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        static async Task Main(string[] args)
        {
            //var items = File.ReadAllText("StandardEncodings2.txt");
            //var split = items.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            //string[] result = new string[256];

            //foreach (var line in split)
            //{
            //    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            //    var name = parts[1];
            //    var part = parts[2];

            //    if (TryToDecimal(part, out var code))
            //    {
            //        result[code] = name;
            //    }
            //}

            //string resString = string.Join(",\r\n", result?.Select(r => r == null ? "null" : $"\"{r}\""));

            int? test = null;
            string rest = test.ToString();

            Logger.LogInformation("PDF Direct Rendering Library");
            Logger.LogInformation("===========================");
            Logger.LogInformation(string.Empty);

            // Test with some sample PDFs
            string[] testFiles = {
                //"pdfs//ArabicCIDTrueType.pdf",
                //"pdfs//asciihexdecode.pdf",
                //"pdfs//complex_ttf_font.pdf",
                //"pdfs//icc-lab-8bit.pdf",
                //"pdfs//devicen.pdf",
                //"pdfs//icc-xyz.pdf",
                //"pdfs//icc-lab4.pdf",
                //"pdfs//icc-lab2.pdf",
                //"Adyen.pdf",
                //"Adyen_debug.pdf",
                //"pdfs//mixedfonts.pdf",
                //"Adyen - Copy.pdf",
                //"pdfs//blendmode.pdf",
                //"pdfs//alphatrans.pdf",
                //"pdfs//calgray.pdf",
                //"pdfs//calrgb.pdf",
                //"pdfs//cmykjpeg.pdf",
                //"pdfs//IndexedCS_negative_and_high.pdf",
                //@"sample.pdf",
                //"pdfs//tiling-pattern-box.pdf",
                //"pdfs//gradientfill.pdf", // TODO: fix, doesn't render
                //@"document - Copy.pdf",
                @"documentS.pdf",
                //@"documentC.pdf",
                //"pdfs//ccitt_EndOfBlock_false.pdf",
                //"pdfs//images_1bit_grayscale.pdf",
                //"adyen_2020.pdf",
                //"documentEd.pdf"
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
                    var reader = new PdfDocumentReader(new LoggerFactory());
                    using var document = reader.Read(stream);

                    Logger.LogInformation("Successfully read PDF: {File}", filename);
                    Logger.LogInformation("Total objects: {Count}", document.Objects.Count);
                    Logger.LogInformation("Total pages: {Count}", document.PageCount);
                    Logger.LogInformation("Actual pages found: {Count}", document.Pages.Count);
                    Logger.LogInformation("Root object: {Root}", document.RootRef);
                    Logger.LogInformation(string.Empty);

                    var start = 0;
                    var max = 300;
                    float scaleX = 3f; // Scale factor for rendering

                    var memory = GC.GetTotalMemory(true) / 1024 / 1024;

                    // Analyze pages with detailed content stream debugging
                    for (int i = start; i < Math.Min(max, document.PageCount); i++)
                    {
                        var page = document.Pages[i];
                        Logger.LogInformation("Page {PageNumber}:", page.PageNumber);

                        // Demonstrate rendering to a bitmap
                        try
                        {
                            // Use the effective rendering bounds (CropBox if available, otherwise MediaBox)
                            // This also accounts for rotation by swapping dimensions when needed
                            var renderingBounds = page.CropBox;
                            
                            var renderWidth = (int)Math.Max(renderingBounds.Width, 100); // Minimum 100px
                            var renderHeight = (int)Math.Max(renderingBounds.Height, 100); // Minimum 100px

                            var info = new SKImageInfo((int)(renderWidth * scaleX), (int)(renderHeight * scaleX));

                            using (var surface = SKSurface.Create(info))
                            {
                                using var canvas = surface.Canvas;
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
                                var filename_png = $"{basePath}\\{name}_page_{page.PageNumber}.png";
                                using (var image = surface.Snapshot())
                                using (var data = image.Encode())
                                using (var fileStream = File.OpenWrite(filename_png))
                                {
                                    data.SaveTo(fileStream);
                                }

                                //if (i == max - 1)
                                //{
                                //    while (true)
                                //    {
                                //        GC.Collect();
                                //        GC.WaitForPendingFinalizers();
                                //        GC.WaitForFullGCComplete();
                                //        await Task.Delay(100);
                                //    }
                                //}

                                //var recording = CreateRecording(page, scaleX);
                                //var filename_png = $"Test\\{filename}_page_{page.PageNumber}.sk";
                                //using (var image = recording.Serialize())
                                //using (var fileStream = File.OpenWrite(filename_png))
                                //{
                                //    image.SaveTo(fileStream);
                                //}

                                //Console.WriteLine($"    Rendered successfully and saved as {filename_png}");
                            }

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

            Logger.LogInformation(string.Empty);
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
