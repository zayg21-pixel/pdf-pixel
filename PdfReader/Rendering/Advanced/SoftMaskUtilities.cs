using PdfReader.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.Advanced
{
    /// <summary>
    /// Utility functions for soft mask processing.
    /// Provides helpers to build temporary graphics states and color filters used when interpreting /SMask dictionaries.
    /// </summary>
    internal static class SoftMaskUtilities
    {
        /// <summary>
        /// Create a graphics state optimized for alpha soft mask rendering (Subtype = /Alpha).
        /// We render the mask content in solid white so that the resulting luminance (or direct alpha composition)
        /// produces maximum coverage for painted marks and the per‑object alpha comes only from transparency operators
        /// (e.g. ca/CA) or explicit painting. Using white ensures that stroke/fill operations that do not explicitly
        /// change color contribute a full 1.0 channel and the eventual mask derives only from transparency semantics.
        /// </summary>
        public static PdfGraphicsState CreateAlphaMaskGraphicsState()
        {
            return new PdfGraphicsState
            {
                // White stroke/fill -> maximum channel; alpha modulation derives from transparency settings.
                StrokePaint = PdfPaint.Solid(SKColors.White),
                FillPaint = PdfPaint.Solid(SKColors.White),
                StrokeAlpha = 1.0f,
                FillAlpha = 1.0f,
                BlendMode = PdfBlendMode.Normal,
                LineWidth = 1.0f
            };
        }

        /// <summary>
        /// Create a graphics state optimized for luminosity soft mask rendering (Subtype = /Luminosity).
        /// For luminosity masks we keep natural grayscale intent by rendering with black (or dark) base color so that
        /// the mask result comes from actual painted content luminance (after optional color space conversions) rather
        /// than being forced to pure white. This aligns with the PDF spec where a luminosity soft mask derives its
        /// values from the luminance of the group result. Black base simplifies interpretation and avoids unintended
        /// bias toward full alpha when colors are not explicitly set.
        /// </summary>
        public static PdfGraphicsState CreateLuminosityMaskGraphicsState()
        {
            return new PdfGraphicsState
            {
                // Black stroke/fill -> preserves true luminance contribution of painted colors.
                StrokePaint = PdfPaint.Solid(SKColors.Black),
                FillPaint = PdfPaint.Solid(SKColors.Black),
                StrokeAlpha = 1.0f,
                FillAlpha = 1.0f,
                BlendMode = PdfBlendMode.Normal,
                LineWidth = 1.0f
            };
        }

        /// <summary>
        /// Create a color filter that converts RGB luminance into the alpha channel (keeps RGB unchanged).
        /// Used when constructing alpha from luminosity sources.
        /// </summary>
        public static SKColorFilter CreateAlphaFromLuminosityFilter()
        {
            var matrix = new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0.299f, 0.587f, 0.114f, 0, 0
            };
            return SKColorFilter.CreateColorMatrix(matrix);
        }

        /// <summary>
        /// Create a color filter that maps decoded Gray/RGB components into alpha according to /Decode array.
        /// - For Gray: alpha = d0 + Gray * (d1 - d0)
        /// - For RGB:  alpha = 0.299*R' + 0.587*G' + 0.114*B' where C' = d0c + C*(d1c - d0c)
        /// RGB/Gray channels are preserved; only alpha is replaced.
        /// Returns null if decode is missing or unsupported format.
        /// </summary>
        public static SKColorFilter CreateAlphaFromDecode(IReadOnlyList<float> decode, int components)
        {
            if (decode == null || decode.Count < 2)
            {
                return null;
            }

            if (components == 1 && decode.Count >= 2)
            {
                float d0 = decode[0];
                float d1 = decode[1];
                float scale = d1 - d0;
                float offset = d0;
                var mat = new float[]
                {
                    1, 0, 0, 0, 0,
                    0, 1, 0, 0, 0,
                    0, 0, 1, 0, 0,
                    scale, 0, 0, 0, offset
                };
                return SKColorFilter.CreateColorMatrix(mat);
            }

            if (components == 3 && decode.Count >= 6)
            {
                float d0R = decode[0], d1R = decode[1]; float sR = d1R - d0R; float oR = d0R;
                float d0G = decode[2], d1G = decode[3]; float sG = d1G - d0G; float oG = d0G;
                float d0B = decode[4], d1B = decode[5]; float sB = d1B - d0B; float oB = d0B;
                const float Lr = 0.299f, Lg = 0.587f, Lb = 0.114f;
                var mat = new float[]
                {
                    1, 0, 0, 0, 0,
                    0, 1, 0, 0, 0,
                    0, 0, 1, 0, 0,
                    Lr*sR, Lg*sG, Lb*sB, 0, Lr*oR + Lg*oG + Lb*oB
                };
                return SKColorFilter.CreateColorMatrix(mat);
            }

            return null;
        }

        /// <summary>
        /// Create a color filter from soft mask transfer function (/TR). Minimal support:
        /// - Name /Identity: no-op
        /// - Array of 256 samples [0..1] or [0..255]: lookup applied to alpha channel only
        /// </summary>
        public static SKColorFilter CreateTransferFunctionColorFilter(PdfSoftMask softMask)
        {
            if (softMask?.TransferFunction == null)
            {
                return null;
            }

            try
            {
                var tr = softMask.TransferFunction;
                if (tr.Type == PdfValueType.Name)
                {
                    var name = tr.AsName();
                    if (!string.IsNullOrEmpty(name) && name.IndexOf("Identity", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return null; // identity
                    }

                    return null;
                }

                if (tr.Type == PdfValueType.Array)
                {
                    var arr = tr.AsArray().GetFloatArray();
                    if (arr == null || arr.Length == 0)
                    {
                        return null;
                    }

                    var alpha = new byte[256];
                    if (arr.Length == 256)
                    {
                        for (int i = 0; i < 256; i++)
                        {
                            float f = arr[i];

                            if (f <= 1f)
                            {
                                f *= 255f; // assume [0..1]
                            }
                            if (f < 0f) { f = 0f; } else if (f > 255f) { f = 255f; }
                            alpha[i] = (byte)(f + 0.5f);
                        }
                    }
                    else
                    {
                        return null;
                    }

                    return SKColorFilter.CreateTable(alpha, null, null, null);
                }
            }
            catch
            {
                // Safe to ignore transfer function parsing failures.
            }

            return null;
        }
    }
}