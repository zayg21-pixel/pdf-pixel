using PdfReader.Color.ColorSpace;
using PdfReader.Forms;
using PdfReader.Models;
using PdfReader.Rendering.Operators;
using PdfReader.Text;
using PdfReader.Transparency.Model;

namespace PdfReader.Transparency.Utilities;

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
        var csValue = groupDict.GetValue(PdfTokens.GroupColorSpaceKey);
        group.ColorSpaceConverter = page.Cache.ColorSpace.ResolveByValue(csValue);
        group.Isolated = groupDict.GetBooleanOrDefault(PdfTokens.GroupIsolatedKey);
        group.Knockout = groupDict.GetBooleanOrDefault(PdfTokens.GroupKnockoutKey);
        return group;
    }
}
