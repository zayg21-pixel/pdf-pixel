using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Forms;
using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Transparency.Utilities;

internal static class PdfSoftMaskParser
{
    public static PdfSoftMask? ParseSoftMaskDictionary(PdfDictionary? softMaskDict)
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

        PdfForm? formObject = PdfForm.FromXObject(groupObject);
        if (formObject == null)
        {
            return null;
        }

        softMask.MaskForm = formObject;

        PdfArray? bcArray = softMaskDict.GetArray(PdfTokens.SoftMaskBCKey);
        if (bcArray?.Count > 0)
        {
            softMask.BackgroundColorComponents = bcArray.GetFloatArray();
        }

        softMask.TransferFunction = TransferFunctionTransform.GetTransferFunction(softMaskDict, PdfTokens.TransferFunctionKey);

        return softMask;
    }
}
