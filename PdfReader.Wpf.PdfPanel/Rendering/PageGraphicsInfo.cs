namespace PdfReader.Wpf.PdfPanel.Rendering
{
    internal readonly struct PageGraphicsInfo
    {
        public PageGraphicsInfo(double maxImageScale, bool hasImages)
        {
            MaxImageScale = maxImageScale;
            HasImages = hasImages;
        }

        public double MaxImageScale { get; }

        public bool HasImages { get; }
    }
}
