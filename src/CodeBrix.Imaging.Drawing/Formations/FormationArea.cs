using System;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// The shape a formation fills, as the two questions its tools ask many thousands of times: is
/// this point inside, and how far is it from the edge. Made from a <see cref="PathGeometry"/>
/// (its filled area, by its own fill rule - the queries of
/// <see cref="PathGeometry.Contains(PointF, float)"/> and
/// <see cref="PathGeometry.DistanceToEdge(PointF, float)"/>) or from a mask plane (the pixels at
/// or above a threshold). Immutable; every answer is shared managed arithmetic, identical in both
/// packages.
/// </summary>
public sealed class FormationArea
{
    private readonly PathAreaQuery _query;
    private readonly DrawingPlane _mask;
    private readonly RectangleF _destination;
    private readonly float _threshold;
    private readonly float[] _inside;
    private readonly float[] _outside;

    private FormationArea(PathGeometry geometry, PathAreaQuery query)
    {
        Geometry = geometry;
        _query = query;
        Bounds = query.Bounds;
    }

    private FormationArea(DrawingPlane mask, RectangleF destination, float threshold)
    {
        _mask = mask;
        _destination = destination;
        _threshold = threshold;
        int width = mask.Width, height = mask.Height;
        double unitX = destination.Width / (double)width;
        double unitY = destination.Height / (double)height;
        var inside = new bool[width * height];
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!(mask.Data[(y * width) + x] >= threshold)) { continue; }
                inside[(y * width) + x] = true;
                if (x < minX) { minX = x; }
                if (x > maxX) { maxX = x; }
                if (y < minY) { minY = y; }
                if (y > maxY) { maxY = y; }
            }
        }
        Bounds = maxX < 0
            ? RectangleF.Empty
            : new RectangleF((float)(destination.X + (minX * unitX)), (float)(destination.Y + (minY * unitY)),
                (float)((maxX - minX + 1) * unitX), (float)((maxY - minY + 1) * unitY));
        //Distance from each inside pixel centre to the nearest outside one, and the other way
        _inside = MaskDistance.Compute(inside, width, height, unitX, unitY, true);
        _outside = MaskDistance.Compute(inside, width, height, unitX, unitY, false);
    }

    /// <summary>
    /// The filled area of a geometry, by its own fill rule, curves flattened within
    /// <paramref name="tolerance"/> (once, here).
    /// </summary>
    /// <param name="geometry">The area.</param>
    /// <param name="tolerance">How closely curves are followed, calibrated units; must be positive.</param>
    /// <returns>The area.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tolerance"/> is not positive and finite.</exception>
    public static FormationArea FromGeometry(PathGeometry geometry, float tolerance = PathGeometry.DefaultFlattenTolerance)
    {
        if (geometry == null) { throw new ArgumentNullException(nameof(geometry)); }
        return new FormationArea(geometry, geometry.GetAreaQuery(tolerance));
    }

    /// <summary>
    /// The pixels of a mask plane whose value is at least <paramref name="threshold"/>, the plane
    /// stretched over <paramref name="destination"/> (calibrated units) - for a shape that exists as
    /// a picture (a hard-edged mask export, a painted plane). A point is inside when the pixel it
    /// falls in is; its distance to the edge is the distance from that pixel's centre to the nearest
    /// pixel centre on the other side (exact, in units), so it is accurate to about a pixel.
    /// </summary>
    /// <param name="mask">The mask plane (read once; later changes to it are not seen).</param>
    /// <param name="destination">Where the plane lies, calibrated units.</param>
    /// <param name="threshold">The least value that counts as inside; 0.5 by default.</param>
    /// <returns>The area.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the destination is empty or not finite, or the threshold is NaN.</exception>
    public static FormationArea FromMask(DrawingPlane mask, RectangleF destination, float threshold = 0.5f)
    {
        if (mask == null) { throw new ArgumentNullException(nameof(mask)); }
        if (!(destination.Width > 0f) || !(destination.Height > 0f) || !Single.IsFinite(destination.X) || !Single.IsFinite(destination.Y)
            || !Single.IsFinite(destination.Width) || !Single.IsFinite(destination.Height))
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }
        if (Single.IsNaN(threshold)) { throw new ArgumentOutOfRangeException(nameof(threshold)); }
        return new FormationArea(mask.Clone(), destination, threshold);
    }

    /// <summary>The geometry the area was made from; <c>null</c> for a mask.</summary>
    public PathGeometry Geometry { get; }

    /// <summary>The box around the area, calibrated units (a geometry's exact bounds; a mask's inside pixels).</summary>
    public RectangleF Bounds { get; }

    /// <summary>Indicates whether a point is inside the area.</summary>
    /// <param name="x">The point's x, calibrated units.</param>
    /// <param name="y">The point's y, calibrated units.</param>
    /// <returns><c>true</c> when inside.</returns>
    public bool Contains(float x, float y)
    {
        if (_query != null) { return _query.Contains(x, y); }
        int index = PixelIndex(x, y);
        return index >= 0 && _mask.Data[index] >= _threshold;
    }

    /// <summary>Indicates whether a point is inside the area.</summary>
    /// <param name="point">The point, calibrated units.</param>
    /// <returns><c>true</c> when inside.</returns>
    public bool Contains(PointF point) => Contains(point.X, point.Y);

    /// <summary>The distance from a point to the area's edge (inside or out), calibrated units.</summary>
    /// <param name="x">The point's x, calibrated units.</param>
    /// <param name="y">The point's y, calibrated units.</param>
    /// <returns>The distance; positive infinity for an empty area (and, for a mask, a point off its plane).</returns>
    public float DistanceToEdge(float x, float y)
    {
        if (_query != null) { return _query.DistanceToEdge(x, y); }
        int index = PixelIndex(x, y);
        if (index < 0) { return Single.PositiveInfinity; }
        return _mask.Data[index] >= _threshold ? _inside[index] : _outside[index];
    }

    /// <summary>The distance from a point to the area's edge (inside or out), calibrated units.</summary>
    /// <param name="point">The point, calibrated units.</param>
    /// <returns>The distance; positive infinity for an empty area (and, for a mask, a point off its plane).</returns>
    public float DistanceToEdge(PointF point) => DistanceToEdge(point.X, point.Y);

    //The mask pixel a point falls in; -1 outside the plane
    private int PixelIndex(float x, float y)
    {
        double px = Math.Floor((x - _destination.X) * (double)_mask.Width / _destination.Width);
        double py = Math.Floor((y - _destination.Y) * (double)_mask.Height / _destination.Height);
        if (!(px >= 0) || !(py >= 0) || px >= _mask.Width || py >= _mask.Height) { return -1; }
        return ((int)py * _mask.Width) + (int)px;
    }
}
