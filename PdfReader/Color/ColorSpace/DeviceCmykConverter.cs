using PdfReader.Color.Icc.Converters;
using PdfReader.Color.Icc.Model;
using PdfReader.Models;
using PdfReader.Resources;
using SkiaSharp;
using System;

namespace PdfReader.Color.ColorSpace
{
    internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
    {
        public static readonly DeviceCmykConverter Instance = new DeviceCmykConverter();
        private static readonly IccCmykColorConverter _iccCmykConverter;

        static DeviceCmykConverter()
        {
            var cmykIccBytes = PdfResourceLoader.GetResource("CompactCmyk.icc");
            var cmykProfile = IccProfile.Parse(cmykIccBytes);
            _iccCmykConverter = new IccCmykColorConverter(cmykProfile);
        }

        public override int Components => 4;

        public override bool IsDevice => true;

        protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
        {
            _iccCmykConverter.TryToSrgb01(comps01, renderingIntent, out var srgb01);
            return new SKColor(ToByte(srgb01.X), ToByte(srgb01.Y), ToByte(srgb01.Z));
        }
    }
}
