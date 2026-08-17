using PdfPixel.Color.ColorSpace;
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

        PdfObject? trObject = softMaskDict.GetObject(PdfTokens.TransferFunctionKey);
        if (trObject != null)
        {
            softMask.TransferFunction = TransferFunctionTransform.FromPdfObject(trObject);
        }

        return softMask;
    }

    /// <summary>
    /// Parses the transparency group <paramref name="ownerDictionary"/> holds under <paramref name="key"/>.
    /// </summary>
    public static PdfTransparencyGroup? ParseTransparencyGroup(PdfDictionary? ownerDictionary, in PdfString key, IPdfPageInternal page)
    {
        if (ownerDictionary == null)
        {
            return null;
        }

        PdfReference? groupReference = ownerDictionary.GetReference(key);

        if (groupReference != null && page.Document.ObjectCache.TransparencyGroups.TryGetValue(groupReference.Value, out PdfTransparencyGroup? documentCachedGroup))
        {
            return documentCachedGroup;
        }

        PdfDictionary? groupDictionary = ownerDictionary.GetDictionary(key);

        if (groupDictionary == null)
        {
            return null;
        }

        if (groupDictionary.GetName(PdfTokens.GroupSubtypeKey) != PdfTokens.TransparencyGroupValue)
        {
            return null;
        }

        PdfColorSpaceReference colorSpaceReference = PdfColorSpaceReference.FromDictionary(groupDictionary, PdfTokens.GroupColorSpaceKey);

        PdfTransparencyGroup group = new()
        {
            ColorSpaceConverter = page.Cache.ColorSpace.Resolve(colorSpaceReference),
            Isolated = groupDictionary.GetBooleanOrDefault(PdfTokens.GroupIsolatedKey),
            Knockout = groupDictionary.GetBooleanOrDefault(PdfTokens.GroupKnockoutKey)
        };

        if (groupReference != null && colorSpaceReference.Reference.IsValid)
        {
            page.Document.ObjectCache.TransparencyGroups[groupReference.Value] = group;
        }

        return group;
    }
}
