using PdfPixel.Models;
using System;
using PdfPixel.Text;
using PdfPixel.Transparency.Utilities;
using PdfPixel.Transparency.Model;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Geometry;

namespace PdfPixel.Rendering.State
{
    /// <summary>
    /// Parses and applies PDF graphics state parameters from ExtGState dictionaries.
    /// Responsible for parsing PDF graphics state dictionaries and producing <see cref="PdfGraphicsStateParameters"/> instances
    /// which can then be applied to a target <see cref="PdfGraphicsState"/>.
    /// </summary>
    public static class PdfGraphicsStateParser
    {
        /// <summary>
        /// Parse graphics state parameters into a parameter container without mutating the target state.
        /// </summary>
        /// <param name="gsDict">Graphics state dictionary.</param>
        /// <param name="page">Owning page (used for soft mask parsing).</param>
        /// <returns>Populated parameters container.</returns>
        internal static PdfGraphicsStateParameters ParseGraphicsStateParametersFromDictionary(PdfDictionary gsDict, IPdfPageInternal page)
        {
            PdfGraphicsStateParameters parameters = new();
            if (gsDict == null)
            {
                return parameters;
            }

            parameters.AlphaIsShape = gsDict.GetBoolean(PdfTokens.AlphaIsShapeKey);
            parameters.LineWidth = gsDict.GetFloat(PdfTokens.LineWidthKey);
            parameters.MiterLimit = gsDict.GetFloat(PdfTokens.MiterLimitKey);

            float? capStyle = gsDict.GetFloat(PdfTokens.LineCapKey);
            if (capStyle.HasValue)
            {
                parameters.LineCap = capStyle.Value switch
                {
                    0 => PdfStrokeCap.Butt,
                    1 => PdfStrokeCap.Round,
                    2 => PdfStrokeCap.Square,
                    _ => PdfStrokeCap.Butt
                };
            }

            float? joinStyle = gsDict.GetFloat(PdfTokens.LineJoinKey);
            if (joinStyle.HasValue)
            {
                parameters.LineJoin = joinStyle.Value switch
                {
                    0 => PdfStrokeJoin.Miter,
                    1 => PdfStrokeJoin.Round,
                    2 => PdfStrokeJoin.Bevel,
                    _ => PdfStrokeJoin.Miter
                };
            }

            PdfArray? dashArray = gsDict.GetArray(PdfTokens.DashPatternKey);
            if (dashArray?.Count >= 2)
            {
                float[]? patternArray = dashArray.GetArray(0)?.GetFloatArray();
                float phase = dashArray.GetFloatOrDefault(1);

                if (patternArray?.Length > 0)
                {
                    parameters.DashPattern = patternArray;
                    parameters.DashPhase = phase;
                }
                else
                {
                    // Empty array means solid line
                    parameters.DashPattern = null;
                    parameters.DashPhase = 0f;
                }
            }

            float? strokeAlpha = gsDict.GetFloat(PdfTokens.StrokeAlphaKey); // Stroke alpha (/CA)
            if (strokeAlpha.HasValue)
            {
                // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
                parameters.StrokeAlpha = Math.Max(0f, Math.Min(1f, strokeAlpha.Value));
            }

            float? fillAlpha = gsDict.GetFloat(PdfTokens.FillAlphaKey); // Fill alpha (/ca)
            if (fillAlpha.HasValue)
            {
                // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
                parameters.FillAlpha = Math.Max(0f, Math.Min(1f, fillAlpha.Value));
            }

            if (gsDict.HasKey(PdfTokens.BlendModeKey))
            {
                var mode = PdfBlendMode.Unknown;

                // First try to get as name
                PdfString blendModeName = gsDict.GetName(PdfTokens.BlendModeKey);
                if (!blendModeName.IsEmpty)
                {
                    mode = blendModeName.AsEnum<PdfBlendMode>();
                }
                else
                {
                    // Handle blend mode arrays - PDF viewers should use the first supported blend mode
                    PdfArray? blendModeArray = gsDict.GetArray(PdfTokens.BlendModeKey);
                    if (blendModeArray != null)
                    {
                        for (int index = 0; index < blendModeArray.Count; index++)
                        {
                            PdfBlendMode candidate = blendModeArray.GetName(index).AsEnum<PdfBlendMode>();
                            if (candidate != PdfBlendMode.Unknown)
                            {
                                mode = candidate;
                                break;
                            }
                        }
                    }
                }

                // An unrecognized blend mode name falls back to Normal, per spec, rather than being ignored.
                parameters.BlendMode = (mode == PdfBlendMode.Unknown) ? PdfBlendMode.Normal : mode;
            }

            // Custom transformation matrix (/Matrix, or /CTM as an alternative key name)
            PdfArray? matrixArray = gsDict.GetArray(PdfTokens.MatrixKey) ?? gsDict.GetArray(PdfTokens.CTMKey);
            parameters.TransformMatrix = PdfMatrix.FromArray(matrixArray);

            // Soft Mask (/SMask)
            if (gsDict.HasKey(PdfTokens.SoftMaskKey))
            {
                PdfString maskName = gsDict.GetName(PdfTokens.SoftMaskKey);
                if (maskName == PdfTokens.NoneValue)
                {
                    parameters.ShouldUnsetSoftMask = true;
                }
                else
                {
                    PdfDictionary? softMaskDict = gsDict.GetDictionary(PdfTokens.SoftMaskKey);
                    parameters.SoftMask = PdfSoftMaskParser.ParseSoftMaskDictionary(softMaskDict, page);
                }
            }

            // Transfer Function (/TR2 takes priority over /TR)
            if (gsDict.HasKey(PdfTokens.TransferFunction2Key))
            {
                ParseTransferFunction(gsDict, PdfTokens.TransferFunction2Key, PdfTokens.DefaultValue, parameters);
            }
            else if (gsDict.HasKey(PdfTokens.TransferFunctionKey))
            {
                ParseTransferFunction(gsDict, PdfTokens.TransferFunctionKey, PdfTokens.IdentityKey, parameters);
            }

            parameters.Knockout = gsDict.GetBoolean(PdfTokens.KnockoutKey);           // Knockout (/TK)
            parameters.OverprintMode = gsDict.GetInteger(PdfTokens.OverprintModeKey); // Overprint Mode (/OPM)
            parameters.OverprintStroke = gsDict.GetBoolean(PdfTokens.OverprintStrokeKey); // Overprint Stroke (/OP)
            parameters.OverprintFill = gsDict.GetBoolean(PdfTokens.OverprintFillKey);     // Overprint Fill (/op)

            // Font (/Font)
            PdfArray? fontArray = gsDict.GetArray(PdfTokens.FontKey);
            if (fontArray?.Count == 2)
            {
                PdfObject? fontObject = fontArray.GetObject(0);
                float fontSize = fontArray.GetFloatOrDefault(1);

                parameters.Font = page.Cache.GetFont(fontObject);
                parameters.FontSize = fontSize;
            }

            // Rendering intent (/RI)
            if (gsDict.HasKey(PdfTokens.StateIntentKey))
            {
                PdfString intentName = gsDict.GetName(PdfTokens.StateIntentKey);
                parameters.PdfRenderingIntent = intentName.AsEnum<PdfRenderingIntent>();
            }

            return parameters;
        }

        /// <summary>
        /// Parses a /TR or /TR2 entry. The unset name (/Identity for TR, /Default for TR2) clears any inherited transfer function.
        /// </summary>
        private static void ParseTransferFunction(PdfDictionary gsDict, in PdfString key, in PdfString unsetValue, PdfGraphicsStateParameters parameters)
        {
            PdfString name = gsDict.GetName(key);
            if (name == unsetValue)
            {
                parameters.ShouldUnsetTransferFunction = true;
                return;
            }

            PdfObject? transferFunctionObject = gsDict.GetObject(key);
            parameters.TransferFunction = TransferFunctionTransform.FromPdfObject(transferFunctionObject);
        }
    }
}
