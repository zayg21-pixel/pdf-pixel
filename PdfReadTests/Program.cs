using PdfReader;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReadTests
{
    class Program
    {
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

            Console.WriteLine("PDF Direct Rendering Library");
            Console.WriteLine("===========================");
            Console.WriteLine();

            // Test with some sample PDFs
            string[] testFiles = {
                //"pdfs//ArabicCIDTrueType.pdf",
                //"pdfs//asciihexdecode.pdf",
                //"pdfs//complex_ttf_font.pdf",
                //"pdfs//icc-lab-8bit.pdf",
                "pdfs//devicen.pdf",
                //"pdfs//icc-xyz.pdf",
                //"pdfs//icc-lab4.pdf",
                //"pdfs//icc-lab2.pdf",
                //"Adyen.pdf",
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
                //"pdfs//gradientfill.pdf",
                //@"document - Copy.pdf",
                //@"documentEdD.pdf",
                //@"documentS.pdf",
                //@"documentC.pdf",
                //"pdfs//ccitt_EndOfBlock_false.pdf",
                //"pdfs//images_1bit_grayscale.pdf",
                //"adyen_2020.pdf"
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
            Console.WriteLine($"Testing file: {filename}");
            Console.WriteLine(new string('=', 50));

            if (!File.Exists(filename))
            {
                Console.WriteLine($"File not found: {filename}");
                Console.WriteLine();
                return;
            }

            try
            {
                using (var stream = File.OpenRead(filename))
                {
                    using var document = PdfDocumentReader.Read(stream);

                    Console.WriteLine($"Successfully read PDF: {filename}");
                    Console.WriteLine($"Total objects: {document.Objects.Count}");
                    Console.WriteLine($"Total pages: {document.PageCount}");
                    Console.WriteLine($"Actual pages found: {document.Pages.Count}");
                    Console.WriteLine($"Root object: {document.RootRef}");
                    Console.WriteLine();

                    var start = 0;
                    var max = 200;
                    float scaleX = 3f; // Scale factor for rendering

                    var memory = GC.GetTotalMemory(true) / 1024 / 1024;

                    // Analyze pages with detailed content stream debugging
                    for (int i = start; i < Math.Min(max, document.PageCount); i++)
                    {
                        var page = document.Pages[i];
                        Console.WriteLine($"Page {page.PageNumber}:");

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

                                Console.WriteLine($"  === PAGE RENDERING COMPLETE ===");

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
                            Console.WriteLine($"    Rendering error: {ex.Message}");
                            Console.WriteLine($"    Stack trace: {ex.StackTrace}");
                        }

                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading PDF: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine();
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
