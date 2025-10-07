using PdfReader.Models;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Wrapper page that overlays Form XObject resources with fallback to the original page resources.
    /// Geometry (MediaBox, CropBox, Rotation) is inherited unchanged from the original page.
    /// </summary>
    internal class FormXObjectPageWrapper : PdfPage
    {
        private readonly PdfObject _resourcePageObject;
        private readonly PdfDictionary _resourceDictionary;

        public FormXObjectPageWrapper(PdfPage originalPage, PdfObject formXObject)
            : base(originalPage.PageNumber,
                   originalPage.Document,
                   originalPage.PageObject,
                   originalPage.MediaBox,
                   originalPage.CropBox,
                   originalPage.Rotation,
                   originalPage.ResourceDictionary)
        {
            (_resourcePageObject, _resourceDictionary) = CreateResourcePageObject(originalPage, formXObject);
        }

        public override PdfObject PageObject => _resourcePageObject;
        public override PdfDictionary ResourceDictionary => _resourceDictionary;

        private (PdfObject, PdfDictionary) CreateResourcePageObject(PdfPage originalPage, PdfObject formXObject)
        {
            var syntheticDictionary = new PdfDictionary(originalPage.Document);
            var syntheticPageObject = new PdfObject(new PdfReference(-1, 0), originalPage.Document, PdfValue.Dictionary(syntheticDictionary));

            var formResourcesDict = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
            var pageResourcesDict = originalPage.PageObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);

            var mergedResourcesDict = CreateMergedResourcesDictionary(formResourcesDict, pageResourcesDict, originalPage.Document);
            if (mergedResourcesDict != null)
            {
                syntheticDictionary.Set(PdfTokens.ResourcesKey, PdfValue.Dictionary(mergedResourcesDict));
            }

            return (syntheticPageObject, mergedResourcesDict);
        }

        private PdfDictionary CreateMergedResourcesDictionary(PdfDictionary formResourcesDict,
                                                              PdfDictionary pageResourcesDict,
                                                              PdfDocument document)
        {
            if (formResourcesDict == null && pageResourcesDict == null)
            {
                return null;
            }

            var merged = new PdfDictionary(document);

            if (pageResourcesDict != null)
            {
                foreach (var kv in pageResourcesDict.RawValues)
                {
                    merged.RawValues[kv.Key] = kv.Value;
                }
            }

            if (formResourcesDict == null)
            {
                return merged;
            }

            foreach (var kv in formResourcesDict.RawValues)
            {
                var key = kv.Key;
                if (key == PdfTokens.ProcSetKey)
                {
                    var formProcSet = formResourcesDict.GetArray(PdfTokens.ProcSetKey);
                    if (formProcSet != null && formProcSet.Count > 0)
                    {
                        merged.Set(PdfTokens.ProcSetKey, PdfValue.Array(formProcSet));
                    }
                    else
                    {
                        var pageProcSet = pageResourcesDict?.GetArray(PdfTokens.ProcSetKey);
                        if (pageProcSet != null)
                        {
                            merged.Set(PdfTokens.ProcSetKey, PdfValue.Array(pageProcSet));
                        }
                    }
                    continue;
                }

                var formSub = formResourcesDict.GetDictionary(key);
                var pageSub = pageResourcesDict?.GetDictionary(key);
                if (formSub != null || pageSub != null)
                {
                    var innerMerged = MergeResourceDictionaries(formSub, pageSub, document);
                    merged.Set(key, PdfValue.Dictionary(innerMerged));
                }
                else
                {
                    merged.RawValues[key] = kv.Value;
                }
            }

            return merged;
        }

        private PdfDictionary MergeResourceDictionaries(PdfDictionary primary, PdfDictionary fallback, PdfDocument document)
        {
            var merged = new PdfDictionary(document);
            if (fallback != null)
            {
                foreach (var item in fallback.RawValues)
                {
                    merged.RawValues[item.Key] = item.Value;
                }
            }
            if (primary != null)
            {
                foreach (var item in primary.RawValues)
                {
                    merged.RawValues[item.Key] = item.Value;
                }
            }
            return merged;
        }
    }
}