namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Whether geometry is filled, stroked, or both.
/// </summary>
public enum DrawingPaintStyle
{
    /// <summary>Fill the geometry's interior.</summary>
    Fill = 0,

    /// <summary>Stroke the geometry's outline.</summary>
    Stroke = 1,

    /// <summary>Fill the interior and stroke the outline.</summary>
    StrokeAndFill = 2,
}
