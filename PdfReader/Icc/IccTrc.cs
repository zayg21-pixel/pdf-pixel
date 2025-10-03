namespace PdfReader.Icc
{
    internal sealed class IccTrc
    {
        public bool IsGamma { get; }
        public float Gamma { get; }
        public bool IsSampled { get; }
        public int SampleCount { get; }
        public float[] Samples { get; }

        // Parametric support (ICC parametricCurveType 0..4)
        public bool IsParametric { get; }
        public int ParametricType { get; }
        public float[] Parameters { get; }

        public bool IsUnsupportedParametric { get; }

        private IccTrc(
            bool isGamma,
            float gamma,
            bool isSampled,
            int sampleCount,
            float[] samples,
            bool isParametric,
            int paramType,
            float[] parameters,
            bool isUnsupportedParametric)
        {
            IsGamma = isGamma;
            Gamma = gamma;
            IsSampled = isSampled;
            SampleCount = sampleCount;
            Samples = samples;
            IsParametric = isParametric;
            ParametricType = paramType;
            Parameters = parameters;
            IsUnsupportedParametric = isUnsupportedParametric;
        }

        public static IccTrc FromGamma(float gamma)
            => new IccTrc(true, gamma, false, 0, null, false, 0, null, false);

        public static IccTrc FromSamples(float[] samples)
        {
            var s = samples ?? System.Array.Empty<float>();
            return new IccTrc(false, 0f, true, s.Length, s, false, 0, null, false);
        }

        public static IccTrc FromParametric(int type, float[] parameters)
        {
            // Supported types 0..4 per ICC spec
            return new IccTrc(false, 0f, false, 0, null, true, type, parameters ?? System.Array.Empty<float>(), false);
        }

        public static IccTrc Sampled(int count)
            => new IccTrc(false, 0f, true, count < 0 ? 0 : count, null, false, 0, null, false);

        public static IccTrc UnsupportedParametric(int type)
            => new IccTrc(false, 0f, false, 0, null, false, type, null, true);

        public override string ToString()
        {
            if (IsGamma) return $"Gamma({Gamma})";
            if (IsSampled) return Samples != null ? $"Curve(samples={Samples.Length})" : $"Curve(samples={SampleCount})";
            if (IsParametric) return $"Parametric(type={ParametricType})";
            if (IsUnsupportedParametric) return $"Parametric(unsupported type={ParametricType})";
            return "TRC(?)";
        }
    }
}
