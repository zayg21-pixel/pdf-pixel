namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Maps arc length to the parametric <c>t</c> of the segment it was built from: a plain ratio for a
/// straight segment, an interpolation within the arc-length table for a curve.
/// </summary>
internal readonly struct PdfLengthLookup
{
    private readonly float[]? _distances;
    private readonly float[]? _parameters;
    private readonly int _count;

    public PdfLengthLookup(float length)
    {
        Length = length;
        _distances = null;
        _parameters = null;
        _count = 0;
    }

    public PdfLengthLookup(float[] distances, float[] parameters, int count)
    {
        _distances = distances;
        _parameters = parameters;
        _count = count;
        Length = distances[count - 1];
    }

    public float Length { get; }

    public float ParameterAt(float targetLength)
    {
        if (_distances == null || _parameters == null)
        {
            return (Length <= PdfVectorMath.Epsilon) ? 0f : (targetLength / Length);
        }

        for (int i = 1; i < _count; i++)
        {
            if (_distances[i] >= targetLength)
            {
                float span = _distances[i] - _distances[i - 1];
                float localT = (span <= PdfVectorMath.Epsilon) ? 0f : ((targetLength - _distances[i - 1]) / span);
                return _parameters[i - 1] + (localT * (_parameters[i] - _parameters[i - 1]));
            }
        }

        return 1f;
    }
}
