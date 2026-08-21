using System.Collections.Generic;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// One contour of a path after curve flattening: an ordered polyline plus whether the
/// contour was explicitly closed (which decides caps versus joins when stroking).
/// </summary>
internal sealed class FlattenedContour
{
    /// <summary>The contour's points, in order.</summary>
    public List<Vector2> Points { get; } = new List<Vector2>();

    /// <summary>Whether the contour was closed with an explicit close verb.</summary>
    public bool IsClosed { get; set; }
}
