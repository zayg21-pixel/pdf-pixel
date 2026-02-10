using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Forms;
using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Transparency.Utilities;

internal class PdfSoftMaskParser
{
    public static PdfSoftMask ParseSoftMaskDictionary(PdfDictionary softMaskDict, PdfPage page)
    {
        if (softMaskDict == null)
        {
            return null;
        }

        var softMask = new PdfSoftMask();
        softMask.Subtype = softMaskDict.GetName(PdfTokens.SoftMaskSubtypeKey).AsEnum<PdfSoftMaskSubtype>();

        var groupObject = softMaskDict.GetObject(PdfTokens.SoftMaskGroupKey);
        if (groupObject == null)
        {
            return null;
        }

        var formObject = PdfForm.FromXObject(groupObject, page);
        softMask.MaskForm = formObject;

        var bcArray = softMaskDict.GetArray(PdfTokens.SoftMaskBCKey);
        if (bcArray != null && bcArray.Count > 0)
        {
            softMask.BackgroundColor = bcArray.GetFloatArray();
        }

        // Parse optional TR transfer function
        var trObject = softMaskDict.GetObject(PdfTokens.TransferFunctionKey);
        if (trObject != null)
        {
            softMask.TransferFunction = TransferFunctionTransform.FromPdfObject(trObject);
        }

        return softMask;
    }

    public static PdfTransparencyGroup ParseTransparencyGroup(PdfDictionary groupDict, PdfPage page)
    {
        if (groupDict == null)
        {
            return null;
        }

        var group = new PdfTransparencyGroup();
        var subtype = groupDict.GetName(PdfTokens.GroupSubtypeKey);
        if (subtype != PdfTokens.TransparencyGroupValue)
        {
            return null;
        }

        var csValue = groupDict.GetObject(PdfTokens.GroupColorSpaceKey);
        group.ColorSpaceConverter = page.Cache.ColorSpace.ResolveByObject(csValue);
        group.Isolated = groupDict.GetBooleanOrDefault(PdfTokens.GroupIsolatedKey);
        group.Knockout = groupDict.GetBooleanOrDefault(PdfTokens.GroupKnockoutKey);
        return group;
    }
}
