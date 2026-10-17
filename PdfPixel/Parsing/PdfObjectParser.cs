using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Models;

namespace PdfPixel.Parsing;

/// <summary>
/// Handles parsing of PDF objects and object streams.
/// Performs a single forward scan that can also detect trailer dictionaries to set the document RootRef when present.
/// </summary>
internal class PdfObjectParser
{
    private readonly IPdfDocumentInternal _document;
    private readonly PdfObjectStreamParser _objectStreamParser;
    private readonly ILogger<PdfObjectParser> _logger;

    public PdfObjectParser(IPdfDocumentInternal document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        _document = document;
        _objectStreamParser = new PdfObjectStreamParser(document);
        _logger = document.LoggerFactory.CreateLogger<PdfObjectParser>();
    }

    /// <summary>
    /// Lazily parse a single indexed indirect object using only information from the provided PdfObjectInfo.
    /// </summary>
    /// <param name="reference">Reference the index holds this entry under.</param>
    /// <param name="info">Indexed object metadata.</param>
    /// <returns>Parsed PdfObject or null on failure / unsupported cases.</returns>
    public static System.Collections.Generic.Dictionary<PdfReference, int> ProbeParseCounts = [];
    public static System.Collections.Generic.Dictionary<string, int> ProbeRepeatCallers = [];

    public PdfObject? ParseSingleIndexedObject(in PdfReference reference, PdfObjectInfo info)
    {
        ProbeParseCounts.TryGetValue(reference, out int probeCount);
        ProbeParseCounts[reference] = probeCount + 1;
        if (probeCount > 0)
        {
            string probeCaller = "?";
            foreach (System.Diagnostics.StackFrame probeFrame in new System.Diagnostics.StackTrace(1, false).GetFrames())
            {
                System.Type? probeType = probeFrame.GetMethod()?.DeclaringType;
                if (probeType == null || probeType == typeof(PdfObjectParser) || probeType == typeof(PdfDocumentObjectCache) || probeType == typeof(IPdfValueExtension) || probeType == typeof(PdfDictionary) || probeType == typeof(PdfArray))
                {
                    continue;
                }

                probeCaller = $"{probeType.Name}.{probeFrame.GetMethod()?.Name}";
                break;
            }

            ProbeRepeatCallers.TryGetValue(probeCaller, out int probeCallerCount);
            ProbeRepeatCallers[probeCaller] = probeCallerCount + 1;
        }

        if (info == null)
        {
            return null;
        }

        if (info.IsFree)
        {
            return null;
        }

        if (info.IsCompressed)
        {
            return _objectStreamParser.ParseSingleCompressed(reference, info);
        }

        if (info.Offset == null)
        {
            return null;
        }

        if (info.Offset.Value > int.MaxValue)
        {
            return null;
        }

        // Use unified PdfParser.ReadObject for indirect object parsing (handles header + value + optional stream).
        PdfParser parser = new(_document.Stream, _document, allowReferences: true, decrypt: true);
        parser.Position = (int)info.Offset.Value;

        PdfObject? parsedObject = parser.ReadObject();
        if (parsedObject == null)
        {
            return null;
        }

        // Validate reference matches index metadata to guard against malformed offsets.
        if (parsedObject.Reference != reference)
        {
            _logger.LogWarning(
                "Xref offset {Offset} for {ExpectedReference} actually contains {ActualReference}; treating the xref entry as invalid.",
                info.Offset,
                reference,
                parsedObject.Reference);
            return null;
        }

        return parsedObject;
    }
}
