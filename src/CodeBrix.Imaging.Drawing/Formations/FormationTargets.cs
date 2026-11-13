using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.Formations;

/// <summary>
/// Chooses the spots that fill a shape for a formation: a jittered triangular grid over the
/// shape's bounds, kept to the inside and a clearance away from the edge, each spot given an
/// arrival key by an <see cref="IArrivalOrder"/>, sorted earliest first. Seeded (a
/// <see cref="DrawingRandom"/>), so the same shape and options always give the same spots, and
/// the arithmetic is shared managed code - identical in both packages.
/// <para>
/// THE GRID: rows <c>0.866 x Spacing</c> apart from half a row above the bottom of the bounds
/// upwards, every other row shifted by half the spacing; each grid point moved by a uniform random
/// amount up to <c>Jitter x Spacing</c> on each axis (two draws per grid point, x then y, whether
/// or not the spot is kept); a spot is kept when it is inside and at least
/// <see cref="FormationTargetOptions.EdgeClearance"/> from the edge, and its key is finite.
/// </para>
/// </summary>
public static class FormationTargets
{
    /// <summary>Fills a geometry's area with spots (the area is flattened at the default tolerance).</summary>
    /// <param name="area">The shape's area.</param>
    /// <param name="options">Spacing, jitter, clearance, seed and order; or <c>null</c> for the defaults.</param>
    /// <returns>The spots, earliest arrival first.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="area"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is out of range.</exception>
    public static List<FormationTarget> Fill(PathGeometry area, FormationTargetOptions options = null)
    {
        if (area == null) { throw new ArgumentNullException(nameof(area)); }
        return Fill(FormationArea.FromGeometry(area), options);
    }

    /// <summary>Fills an area (a geometry at a chosen tolerance, or a mask) with spots.</summary>
    /// <param name="area">The shape's area.</param>
    /// <param name="options">Spacing, jitter, clearance, seed and order; or <c>null</c> for the defaults.</param>
    /// <returns>The spots, earliest arrival first (spots with equal keys in no promised order, but always the same one).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="area"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is out of range.</exception>
    public static List<FormationTarget> Fill(FormationArea area, FormationTargetOptions options = null)
    {
        if (area == null) { throw new ArgumentNullException(nameof(area)); }
        FormationTargetOptions settings = options ?? new FormationTargetOptions();
        settings.Validate();
        IArrivalOrder order = settings.Order ?? new RandomArrivalOrder();
        order.Prepare(area, settings.Seed);
        var random = new DrawingRandom(settings.Seed);
        float spacing = settings.Spacing;
        float rowStep = spacing * 0.866f;
        float jitter = spacing * settings.Jitter;
        RectangleF bounds = area.Bounds;
        var targets = new List<FormationTarget>();
        int row = 0;
        for (float y = bounds.Bottom - (rowStep * 0.5f); y > bounds.Top; y -= rowStep, row++)
        {
            float shift = (row & 1) == 0 ? 0 : spacing * 0.5f;
            for (float x0 = bounds.Left + shift; x0 < bounds.Right; x0 += spacing)
            {
                float x = x0 + (float)random.Uniform(-jitter, jitter);
                float yy = y + (float)random.Uniform(-jitter, jitter);
                if (!area.Contains(x, yy)) { continue; }
                float clearance = area.DistanceToEdge(x, yy);
                if (clearance < settings.EdgeClearance) { continue; }
                float key = order.GetKey(x, yy, clearance);
                if (Single.IsInfinity(key) || Single.IsNaN(key)) { continue; }
                targets.Add(new FormationTarget(new PointF(x, yy), key, clearance));
            }
        }

        targets.Sort((a, b) => a.Key.CompareTo(b.Key));
        return targets;
    }
}
