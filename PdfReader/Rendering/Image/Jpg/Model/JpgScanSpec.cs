using System.Collections.Generic;

namespace PdfReader.Rendering.Image.Jpg.Model
{
    /// <summary>
    /// Describes a single JPEG scan header (SOS) without the entropy payload.
    /// </summary>
    internal sealed class JpgScanSpec
    {
        public List<JpgScanComponentSpec> Components { get; } = new List<JpgScanComponentSpec>();
        public int SpectralStart { get; set; }
        public int SpectralEnd { get; set; }
        public int SuccessiveApproxHigh { get; set; }
        public int SuccessiveApproxLow { get; set; }
    }

    /// <summary>
    /// Component selector inside an SOS header: component id and DC/AC table selectors.
    /// </summary>
    internal sealed class JpgScanComponentSpec
    {
        public byte ComponentId { get; set; }
        public int DcTableId { get; set; }
        public int AcTableId { get; set; }
    }
}
