using PdfReader.Models;
using SkiaSharp;
using System;
using PdfReader.Rendering.Color;

namespace PdfReader.Rendering.State
{
    /// <summary>
    /// Parses and applies PDF graphics state parameters from ExtGState dictionaries
    /// Responsible for parsing PDF graphics state dictionaries and updating PdfGraphicsState objects
    /// </summary>
    public static class PdfGraphicsStateParser
    {
        /// <summary>
        /// Parse and apply graphics state parameters from a named graphics state dictionary
        /// Updates the graphics state object but does NOT apply canvas transformations
        /// </summary>
        /// <param name="gsName">Name of the graphics state in the page resources</param>
        /// <param name="graphicsState">Graphics state object to update</param>
        /// <param name="page">PDF page containing the resources</param>
        /// <param name="document">PDF document for reference resolution</param>
        /// <returns>Transformation matrix if one was specified, null otherwise</returns>
        public static SKMatrix? ParseGraphicsStateParameters(string gsName, PdfGraphicsState graphicsState, PdfPage page)
        {
            // Get the ExtGState dictionary from resources using smart resolution
            var extGStateDict = page.ResourceDictionary.GetDictionary(PdfTokens.ExtGStateKey);
            if (extGStateDict == null)
            {
                return null;
            }

            // Get the specific graphics state dictionary by name using smart resolution
            var gsDict = extGStateDict.GetDictionary(gsName);
            if (gsDict == null)
            {
                return null;
            }

            return ParseGraphicsStateFromDictionary(gsDict, graphicsState, page);
        }

        /// <summary>
        /// Parse graphics state parameters from a dictionary and update the graphics state
        /// </summary>
        /// <param name="gsDict">Graphics state dictionary</param>
        /// <param name="graphicsState">Graphics state object to update</param>
        /// <returns>Transformation matrix if one was specified, null otherwise</returns>
        public static SKMatrix? ParseGraphicsStateFromDictionary(PdfDictionary gsDict, PdfGraphicsState graphicsState, PdfPage page)
        {
            SKMatrix? transformMatrix = null;

            // Line width
            if (gsDict.HasKey(PdfTokens.LineWidthKey))
            {
                graphicsState.LineWidth = gsDict.GetFloatOrDefault(PdfTokens.LineWidthKey);
            }

            // Line cap style
            if (gsDict.HasKey(PdfTokens.LineCapKey))
            {
                var capStyle = gsDict.GetFloatOrDefault(PdfTokens.LineCapKey);

                graphicsState.LineCap = capStyle switch
                {
                    0 => SKStrokeCap.Butt,
                    1 => SKStrokeCap.Round,
                    2 => SKStrokeCap.Square,
                    _ => SKStrokeCap.Butt
                };
            }

            // Line join style
            if (gsDict.HasKey(PdfTokens.LineJoinKey))
            {
                var joinStyle = gsDict.GetFloatOrDefault(PdfTokens.LineJoinKey);
                graphicsState.LineJoin = joinStyle switch
                {
                    0 => SKStrokeJoin.Miter,
                    1 => SKStrokeJoin.Round,
                    2 => SKStrokeJoin.Bevel,
                    _ => SKStrokeJoin.Miter
                };
            }

            // Miter limit
            if (gsDict.HasKey(PdfTokens.MiterLimitKey))
            {
                graphicsState.MiterLimit = gsDict.GetFloatOrDefault(PdfTokens.MiterLimitKey);
            }

            // Dash pattern
            if (gsDict.HasKey(PdfTokens.DashPatternKey))
            {
                var dashArray = gsDict.GetArray(PdfTokens.DashPatternKey);
                if (dashArray != null && dashArray.Count >= 2)
                {
                    var patternArray = dashArray.GetArray(0).GetFloatArray();
                    var phase = dashArray.GetFloat(1);

                    if (patternArray != null && patternArray.Length > 0)
                    {
                        graphicsState.DashPattern = patternArray;
                        graphicsState.DashPhase = phase;
                    }
                    else
                    {
                        // Empty array means solid line
                        graphicsState.DashPattern = null;
                        graphicsState.DashPhase = 0;
                    }
                }
            }

            // Alpha constants (transparency)
            if (gsDict.HasKey(PdfTokens.StrokeAlphaKey)) // Stroke alpha (/CA)
            {
                var alpha = gsDict.GetFloatOrDefault(PdfTokens.StrokeAlphaKey);
                // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
                graphicsState.StrokeAlpha = Math.Max(0f, Math.Min(1f, alpha));
            }

            if (gsDict.HasKey(PdfTokens.FillAlphaKey))   // Fill alpha (/ca)
            {
                var alpha = gsDict.GetFloatOrDefault(PdfTokens.FillAlphaKey);
                // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
                graphicsState.FillAlpha = Math.Max(0f, Math.Min(1f, alpha));
            }

            // Blend mode (/BM)
            if (gsDict.HasKey(PdfTokens.BlendModeKey))
            {
                // First try to get as name
                var blendModeName = gsDict.GetName(PdfTokens.BlendModeKey);
                if (!string.IsNullOrEmpty(blendModeName))
                {
                    graphicsState.BlendMode = PdfBlendModeNames.ParseBlendMode(blendModeName);
                }
                else
                {
                    // Handle blend mode arrays - PDF viewers should use the first supported blend mode
                    var blendModeArray = gsDict.GetArray(PdfTokens.BlendModeKey);
                    if (blendModeArray != null && blendModeArray.Count > 0)
                    {
                        // Try each blend mode in the array until we find a supported one
                        for (int i = 0; i < blendModeArray.Count; i++)
                        {
                            var modeName = blendModeArray.GetName(i);

                            if (!string.IsNullOrEmpty(modeName))
                            {
                                var parsedMode = PdfBlendModeNames.ParseBlendMode(modeName);
                                // Use the first blend mode that we recognize (even if it's Normal)
                                if (parsedMode != PdfBlendMode.Normal || string.Equals(modeName, PdfBlendModeNames.Normal, StringComparison.OrdinalIgnoreCase))
                                {
                                    graphicsState.BlendMode = parsedMode;
                                    break; // Use the first recognized blend mode
                                }
                            }
                        }
                    }
                }
            }

            // Transformation matrix
            PdfArray matrixArray = null;
            if (gsDict.HasKey(PdfTokens.MatrixKey)) // Custom transformation matrix
            {
                matrixArray = gsDict.GetArray(PdfTokens.MatrixKey);
            }
            else if (gsDict.HasKey(PdfTokens.CTMKey)) // Alternative key name
            {
                matrixArray = gsDict.GetArray(PdfTokens.CTMKey);
            }

            transformMatrix = PdfMatrixUtilities.CreateMatrix(matrixArray);

            // Soft Mask (/SMask) - CRITICAL for shadow effects
            if (gsDict.HasKey(PdfTokens.SoftMaskKey))
            {
                var maskName = gsDict.GetName(PdfTokens.SoftMaskKey);
                if (string.Equals(maskName, PdfTokens.NoneValue, StringComparison.OrdinalIgnoreCase))
                {
                    // Remove soft mask
                    graphicsState.SoftMask = null;
                }
                else
                {
                    // Parse soft mask dictionary - try to get as dictionary directly
                    var softMaskDict = gsDict.GetDictionary(PdfTokens.SoftMaskKey);
                    var newSoftMask = ParseSoftMaskDictionary(softMaskDict, page);

                    if (newSoftMask != null)
                    {
                        graphicsState.SoftMask = newSoftMask;
                    }
                }
            }

            // Knockout (/TK)
            if (gsDict.HasKey(PdfTokens.KnockoutKey))
            {
                var knockoutName = gsDict.GetName(PdfTokens.KnockoutKey);
                graphicsState.Knockout = string.Equals(knockoutName, PdfTokens.TrueValue, StringComparison.OrdinalIgnoreCase);
            }

            // Overprint Mode (/OPM)
            if (gsDict.HasKey(PdfTokens.OverprintModeKey))
            {
                graphicsState.OverprintMode = gsDict.GetIntegerOrDefault(PdfTokens.OverprintModeKey);
            }

            // Overprint Stroke (/OP)
            if (gsDict.HasKey(PdfTokens.OverprintStrokeKey))
            {
                var overprintName = gsDict.GetName(PdfTokens.OverprintStrokeKey);
                graphicsState.OverprintStroke = string.Equals(overprintName, PdfTokens.TrueValue, StringComparison.OrdinalIgnoreCase);
            }

            // Overprint Fill (/op)
            if (gsDict.HasKey(PdfTokens.OverprintFillKey))
            {
                var overprintFillName = gsDict.GetName(PdfTokens.OverprintFillKey);
                graphicsState.OverprintFill = string.Equals(overprintFillName, PdfTokens.TrueValue, StringComparison.OrdinalIgnoreCase);
            }

            return transformMatrix;
        }

        /// <summary>
        /// Parse a soft mask dictionary
        /// </summary>
        public static PdfSoftMask ParseSoftMaskDictionary(PdfDictionary softMaskDict, PdfPage page)
        {
            if (softMaskDict == null)
            {
                return null;
            }

            var softMask = new PdfSoftMask();

            // Subtype (/S)
            var subtypeName = softMaskDict.GetName(PdfTokens.SoftMaskSubtypeKey);
            if (string.Equals(subtypeName, PdfTokens.AlphaSoftMask, StringComparison.OrdinalIgnoreCase))
            {
                softMask.Subtype = SoftMaskSubtype.Alpha;
            }
            else if (string.Equals(subtypeName, PdfTokens.LuminositySoftMask, StringComparison.OrdinalIgnoreCase))
            {
                softMask.Subtype = SoftMaskSubtype.Luminosity;
            }
            else
            {
                softMask.Subtype = SoftMaskSubtype.Unknown;
            }

            // Group (/G) mandatory
            softMask.GroupObject = softMaskDict.GetPageObject(PdfTokens.SoftMaskGroupKey);
            if (softMask.GroupObject == null)
            {
                return null;
            }

            var formDict = softMask.GroupObject.Dictionary;

            // /Matrix (optional) -> if present replace default identity
            var matrixArray = formDict.GetArray(PdfTokens.MatrixKey);
            if (matrixArray != null)
            {
                softMask.FormMatrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
            }

            // /BBox
            var bboxArray = formDict.GetArray(PdfTokens.BBoxKey);
            if (bboxArray != null && bboxArray.Count >= 4)
            {
                var left = bboxArray.GetFloat(0);
                var bottom = bboxArray.GetFloat(1);
                var right = bboxArray.GetFloat(2);
                var top = bboxArray.GetFloat(3);

                softMask.BBox = new SKRect(left, bottom, right, top);

                // Use SKMatrix.MapRect returning a new rectangle
                softMask.TransformedBounds = softMask.FormMatrix.MapRect(softMask.BBox);
            }

            // /Resources
            softMask.ResourcesDictionary = formDict.GetDictionary(PdfTokens.ResourcesKey);

            // /Group (transparency group inside mask form)
            var groupDict = formDict.GetDictionary(PdfTokens.GroupKey);
            if (groupDict != null)
            {
                softMask.TransparencyGroup = ParseTransparencyGroup(groupDict, page);
            }

            // /BC background color
            var bcArray = softMaskDict.GetArray(PdfTokens.SoftMaskBCKey);
            if (bcArray != null && bcArray.Count > 0)
            {
                var groupCsDict = formDict.GetDictionary(PdfTokens.GroupKey);
                var csVal = groupCsDict?.GetValue(PdfTokens.GroupColorSpaceKey);
                var converter = csVal != null ? PdfColorSpaces.ResolveByValue(csVal, page) : DeviceGrayConverter.Instance;
                var comps = bcArray.GetFloatArray();
                softMask.BackgroundColor = converter.ToSrgb(comps, PdfRenderingIntent.RelativeColorimetric);
            }

            // /TR transfer function
            if (softMaskDict.HasKey(PdfTokens.SoftMaskTRKey))
            {
                softMask.TransferFunction = softMaskDict.GetValue(PdfTokens.SoftMaskTRKey);
            }

            return softMask;
        }

        /// <summary>
        /// Parse a transparency group dictionary from a Form XObject.
        /// Resolves /CS to a color space converter immediately (defaults to DeviceRGB when absent).
        /// </summary>
        public static PdfTransparencyGroup ParseTransparencyGroup(PdfDictionary groupDict, PdfPage page)
        {
            if (groupDict == null)
            {
                return null;
            }

            var group = new PdfTransparencyGroup();

            // Subtype (/S) - should be /Transparency
            group.Subtype = groupDict.GetName(PdfTokens.GroupSubtypeKey);
            if (!group.IsTransparencyGroup)
            {
                return group; // Non-transparency group; leave defaults
            }

            // Color Space (/CS) - may be name or array (ICCBased, etc.)
            var csValue = groupDict.GetValue(PdfTokens.GroupColorSpaceKey);
            group.ColorSpaceConverter = PdfColorSpaces.ResolveByValue(csValue, page);

            // Boolean flags (/I) and (/K) allow multiple representations; leverage GetBool
            group.Isolated = groupDict.GetBoolOrDefault(PdfTokens.GroupIsolatedKey);
            group.Knockout = groupDict.GetBoolOrDefault(PdfTokens.GroupKnockoutKey);

            return group;
        }
    }
}