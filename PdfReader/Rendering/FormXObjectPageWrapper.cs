using PdfReader.Models;
namespace PdfReader.Rendering
{
    /// <summary>
    /// Wrapper class that provides Form XObject resource resolution with fallback to page resources
    /// This implements the PDF specification requirement that Form XObjects can have their own Resources
    /// </summary>
    internal class FormXObjectPageWrapper : PdfPage
    {
        private readonly PdfObject _resourcePageObject;
        private readonly PdfDictionary _resourceDictionary;

        public FormXObjectPageWrapper(PdfPage originalPage, PdfObject formXObject) 
            : base(originalPage.PageNumber, originalPage.Document, originalPage.PageObject)
        {
            // Create a synthetic page object that prioritizes Form XObject resources
            (_resourcePageObject, _resourceDictionary) = CreateResourcePageObject(originalPage, formXObject);
        }

        /// <summary>
        /// Override PageObject to return our synthetic resource-aware page object
        /// This ensures that GetXObjectFromResources and other resource lookups
        /// check Form XObject resources first
        /// </summary>
        public override PdfObject PageObject => _resourcePageObject;

        public override PdfDictionary ResourceDictionary => _resourceDictionary;

        /// <summary>
        /// Create a synthetic page object that merges Form XObject and page resources
        /// Form XObject resources take priority, with page resources as fallback
        /// This properly implements PDF specification resource inheritance
        /// </summary>
        private (PdfObject, PdfDictionary) CreateResourcePageObject(PdfPage originalPage, PdfObject formXObject)
        {
            // Create a new synthetic page object
            var syntheticDictionary = new PdfDictionary(originalPage.Document);
            var syntheticPageObject = new PdfObject(new PdfReference(-1, 0), originalPage.Document, PdfValue.Dictionary(syntheticDictionary));

            // Get both resource dictionaries
            var formResourcesDict = formXObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);
            var pageResourcesDict = originalPage.PageObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey);

            // Create merged resources dictionary
            var mergedResourcesDict = CreateMergedResourcesDictionary(formResourcesDict, pageResourcesDict, originalPage.Document);

            if (mergedResourcesDict != null)
            {
                syntheticDictionary.Set(PdfTokens.ResourcesKey, PdfValue.Dictionary(mergedResourcesDict));
            }

            return (syntheticPageObject, mergedResourcesDict);
        }

        /// <summary>
        /// Generic merge of resource dictionaries per PDF spec:
        /// - Start with page resources (fallback)
        /// - Overlay Form resources (priority)
        /// - For sub-dictionaries (Font, XObject, ColorSpace, Pattern, Shading, ExtGState, Properties, etc.)
        ///   merge inner entries so Form overrides and Page provides fallback.
        /// - For ProcSet arrays, prefer Form if present, otherwise use Page.
        /// </summary>
        private PdfDictionary CreateMergedResourcesDictionary(PdfDictionary formResourcesDict, 
                                                            PdfDictionary pageResourcesDict, 
                                                            PdfDocument document)
        {
            // If neither has resources, return null
            if (formResourcesDict == null && pageResourcesDict == null)
            {
                return null;
            }

            // Create new merged dictionary
            var merged = new PdfDictionary(document);

            // 1) Copy all page-level resources as fallback
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

            // 2) Overlay/merge form resources
            foreach (var kv in formResourcesDict.RawValues)
            {
                var key = kv.Key;

                // Special case: ProcSet arrays (combine or prefer form)
                if (key == PdfTokens.ProcSetKey)
                {
                    var formProcSet = formResourcesDict.GetArray(PdfTokens.ProcSetKey);
                    if (formProcSet != null && formProcSet.Count > 0)
                    {
                        merged.Set(PdfTokens.ProcSetKey, PdfValue.Array(formProcSet));
                    }
                    else
                    {
                        // keep page's ProcSet if form omitted
                        var pageProcSet = pageResourcesDict?.GetArray(PdfTokens.ProcSetKey);
                        if (pageProcSet != null)
                            merged.Set(PdfTokens.ProcSetKey, PdfValue.Array(pageProcSet));
                    }
                    continue;
                }

                // If this resource entry is a sub-dictionary in either form or page, merge inner dictionaries
                var formSub = formResourcesDict.GetDictionary(key);
                var pageSub = pageResourcesDict?.GetDictionary(key);
                if (formSub != null || pageSub != null)
                {
                    var innerMerged = MergeResourceDictionaries(formSub, pageSub, document);
                    merged.Set(key, PdfValue.Dictionary(innerMerged));
                }
                else
                {
                    // Scalar or array resource: form overrides page
                    merged.RawValues[key] = kv.Value;
                }
            }

            return merged;
        }

        /// <summary>
        /// Merge two resource sub-dictionaries, with primary (form) taking priority
        /// and fallback (page) providing defaults.
        /// </summary>
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