using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// One painterly brushstroke: a filled, tapered lozenge laid along a centre line that may bow
/// sideways - built from its centre, length, width, direction and bend. It draws as an
/// ordinary anti-aliased filled shape, and it reports its own axes (<see cref="GetAxes"/>), so
/// in an ownership map every pixel it owns can be measured along and across the stroke - the
/// basis of bristle textures.
/// <para>
/// GEOMETRY, in the stroke's own axes (s runs from -0.5 at one end to +0.5 at the other; along
/// = s x <see cref="Length"/>): the centre line sits <c>Bend x Length x (4 s^2 - 1)</c> across
/// the axis (0 at the ends, <c>-Bend x Length</c> at the middle), and the half width there is
/// <c>Width / 2 x sqrt(max(0, 1 - |2 s|^Profile))</c>. <see cref="Profile"/> 2 gives an
/// ellipse-like stroke with pointed ends; the default 4 a blunt-ended lozenge; larger values
/// squarer ends. The outline is a polygon of <see cref="PointsPerSide"/> evenly spaced points
/// per side (the two tips shared). "Across" points to the right of the direction of travel on
/// screen (y down): <c>(-sin angle, cos angle)</c>. <see cref="Angle"/> is in DEGREES, clockwise on
/// screen from +x - the unit of <see cref="DrawingTransform.Rotation"/> and
/// <see cref="PathPoint.Angle"/>, so a direction read off a path can be passed straight in.
/// </para>
/// </summary>
public sealed class BrushStrokeShape : DrawingShape
{
    /// <summary>The default <see cref="Profile"/>: a blunt-ended lozenge.</summary>
    public const float DefaultProfile = 4f;

    /// <summary>How many outline points each side of the stroke has, the two tips included.</summary>
    public const int PointsPerSide = 17;

    private readonly PointF[] _outline;
    private readonly RectangleF _bounds;

    /// <summary>
    /// Creates a brushstroke in a flat color.
    /// </summary>
    /// <param name="centerX">The horizontal position of the stroke's centre, calibrated units.</param>
    /// <param name="centerY">The vertical position of the stroke's centre, calibrated units.</param>
    /// <param name="length">Its length end to end; positive.</param>
    /// <param name="width">Its width at the middle; positive.</param>
    /// <param name="angle">Its direction, in degrees, clockwise on screen from +x (y points down).</param>
    /// <param name="bend">How far it bows sideways at the middle, as a fraction of its length (0 = straight).</param>
    /// <param name="profile">How blunt its ends are (the exponent of the width profile, positive; see the remarks above).</param>
    /// <param name="color">The stroke's color; or <c>null</c> to use the owning layer's color.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, or <paramref name="length"/>, <paramref name="width"/> or <paramref name="profile"/> is not positive.</exception>
    public BrushStrokeShape(float centerX, float centerY, float length, float width, float angle,
        float bend = 0f, float profile = DefaultProfile, Color? color = null)
        : base(1f, color)
    {
        Validate(centerX, centerY, length, width, angle, bend, profile);
        CenterX = centerX;
        CenterY = centerY;
        Length = length;
        Width = width;
        Angle = angle;
        Bend = bend;
        Profile = profile;
        _outline = BuildOutline();
        _bounds = BoundsOf(_outline);
    }

    /// <summary>
    /// Creates a brushstroke painted by <paramref name="fill"/> - for example a gradient along
    /// the stroke - instead of a flat color.
    /// </summary>
    /// <param name="centerX">The horizontal position of the stroke's centre, calibrated units.</param>
    /// <param name="centerY">The vertical position of the stroke's centre, calibrated units.</param>
    /// <param name="length">Its length end to end; positive.</param>
    /// <param name="width">Its width at the middle; positive.</param>
    /// <param name="angle">Its direction, in degrees, clockwise on screen from +x (y points down).</param>
    /// <param name="bend">How far it bows sideways at the middle, as a fraction of its length.</param>
    /// <param name="profile">How blunt its ends are (positive).</param>
    /// <param name="fill">The fill that paints the stroke.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fill"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is not finite, or <paramref name="length"/>, <paramref name="width"/> or <paramref name="profile"/> is not positive.</exception>
    public BrushStrokeShape(float centerX, float centerY, float length, float width, float angle,
        float bend, float profile, ShapeFill fill)
        : base(1f, (Color?)null, fill ?? throw new ArgumentNullException(nameof(fill)))
    {
        Validate(centerX, centerY, length, width, angle, bend, profile);
        CenterX = centerX;
        CenterY = centerY;
        Length = length;
        Width = width;
        Angle = angle;
        Bend = bend;
        Profile = profile;
        _outline = BuildOutline();
        _bounds = BoundsOf(_outline);
    }

    /// <summary>The horizontal position of the stroke's centre, calibrated units.</summary>
    public float CenterX { get; }

    /// <summary>The vertical position of the stroke's centre, calibrated units.</summary>
    public float CenterY { get; }

    /// <summary>The stroke's length, end to end.</summary>
    public float Length { get; }

    /// <summary>The stroke's width at its middle.</summary>
    public float Width { get; }

    /// <summary>The stroke's direction, in degrees, clockwise on screen from +x (y points down).</summary>
    public float Angle { get; }

    /// <summary>How far the stroke bows sideways at its middle, as a fraction of its length.</summary>
    public float Bend { get; }

    /// <summary>The exponent of the width profile: 2 pointed, 4 (the default) blunt, larger squarer.</summary>
    public float Profile { get; }

    /// <summary>Returns a copy of the outline polygon's points, in order around the stroke.</summary>
    /// <returns>A new array of the points, calibrated units.</returns>
    public PointF[] GetPoints() => (PointF[])_outline.Clone();

    /// <summary>The outline polygon as a closed geometry (non-zero fill rule).</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline() => PathGeometry.FromPoints(_outline, true);

    /// <summary>The area the stroke covers: its outline polygon.</summary>
    /// <param name="tolerance">Not used (the polygon is filled).</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance) => new[] { GetOutline() };

    /// <summary>The stroke's own axes: its centre, its direction, its length and its width - unbent (the bow lies inside them).</summary>
    /// <returns>The axes.</returns>
    public override ElementAxes? GetAxes() => new ElementAxes(new PointF(CenterX, CenterY), Angle, Length, Width);

    /// <summary>The box around the outline polygon, calibrated units.</summary>
    /// <returns>The bounds.</returns>
    public override RectangleF? GetBounds() => _bounds;

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        var pathBuilder = new SKPathBuilder();
        pathBuilder.MoveTo(GraphicsInterop.ToGraphics(_outline[0]));
        for (int i = 1; i < _outline.Length; i++)
        {
            pathBuilder.LineTo(GraphicsInterop.ToGraphics(_outline[i]));
        }
        pathBuilder.Close();
        using SKPath path = pathBuilder.Detach();
        using SKPaint paint = CreateFillPaint(color, Fill);
        canvas.DrawPath(path, paint);
    }

    private static void Validate(float centerX, float centerY, float length, float width, float angle, float bend, float profile)
    {
        if (!float.IsFinite(centerX)) { throw new ArgumentOutOfRangeException(nameof(centerX)); }
        if (!float.IsFinite(centerY)) { throw new ArgumentOutOfRangeException(nameof(centerY)); }
        if (!float.IsFinite(length) || length <= 0f) { throw new ArgumentOutOfRangeException(nameof(length)); }
        if (!float.IsFinite(width) || width <= 0f) { throw new ArgumentOutOfRangeException(nameof(width)); }
        if (!float.IsFinite(angle)) { throw new ArgumentOutOfRangeException(nameof(angle)); }
        if (!float.IsFinite(bend)) { throw new ArgumentOutOfRangeException(nameof(bend)); }
        if (!float.IsFinite(profile) || profile <= 0f) { throw new ArgumentOutOfRangeException(nameof(profile)); }
    }

    //One side from tip to tip, then back along the other side (the tips are shared). Computed in
    //  double: the angle in radians, the across offsets, then the rotation about the centre;
    //  stored as floats
    private PointF[] BuildOutline()
    {
        const int N = PointsPerSide;
        var points = new PointF[(2 * N) - 2];
        double radians = AngleUnits.ToRadians(Angle);
        double cos = Math.Cos(radians), sin = Math.Sin(radians);
        for (int i = 0; i < N; i++)
        {
            double s = -0.5 + (i / (double)(N - 1));
            double along = s * Length;
            double bow = Bend * (double)Length * ((4.0 * s * s) - 1.0);
            double half = Math.Sqrt(Math.Max(1.0 - Math.Pow(Math.Abs(2.0 * s), Profile), 0.0)) * Width / 2.0;
            points[i] = Place(along, bow + half, cos, sin);
            if (i > 0 && i < N - 1) { points[(2 * N) - 1 - i - 1] = Place(along, bow - half, cos, sin); }
        }
        return points;
    }

    private PointF Place(double along, double across, double cos, double sin)
        => new PointF((float)(CenterX + (along * cos) - (across * sin)), (float)(CenterY + (along * sin) + (across * cos)));
}
