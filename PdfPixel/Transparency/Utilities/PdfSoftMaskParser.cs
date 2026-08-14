using PdfPixel.Color.Transform;
using PdfPixel.Forms;
using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Transparency.Utilities;

internal static class PdfSoftMaskParser
{
    public static PdfSoftMask? ParseSoftMaskDictionary(PdfDictionary? softMaskDict, IPdfPageInternal page)
    {
        if (softMaskDict == null)
        {
            return null;
        }

        PdfSoftMask softMask = new();
        softMask.Subtype = softMaskDict.GetNameOrDefault(PdfTokens.SoftMaskSubtypeKey).AsEnum<PdfSoftMaskSubtype>();

        PdfObject? groupObject = softMaskDict.GetObject(PdfTokens.SoftMaskGroupKey);
        if (groupObject == null)
        {
            return null;
        }

        PdfForm formObject = PdfForm.FromXObject(groupObject, page);
        softMask.MaskForm = formObject;

        PdfArray? bcArray = softMaskDict.GetArray(PdfTokens.SoftMaskBCKey);
        if (bcArray?.Count > 0)
        {
            softMask.BackgroundColorComponents = bcArray.GetFloatArray();
        }

        // Parse optional TR transfer function
        PdfObject? trObject = softMaskDict.GetObject(PdfTokens.TransferFunctionKey);
        if (trObject != null)
        {
            softMask.TransferFunction = TransferFunctionTransform.FromPdfObject(trObject);
        }

        return softMask;
    }

    public static PdfTransparencyGroup? ParseTransparencyGroup(PdfDictionary? groupDict, IPdfPageInternal page)
    {
        if (groupDict == null)
        {
            return null;
        }

        PdfTransparencyGroup group = new();
        if (groupDict.GetName(PdfTokens.GroupSubtypeKey) != PdfTokens.TransparencyGroupValue)
        {
            return null;
        }

        PdfObject? csValue = groupDict.GetObject(PdfTokens.GroupColorSpaceKey);
        group.ColorSpaceConverter = page.Cache.ColorSpace.ResolveByObject(csValue);
        group.Isolated = groupDict.GetBooleanOrDefault(PdfTokens.GroupIsolatedKey);
        group.Knockout = groupDict.GetBooleanOrDefault(PdfTokens.GroupKnockoutKey);
        return group;
    }
}
