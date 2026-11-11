using PdfPixel.Color.ColorSpace;
using PdfPixel.Transparency;
using System;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// The group a layer composites as, resolved against the layer it opens in: its blending color space, whether it is
/// isolated and whether it is knockout (ISO 32000-2 11.4.5, 11.4.6, 11.7.2).
/// </summary>
internal sealed class SnapLayerGroup
{
    private SnapLayerGroup(PdfColorSpaceConverter colorSpace, bool isolated, bool knockout)
    {
        ColorSpace = colorSpace;
        Isolated = isolated;
        Knockout = knockout;
    }

    /// <summary>
    /// Gets the blending color space the layer composites in.
    /// </summary>
    public PdfColorSpaceConverter ColorSpace { get; }

    /// <summary>
    /// Gets whether the group is isolated: declared so, or opened without a parent.
    /// </summary>
    public bool Isolated { get; }

    /// <summary>
    /// Gets whether the group is knockout.
    /// </summary>
    public bool Knockout { get; }

    /// <summary>
    /// Resolves <paramref name="group"/> opened in <paramref name="parent"/>, or as a root without one.
    /// </summary>
    public static SnapLayerGroup Resolve(PdfTransparencyGroupContext group, SnapLayer? parent)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        bool isolated = group.Isolated || parent == null;

        return new SnapLayerGroup(ResolveColorSpace(group, parent, isolated), isolated, group.Knockout);
    }

    /// <summary>
    /// Returns the color space a group composites in (11.7.2): a non-isolated group its parent's; an isolated group its
    /// declared blending space, a device space replaced by the nearest CIE-based ancestor's of as many colorants;
    /// otherwise its parent's, and DeviceRGB at the root.
    /// </summary>
    private static PdfColorSpaceConverter ResolveColorSpace(PdfTransparencyGroupContext group, SnapLayer? parent, bool isolated)
    {
        PdfColorSpaceConverter? declaredColorSpace = null;

        if (group.ColorSpace != null)
        {
            declaredColorSpace = group.ColorSpace.GetBlendingColorSpace();
        }

        if (parent != null && !isolated)
        {
            return parent.ColorSpace;
        }
        else if (declaredColorSpace != null)
        {
            PdfColorSpaceConverter? ancestorColorSpace = null;

            if (declaredColorSpace.IsDevice)
            {
                ancestorColorSpace = GetNearestCieColorSpace(parent, declaredColorSpace.Components);
            }

            return ancestorColorSpace ?? declaredColorSpace;
        }
        else if (parent != null)
        {
            return parent.ColorSpace;
        }
        else
        {
            return PdfDeviceRgbColorSpaceConverter.Instance;
        }
    }

    /// <summary>
    /// Returns the nearest CIE-based blending color space of <paramref name="componentCount"/> colorants among
    /// <paramref name="layer"/> and its ancestors, or <see langword="null"/> when none is.
    /// </summary>
    private static PdfColorSpaceConverter? GetNearestCieColorSpace(SnapLayer? layer, int componentCount)
    {
        for (SnapLayer? ancestor = layer; ancestor != null; ancestor = ancestor.Parent)
        {
            if (!ancestor.ColorSpace.IsDevice && ancestor.ColorSpace.Components == componentCount)
            {
                return ancestor.ColorSpace;
            }
        }

        return null;
    }
}
