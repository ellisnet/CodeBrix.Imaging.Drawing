using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// How a <see cref="CodeBrix.Imaging.Drawing.Shapes.PathShape"/> outlines its path: the stroke
/// width, the end caps, the corner joins and an optional dash pattern - all in calibrated
/// drawing units. Immutable; share one between many shapes.
/// </summary>
public sealed class PathStrokeStyle
{
    private readonly float[] _dashPattern;

    /// <summary>
    /// Creates a stroke style.
    /// </summary>
    /// <param name="width">The stroke width, calibrated units; must be positive.</param>
    /// <param name="cap">The end caps; <see cref="PathStrokeCap.Round"/> by default.</param>
    /// <param name="join">The corner joins; <see cref="PathStrokeJoin.Round"/> by default.</param>
    /// <param name="miterLimit">How far a miter join may reach, as a multiple of half the width, before it is cut to a bevel; 4 by default, at least 1.</param>
    /// <param name="dashPattern">Alternating dash and gap lengths (calibrated units, starting with a dash); <c>null</c> (the default) for a solid stroke. See <see cref="PathGeometry.Dash(IReadOnlyList{float}, float)"/>.</param>
    /// <param name="dashOffset">How far into the pattern each contour starts, calibrated units.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the width is not positive, the miter limit is below 1, or a value is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when the dash pattern is empty, has a negative entry or sums to 0.</exception>
    public PathStrokeStyle(float width, PathStrokeCap cap = PathStrokeCap.Round, PathStrokeJoin join = PathStrokeJoin.Round,
        float miterLimit = 4f, IReadOnlyList<float> dashPattern = null, float dashOffset = 0f)
    {
        if (!Single.IsFinite(width) || width <= 0f) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (!Single.IsFinite(miterLimit) || miterLimit < 1f) { throw new ArgumentOutOfRangeException(nameof(miterLimit)); }
        if (!Single.IsFinite(dashOffset)) { throw new ArgumentOutOfRangeException(nameof(dashOffset)); }
        if (dashPattern != null)
        {
            NormalizeDashPattern(dashPattern, nameof(dashPattern));
            _dashPattern = new float[dashPattern.Count];
            for (int i = 0; i < _dashPattern.Length; i++) { _dashPattern[i] = dashPattern[i]; }
        }
        Width = width;
        Cap = cap;
        Join = join;
        MiterLimit = miterLimit;
        DashOffset = dashOffset;
    }

    /// <summary>The stroke width, calibrated units.</summary>
    public float Width { get; }

    /// <summary>The end caps.</summary>
    public PathStrokeCap Cap { get; }

    /// <summary>The corner joins.</summary>
    public PathStrokeJoin Join { get; }

    /// <summary>The miter limit, as a multiple of half the width.</summary>
    public float MiterLimit { get; }

    /// <summary>How far into the dash pattern each contour starts, calibrated units.</summary>
    public float DashOffset { get; }

    /// <summary>Indicates whether the stroke is dashed.</summary>
    public bool IsDashed => _dashPattern != null;

    /// <summary>Returns a copy of the dash pattern.</summary>
    /// <returns>A new array of alternating dash and gap lengths; empty for a solid stroke.</returns>
    public float[] GetDashPattern() => _dashPattern == null ? Array.Empty<float>() : (float[])_dashPattern.Clone();

    /// <summary>The same style with another width.</summary>
    /// <param name="width">The new width.</param>
    /// <returns>A new style.</returns>
    public PathStrokeStyle WithWidth(float width) => new PathStrokeStyle(width, Cap, Join, MiterLimit, _dashPattern, DashOffset);

    /// <summary>The same style with another dash offset (for "marching" dashes).</summary>
    /// <param name="dashOffset">The new offset, calibrated units.</param>
    /// <returns>A new style.</returns>
    public PathStrokeStyle WithDashOffset(float dashOffset) => new PathStrokeStyle(Width, Cap, Join, MiterLimit, _dashPattern, dashOffset);

    //How far beyond the geometry the stroke can reach, as a multiple of half the width
    internal float ReachFactor
    {
        get
        {
            float factor = 1f;
            if (Join == PathStrokeJoin.Miter) { factor = Math.Max(factor, MiterLimit); }
            if (Cap == PathStrokeCap.Square) { factor = Math.Max(factor, 1.41421356f); }
            return factor;
        }
    }

    internal float[] DashPatternValue => _dashPattern;

    //Checks a dash pattern and returns it in double, doubled when it has an odd count
    internal static double[] NormalizeDashPattern(IReadOnlyList<float> pattern, string parameterName)
    {
        if (pattern == null) { throw new ArgumentNullException(parameterName); }
        if (pattern.Count == 0) { throw new ArgumentException("A dash pattern needs at least one length.", parameterName); }
        double sum = 0.0;
        for (int i = 0; i < pattern.Count; i++)
        {
            float value = pattern[i];
            if (!Single.IsFinite(value) || value < 0f) { throw new ArgumentException("Dash lengths must be finite and not negative.", parameterName); }
            sum += value;
        }
        if (sum <= 0.0) { throw new ArgumentException("A dash pattern must have some length.", parameterName); }
        int count = pattern.Count % 2 == 0 ? pattern.Count : pattern.Count * 2;
        var result = new double[count];
        for (int i = 0; i < count; i++) { result[i] = pattern[i % pattern.Count]; }
        return result;
    }
}
