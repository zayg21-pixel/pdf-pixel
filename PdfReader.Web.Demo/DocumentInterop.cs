using Microsoft.Extensions.Logging;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PdfReader.Web.Demo
{
    [SupportedOSPlatform("browser")]
    public partial class DocumentInterop
    {
        private static PdfDocument _document;
        private static readonly InteropSerializerContext context = new InteropSerializerContext();

        [JSExport]
        public static bool LoadDocument(byte[] documentData)
        {
            var factory = new LoggerFactory();
            var reader = new PdfDocumentReader(factory);
            _document = reader.Read(new MemoryStream(documentData), string.Empty);
            return true;
        }

        [JSImport("globalThis.console.log")]
        internal static partial void Log([JSMarshalAs<JSType.String>] string message);

        [JSExport]
        public static byte[] RenderPageToSkp(int pageIndex)
        {
            try
            {
                using var pageRecorder = new SKPictureRecorder();
                var page = _document.Pages[pageIndex];
                using var pageCanvas = pageRecorder.BeginRecording(page.CropBox);
                page.Draw(pageCanvas);

                using var picture = pageRecorder.EndRecording();

                return picture.Serialize().ToArray();
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
                for (int i = 0; i < _document.PageCount; i++)
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
