using System;
using System.Collections.Generic;
using System.IO;
using PdfReader.Models;
using PdfReader.Parsing;

namespace PdfReader
{
    /// <summary>
    /// Main entry point for reading PDF documents
    /// Orchestrates the parsing process using specialized parsers
    /// Enhanced to support PDF 1.7 features including cross-reference streams and object streams
    /// </summary>
    public static class PdfDocumentReader
    {
        /// <summary>
        /// Read a PDF document from a stream with full PDF 1.7 support
        /// </summary>
        public static PdfDocument Read(Stream stream)
        {
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            
            var context = new PdfParseContext(buffer);
            var document = new PdfDocument();

            try
            {
                // Step 1: Parse and validate PDF version
                var (isValidVersion, version, requiresAdvancedFeatures) = PdfVersionParser.ParsePdfVersion(ref context);
                
                if (!isValidVersion)
                {
                    Console.WriteLine($"Warning: Invalid or unsupported PDF version: {version}");
                    // Continue parsing anyway - might still work
                }
                else
                {
                    Console.WriteLine($"PDF version {version} detected");
                    if (requiresAdvancedFeatures)
                    {
                        Console.WriteLine("Advanced PDF features required - using enhanced parsers");
                        var features = PdfVersionParser.GetRequiredFeatures(version);
                        Console.WriteLine($"Required features: {features}");
                    }
                }

                // Step 2: Find and parse cross-reference information
                int xrefPosition = PdfXrefParser.FindStartXref(ref context);
                
                if (xrefPosition >= 0)
                {
                    // Check if this is a cross-reference stream (PDF 1.5+) or traditional table
                    if (requiresAdvancedFeatures && PdfXrefStreamParser.IsXrefStream(ref context, xrefPosition))
                    {
                        Console.WriteLine("Cross-reference stream detected - using advanced parser");
                        PdfXrefStreamParser.ParseXrefStream(ref context, document, xrefPosition);
                    }
                    else
                    {
                        Console.WriteLine("Traditional cross-reference table detected");
                        PdfXrefParser.ParseXrefAndTrailer(ref context, document, xrefPosition);
                    }
                }
                else
                {
                    Console.WriteLine("Warning: No cross-reference table found");
                }

                // Step 3: Parse all objects (including object streams for PDF 1.5+)
                PdfObjectParser.ParseObjects(ref context, document);

                // Step 4: Process object streams if present (PDF 1.5+ feature)
                if (requiresAdvancedFeatures)
                {
                    ProcessObjectStreams(document);
                }

                // Step 5: Extract page information
                PdfPageExtractor.ExtractPages(document);
                
                // Step 6: Load font resources
                PdfResourceLoader.LoadPageResources(document);
                
                Console.WriteLine($"Successfully parsed PDF {version} with {document.PageCount} pages");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing PDF: {ex.Message}");
                // Return partial document to allow some functionality
            }

            return document;
        }

        /// <summary>
        /// Process object streams to extract compressed objects (PDF 1.5+ feature)
        /// </summary>
        private static void ProcessObjectStreams(PdfDocument document)
        {
            var objectStreams = new List<PdfObject>();
            
            // Find all object streams
            foreach (var obj in document.Objects.Values)
            {
                if (PdfObjectStreamParser.IsObjectStream(obj))
                {
                    objectStreams.Add(obj);
                }
            }

            if (objectStreams.Count > 0)
            {
                Console.WriteLine($"Found {objectStreams.Count} object stream(s) - extracting compressed objects");
                
                int totalExtracted = 0;
                foreach (var objStream in objectStreams)
                {
                    if (PdfObjectStreamParser.ValidateObjectStream(objStream))
                    {
                        var extracted = PdfObjectStreamParser.ExtractObjectsFromSingleStream(document, objStream);
                        totalExtracted += extracted;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid object stream: {objStream.Reference.ObjectNumber}");
                    }
                }
                
                Console.WriteLine($"Extracted {totalExtracted} objects from object streams");
            }
        }
    }
}
