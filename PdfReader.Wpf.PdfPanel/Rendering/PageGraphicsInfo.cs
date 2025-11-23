namespace PdfReader.Wpf.PdfPanel.Rendering
{
    internal readonly struct PageGraphicsInfo
    {
        public PageGraphicsInfo(bool hasImages)
        {
            HasImages = hasImages;
        }

        public bool HasImages { get; }
    }
}
