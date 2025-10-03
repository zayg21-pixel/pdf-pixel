namespace PdfReader.Rendering.Image.Jpg.Model
{
    /// <summary>
    /// Component sampling information calculated from header.
    /// </summary>
    public readonly struct SamplingInfo
    {
        public readonly int MaxHorizontalSampling;
        public readonly int MaxVerticalSampling;
        public readonly int[] ComponentWidths;
        public readonly int[] ComponentHeights;
        public readonly int[] ComponentBlocksX;
        public readonly int[] ComponentBlocksY;

        public SamplingInfo(int maxH, int maxV, int[] widths, int[] heights, int[] blocksX, int[] blocksY)
        {
            MaxHorizontalSampling = maxH;
            MaxVerticalSampling = maxV;
            ComponentWidths = widths;
            ComponentHeights = heights;
            ComponentBlocksX = blocksX;
            ComponentBlocksY = blocksY;
        }
    }
}