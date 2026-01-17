using Microsoft.Extensions.Logging;
using PdfRender.Fonts.Management;
using PdfRender.Models;
using SkiaSharp;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdfRender.Web.Demo
{
    [SupportedOSPlatform("browser")]
    public partial class DocumentInterop
    {
        private static PdfDocument _document;
        private static readonly InteropSerializerContext context = new InteropSerializerContext();
        private static readonly ISkiaFontProvider _fontProvider = new WindowsSkiaFontProvider();

        [JSExport]
        public static bool LoadDocument(byte[] documentData)
        {
            var factory = new LoggerFactory();
            var reader = new PdfDocumentReader(factory, _fontProvider);
            _document = reader.Read(new MemoryStream(documentData), string.Empty);
            return true;
        }

        [JSImport("globalThis.console.log")]
        internal static partial void Log([JSMarshalAs<JSType.String>] string message);

        [JSExport]
        [return: JSMarshalAs<JSType.MemoryView>]
        public static Span<byte> RenderPageToSkp(int pageIndex)
        {
            try
            {
                using var pageRecorder = new SKPictureRecorder();
                var page = _document.Pages[pageIndex];
                using var pageCanvas = pageRecorder.BeginRecording(page.CropBox);
                page.Draw(pageCanvas, new PdfRenderingParameters());

                using var picture = pageRecorder.EndRecording();

                return picture.Serialize().Span;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return null;
            }
        }

        [JSExport]
        public static string GetAllPageInfo()
        {
            if (_document == null)
            {
                return null;
            }

            try
            {
                var allPageInfo = new List<PageInfo>();
                for (int i = 0; i < _document.Pages.Count; i++)
                {
                    var page = _document.Pages[i];
                    var pageInfo = new PageInfo
                    {
                        Width = page.CropBox.Width,
                        Height = page.CropBox.Height,
                        Rotation = page.Rotation
                    };
                    allPageInfo.Add(pageInfo);
                }

                return JsonSerializer.Serialize(allPageInfo.ToArray(), context.PageInfoArray);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return null;
            }
        }
    }

    [JsonSerializable(typeof(PageInfo[]))]
    public partial class InteropSerializerContext : JsonSerializerContext { }

    public class PageInfo
    {
        public float Width { get; set; }

        public float Height { get; set; }

        public int Rotation { get; set; }
    }
}
