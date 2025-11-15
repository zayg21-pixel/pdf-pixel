using PdfReader.Color.ColorSpace;

namespace PdfReader.Transparency.Model;

/// <summary>
/// Represents a PDF transparency group
/// Used for controlling how groups of objects blend together before compositing with the background
/// </summary>
public class PdfTransparencyGroup
{
    /// <summary>
    /// Resolved color space converter for the group's blending color space.
    /// Falls back to DeviceRGB when unspecified or unsupported.
    /// </summary>
    public PdfColorSpaceConverter ColorSpaceConverter { get; set; }
    
    /// <summary>
    /// Isolated flag (I) - if true, objects in group don't interact with backdrop
    /// </summary>
    public bool Isolated { get; set; } = false;
    
    /// <summary>
    /// Knockout flag (K) - if true, objects in group knock out each other (not fully implemented yet)
    /// </summary>
    public bool Knockout { get; set; } = false;
}