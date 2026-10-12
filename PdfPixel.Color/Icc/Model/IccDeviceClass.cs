namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// ICC profile/device class signatures (4CC codes) used in profile headers.
/// </summary>
public enum IccDeviceClass
{
    /// <summary>
    /// Unrecognized or unspecified device class.
    /// </summary>
    Unknown,

    /// <summary>
    /// Input device profile ('scnr').
    /// </summary>
    Input,

    /// <summary>
    /// Display device profile ('mntr').
    /// </summary>
    Display,

    /// <summary>
    /// Output device profile ('prtr').
    /// </summary>
    Output,

    /// <summary>
    /// DeviceLink profile ('link').
    /// </summary>
    DeviceLink,

    /// <summary>
    /// ColorSpace profile ('spac').
    /// </summary>
    ColorSpace,

    /// <summary>
    /// Abstract profile ('abst').
    /// </summary>
    Abstract,

    /// <summary>
    /// NamedColor profile ('nmcl').
    /// </summary>
    NamedColor
}
