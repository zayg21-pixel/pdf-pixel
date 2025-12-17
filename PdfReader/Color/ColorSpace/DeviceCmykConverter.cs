using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Transform;
using PdfReader.Resources;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Provides a converter for the Device CMYK color space to sRGB.
/// </summary>
/// <remarks>This converter uses an ICC profile to accurately transform CMYK color values to sRGB. It is designed
/// to handle the Device CMYK color space, which is commonly used in printing.</remarks>
internal sealed class DeviceCmykConverter : PdfColorSpaceConverter
{
    public static readonly DeviceCmykConverter Instance = new DeviceCmykConverter();
    private static readonly IccProfileTransform _iccTransform;
    
    // Matrix-based coefficient representation for maximum vectorization
    // Matrices are transposed because Vector4.Transform multiplies vector * matrix
    // Each column represents coefficients for R, G, B channels respectively
    private static readonly Matrix4x4 CoeffMatrixQuadratic = new Matrix4x4(
        -4.387332384609988f, 8.841041422036149f, 0.8842522430003296f, 0f,          // c² coefficients [R, G, B, unused]
        54.48615194189176f, 60.118027045597366f, 8.078677503112928f, 0f,            // cm coefficients [R, G, B, unused]
        18.82290502165302f, 6.871425592049007f, 30.89978309703729f, 0f,             // cy coefficients [R, G, B, unused]
        212.25662451639585f, 31.159100130055922f, -0.23883238689178934f, 0f         // ck coefficients [R, G, B, unused]
    );
    
    private static readonly Matrix4x4 CoeffMatrixLinearM = new Matrix4x4(
        1.7149763477362134f, -15.310361306967817f, 10.49593273432072f, 0f,          // m² coefficients [R, G, B, unused]
        -5.6096736904047315f, 17.575251261109482f, 63.02378494754052f, 0f,          // my coefficients [R, G, B, unused]
        -17.873870861415444f, 131.35250912493976f, 50.606957656360734f, 0f,         // mk coefficients [R, G, B, unused]
        0f, 0f, 0f, 0f                                                              // unused
    );
    
    private static readonly Matrix4x4 CoeffMatrixLinearY = new Matrix4x4(
        -2.5217340131683033f, 4.444339102852739f, 0.03296041114873217f, 0f,         // y² coefficients [R, G, B, unused]
        -21.248923337353073f, 9.8632861493405f, 115.60384449646641f, 0f,           // yk coefficients [R, G, B, unused]
        0f, 0f, 0f, 0f,                                                             // unused
        0f, 0f, 0f, 0f                                                              // unused
    );
    
    private static readonly Vector4 CoeffVectorK = new Vector4(
        -21.86122147463605f,     // R channel K² coefficient  
        -20.737325471181034f,    // G channel K² coefficient
        -22.33816807309886f,     // B channel K² coefficient
        0f                       // Alpha (unused)
    );
    
    private static readonly Matrix4x4 OffsetMatrix = new Matrix4x4(
        -285.2331026137004f, -79.2970844816548f, -14.183576799673286f, 0f,          // C offsets [R, G, B, unused]
        -5.497006427196366f, -190.9453302588951f, -112.23884253719248f, 0f,         // M offsets [R, G, B, unused]
        17.5119270841813f, -24.86741582555878f, -193.58209356861505f, 0f,           // Y offsets [R, G, B, unused]
        -189.48180835922747f, -187.80453709719578f, -180.12613974708367f, 0f        // K offsets [R, G, B, unused]
    );
    
    private static readonly Vector4 BaseRGB = new Vector4(255f, 255f, 255f, 255f);
    private static readonly Vector4 Zero = Vector4.Zero;
    private static readonly Vector4 MaxByte = new Vector4(255f);

    static DeviceCmykConverter()
    {
        var cmykIccBytes = PdfResourceLoader.GetResource("CompactCmyk.icc");
        var cmykProfile = IccProfile.Parse(cmykIccBytes);
        _iccTransform = new IccProfileTransform(cmykProfile);
    }

    public override int Components => 4;

    public override bool IsDevice => true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent renderingIntent)
    {
        // Create CMYK vector from input components
        var cmyk = IccVectorUtilities.ToVector4WithOnePadding(comps01);
        
        // Use fully vectorized conversion
        var rgb = ToSrgbFullyVectorized(cmyk);
        
        // Clamp and convert to bytes
        var clamped = Vector4.Clamp(rgb, Zero, MaxByte);
        
        var rClamp = (byte)clamped.X;
        var gClamp = (byte)clamped.Y;  
        var bClamp = (byte)clamped.Z;

        return new SKColor(rClamp, gClamp, bClamp);
    }
    
    /// <summary>
    /// Fully vectorized CMYK to RGB conversion using matrix operations for maximum SIMD utilization.
    /// </summary>
    /// <param name="cmyk">CMYK components as Vector4 (C, M, Y, K)</param>
    /// <returns>RGB components as Vector4</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 ToSrgbFullyVectorized(Vector4 cmyk)
    {
        // original comment from PDF.js:
        // The coefficients below was found using numerical analysis: the method of
        // steepest descent for the sum((f_i - color_value_i)^2) for r/g/b colors,
        // where color_value is the tabular value from the table of sampled RGB colors
        // from CMYK US Web Coated (SWOP) colorspace, and f_i is the corresponding
        // CMYK color conversion using the estimation below:
        //   f(A, B,.. N) = Acc+Bcm+Ccy+Dck+c+Fmm+Gmy+Hmk+Im+Jyy+Kyk+Ly+Mkk+Nk+255

        // Precompute all squared terms in one vectorized operation
        var squared = cmyk * cmyk; // [c², m², y², k²]
        
        // Vectorized cross-product calculations
        // Instead of computing each cross-product individually, use vector shuffles and multiplies
        var cVector = new Vector4(cmyk.X); // [c, c, c, c]
        var mVector = new Vector4(cmyk.Y); // [m, m, m, m]
        var yVector = new Vector4(cmyk.Z); // [y, y, y, y]
        
        // Cross-products for C-based terms: [c², cm, cy, ck]
        var cCrossTerms = cVector * cmyk; // [c², cm, cy, ck]
        cCrossTerms = new Vector4(squared.X, cCrossTerms.Y, cCrossTerms.Z, cCrossTerms.W); // Replace c² with pre-computed value
        
        // Cross-products for M-based terms: [m², my, mk, 0]
        var mCrossTerms = mVector * new Vector4(cmyk.Y, cmyk.Z, cmyk.W, 0f); // [m², my, mk, 0]
        mCrossTerms = new Vector4(squared.Y, mCrossTerms.Y, mCrossTerms.Z, 0f); // Replace m² with pre-computed value
        
        // Cross-products for Y-based terms: [y², yk, 0, 0]
        var yCrossTerms = yVector * new Vector4(cmyk.Z, cmyk.W, 0f, 0f); // [y², yk, 0, 0]
        yCrossTerms = new Vector4(squared.Z, yCrossTerms.Y, 0f, 0f); // Replace y² with pre-computed value
        
        // K² terms for all channels (already squared)
        var kSquaredTerm = new Vector4(squared.W);

        // Apply matrix transformations for each coefficient group
        // Vector4.Transform multiplies vector * matrix, so matrices are transposed above
        var resultQuadratic = Vector4.Transform(cCrossTerms, CoeffMatrixQuadratic);
        var resultLinearM = Vector4.Transform(mCrossTerms, CoeffMatrixLinearM);
        var resultLinearY = Vector4.Transform(yCrossTerms, CoeffMatrixLinearY);
        var resultK = kSquaredTerm * CoeffVectorK;
        
        // Apply offset matrix transformation
        var resultOffsets = Vector4.Transform(cmyk, OffsetMatrix);
        
        // Combine all terms and add base RGB values
        return BaseRGB + resultQuadratic + resultLinearM + resultLinearY + resultK + resultOffsets;
    }
}
