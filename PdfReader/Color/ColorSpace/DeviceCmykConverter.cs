using PdfReader.Color.Icc;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Icc.Transform;
using PdfReader.Color.Sampling;
using PdfReader.Color.Structures;
using PdfReader.Color.Transform;
using PdfReader.Resources;
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

    static DeviceCmykConverter()
    {
        var cmykIccBytes = PdfResourceLoader.GetResource("CompactCmyk.icc");
        var cmykProfile = IccProfile.Parse(cmykIccBytes);
        _iccTransform = new IccProfileTransform(cmykProfile);
    }

    public override int Components => 4;

    public override bool IsDevice => true;

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent, IColorTransform postTransform)
    {
        if (postTransform != null)
        {
            return new CmykSampler(postTransform);
        }

        return CmykSampler.Instance; // Looks like a good approximation, TODO: remove CompactCmyk
    }
}

internal sealed class CmykSampler : IRgbaSampler
{
    // Vectors for r channel coefficients (for c, m, y, k interactions)
    private static readonly Vector4 Rc = new Vector4(-4.387332384609988f, 54.48615194189176f, 18.82290502165302f, 212.25662451639585f);
    private static readonly float Rc0 = -285.2331026137004f;

    private static readonly Vector4 Rm = new Vector4(0f, 1.7149763477362134f, -5.6096736904047315f, -17.873870861415444f);
    private static readonly float Rm0 = -5.497006427196366f;

    private static readonly Vector4 Ry = new Vector4(0f, 0f, -2.5217340131683033f, -21.248923337353073f);
    private static readonly float Ry0 = 17.5119270841813f;

    private static readonly Vector4 Rk = new Vector4(0f, 0f, 0f, -21.86122147463605f);
    private static readonly float Rk0 = -189.48180835922747f;

    // Bias vector for r channel (c*Rc0 + m*Rm0 + y*Ry0 + k*Rk0)
    private static readonly Vector4 R0 = new Vector4(Rc0, Rm0, Ry0, Rk0);

    // Vectors for g channel coefficients
    private static readonly Vector4 Gc = new Vector4(8.841041422036149f, 60.118027045597366f, 6.871425592049007f, 31.159100130055922f);
    private static readonly float Gc0 = -79.2970844816548f;

    private static readonly Vector4 Gm = new Vector4(0f, -15.310361306967817f, 17.575251261109482f, 131.35250912493976f);
    private static readonly float Gm0 = -190.9453302588951f;

    private static readonly Vector4 Gy = new Vector4(0f, 0f, 4.444339102852739f, 9.8632861493405f);
    private static readonly float Gy0 = -24.86741582555878f;

    private static readonly Vector4 Gk = new Vector4(0f, 0f, 0f, -20.737325471181034f);
    private static readonly float Gk0 = -187.80453709719578f;

    // Bias vector for g channel
    private static readonly Vector4 G0 = new Vector4(Gc0, Gm0, Gy0, Gk0);

    // Vectors for b channel coefficients
    private static readonly Vector4 Bc = new Vector4(0.8842522430003296f, 8.078677503112928f, 30.89978309703729f, -0.23883238689178934f);
    private static readonly float Bc0 = -14.183576799673286f;

    private static readonly Vector4 Bm = new Vector4(0f, 10.49593273432072f, 63.02378494754052f, 50.606957656360734f);
    private static readonly float Bm0 = -112.23884253719248f;

    private static readonly Vector4 By = new Vector4(0f, 0f, 0.03296041114873217f, 115.60384449646641f);
    private static readonly float By0 = -193.58209356861505f;

    private static readonly Vector4 Bk = new Vector4(0f, 0f, 0f, -22.33816807309886f);
    private static readonly float Bk0 = -180.12613974708367f;

    // Bias vector for b channel
    private static readonly Vector4 B0 = new Vector4(Bc0, Bm0, By0, Bk0);

    private readonly IColorTransform _postTransform;

    public CmykSampler()
    {
    }

    public CmykSampler(IColorTransform postTransform)
    {
        _postTransform = postTransform;
    }

    public static CmykSampler Instance { get; } = new CmykSampler();

    public Vector4 Sample(ReadOnlySpan<float> source)
    {
        var c = source[0];
        var m = source[1];
        var y = source[2];
        var k = source[3];

        var cmyk = new Vector4(c, m, y, k);

        var rWeights = R0 + (c * Rc) + (m * Rm) + (y * Ry) + (k * Rk);
        var gWeights = G0 + (c * Gc) + (m * Gm) + (y * Gy) + (k * Gk);
        var bWeights = B0 + (c * Bc) + (m * Bm) + (y * By) + (k * Bk);

        var r = 255f + ColorVectorUtilities.CustomDot(rWeights, cmyk);
        var g = 255f + ColorVectorUtilities.CustomDot(gWeights, cmyk);
        var b = 255f + ColorVectorUtilities.CustomDot(bWeights, cmyk);

        var result = new Vector4(r, g, b, 255f) / 255f;

        return _postTransform?.Transform(result) ?? result;
    }
}
