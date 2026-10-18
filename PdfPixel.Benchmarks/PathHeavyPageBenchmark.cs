using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel;
using PdfPixel.Commands.Model;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Skia.Fonts;
using PdfPixel.Streams;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Benchmarks
{
    /// <summary>
    /// PDF value model costs on real path-heavy pages: content stream tokenizing, page recording and document loading.
    /// </summary>
    [MemoryDiagnoser]
    public class PathHeavyPageBenchmark
    {
        private readonly List<ReadOnlyMemory<byte>> _pageContentStreams = [];
        private readonly List<ReadOnlyMemory<byte>> _formContentStreams = [];
        private readonly HashSet<PdfReference> _visitedForms = [];
        private readonly PdfRenderingParameters _renderingParameters = new();
        private readonly IPdfExecutionObserver _executionObserver = new PdfCancellationExecutionObserver(CancellationToken.None);

        private PdfDocumentReader _reader;
        private byte[] _documentBytes;
        private MemoryStream _documentStream;
        private IPdfDocument _document;
        private IPdfPage _page;

        [ParamsSource(nameof(Pages))]
        public CorpusPage Page { get; set; }

        public static IEnumerable<CorpusPage> Pages => CorpusPage.PathHeavyPages;

        [GlobalSetup]
        public void Setup()
        {
            SkiaFontSubstitutor fontSubstitutor = new(NullLoggerFactory.Instance);
            _reader = new PdfDocumentReader(NullLoggerFactory.Instance, fontSubstitutor);
            _documentBytes = Page.ReadAllBytes();
            _documentStream = new MemoryStream(_documentBytes);
            _document = _reader.Read(_documentStream);
            _page = _document.Pages[Page.PageNumber - 1];

            IPdfPageInternal pageInternal = (IPdfPageInternal)_page;

            foreach (PdfObjectStream contentStream in pageInternal.ContentStreams)
            {
                ReadOnlyMemory<byte> contentData = contentStream.DecodeAsMemory();

                if (!contentData.IsEmpty)
                {
                    _pageContentStreams.Add(contentData);
                }
            }

            IPdfDocumentInternal documentInternal = (IPdfDocumentInternal)_document;
            PdfObject pageObject = documentInternal.ObjectCache.GetObject(pageInternal.PageReference);
            CollectFormContentStreams(pageObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _document.Dispose();
            _documentStream.Dispose();
        }

        /// <summary>
        /// Tokenizes the decoded page content and every form XObject reachable from the page resources,
        /// splitting operators from operands the way the content renderer does.
        /// </summary>
        [Benchmark]
        public int ParseContent()
        {
            PdfParseContext pageParseContext = new(_pageContentStreams);
            int operatorCount = ParseContext(pageParseContext);

            foreach (ReadOnlyMemory<byte> formContent in _formContentStreams)
            {
                PdfParseContext formParseContext = new(formContent);
                operatorCount += ParseContext(formParseContext);
            }

            return operatorCount;
        }

        /// <summary>
        /// Interprets the page content into drawing commands, including content stream decoding.
        /// </summary>
        [Benchmark]
        public int RecordPage()
        {
            PdfCommandRecorder recorder = new();
            _page.Render(recorder, _renderingParameters, _executionObserver);

            return recorder.Commands.Count;
        }

        /// <summary>
        /// Loads the whole document from memory and resolves its page tree.
        /// </summary>
        [Benchmark]
        public int LoadDocument()
        {
            using MemoryStream documentStream = new(_documentBytes);
            using IPdfDocument document = _reader.Read(documentStream);

            return document.Pages.Count;
        }

        private int ParseContext(PdfParseContext parseContext)
        {
            PdfParser parser = new(parseContext, (IPdfDocumentInternal)_document, PdfParserMode.ContentStream);
            PdfOperandStack operandStack = new();
            int operatorCount = 0;
            PdfValue value;

            while ((value = parser.ReadNextValue(operandStack)) != null)
            {
                if (value is PdfOperatorValue)
                {
                    operatorCount++;
                    operandStack.Clear();
                }
                else
                {
                    operandStack.Push(value);
                }
            }

            return operatorCount;
        }

        private void CollectFormContentStreams(PdfDictionary resources)
        {
            PdfDictionary xObjects = resources?.GetDictionary(PdfTokens.XObjectKey);

            if (xObjects == null)
            {
                return;
            }

            foreach (PdfString name in xObjects.RawValues.Keys)
            {
                PdfObject xObject = xObjects.GetObject(name);

                if (xObject == null
                    || xObject.Reference == null
                    || !_visitedForms.Add(xObject.Reference.Value)
                    || xObject.Dictionary.GetName(PdfTokens.SubtypeKey) != PdfTokens.FormSubtype)
                {
                    continue;
                }

                ReadOnlyMemory<byte> formContent = xObject.Stream.DecodeAsMemory();

                if (!formContent.IsEmpty)
                {
                    _formContentStreams.Add(formContent);
                }

                CollectFormContentStreams(xObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey));
            }
        }
    }
}
