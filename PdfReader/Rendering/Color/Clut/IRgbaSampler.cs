namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Samples RGBA values from a lookup table or applies a color transformation.
    /// </summary>
    public interface IRgbaSampler
    {
        /// <summary>
        /// True if this sampler is the default no-op sampler that does not modify colors.
        /// </summary>
        bool IsDefault { get; }

        /// <summary>
        /// Copies the color data from the source <see cref="Rgba"/> structure to the destination <see cref="Rgba"/>
        /// structure.
        /// </summary>
        /// <remarks>Both <paramref name="source"/> and <paramref name="destination"/> are passed by
        /// reference, allowing the method to directly modify the destination structure.</remarks>
        /// <param name="source">The source <see cref="Rgba"/> structure containing the color data to copy.</param>
        /// <param name="destination">The destination <see cref="Rgba"/> structure where the color data will be copied.</param>
        void Sample(ref Rgba source, ref Rgba destination);
    }
}
