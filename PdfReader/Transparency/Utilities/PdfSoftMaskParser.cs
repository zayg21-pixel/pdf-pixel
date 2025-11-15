using PdfReader.Color.ColorSpace;
using PdfReader.Models;
using PdfReader.Rendering.Operators;
using PdfReader.Text;
using PdfReader.Transparency.Model;
using SkiaSharp;

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
        softMask.GroupObject = softMaskDict.GetObject(PdfTokens.SoftMaskGroupKey);
        if (softMask.GroupObject == null)
        {
            return null;
        }
        var formDict = softMask.GroupObject.Dictionary;
        var matrixArray = formDict.GetArray(PdfTokens.MatrixKey);
        if (matrixArray != null)
        {
            softMask.FormMatrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
        }
        var bboxArray = formDict.GetArray(PdfTokens.BBoxKey);
        if (bboxArray != null && bboxArray.Count >= 4)
        {
            var left = bboxArray.GetFloat(0);
            var bottom = bboxArray.GetFloat(1);
            var right = bboxArray.GetFloat(2);
            var top = bboxArray.GetFloat(3);
            softMask.BBox = new SKRect(left, bottom, right, top).Standardized;
            softMask.TransformedBounds = softMask.FormMatrix.MapRect(softMask.BBox);
        }
        softMask.ResourcesDictionary = formDict.GetDictionary(PdfTokens.ResourcesKey);
        var groupDict = formDict.GetDictionary(PdfTokens.GroupKey);
        if (groupDict != null)
        {
            softMask.TransparencyGroup = ParseTransparencyGroup(groupDict, page);
        }
        var bcArray = softMaskDict.GetArray(PdfTokens.SoftMaskBCKey);
        if (bcArray != null && bcArray.Count > 0)
        {
            var groupCsDict = formDict.GetDictionary(PdfTokens.GroupKey);
            var csVal = groupCsDict?.GetValue(PdfTokens.GroupColorSpaceKey);
            var converter = page.Cache.ColorSpace.ResolveByValue(csVal, 1);
            var comps = bcArray.GetFloatArray();
            softMask.BackgroundColor = converter.ToSrgb(comps, PdfRenderingIntent.RelativeColorimetric);
        }
        if (softMaskDict.HasKey(PdfTokens.SoftMaskTRKey))
        {
            softMask.TransferFunction = softMaskDict.GetValue(PdfTokens.SoftMaskTRKey);
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
