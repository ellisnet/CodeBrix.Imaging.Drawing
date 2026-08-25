namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How the corners of a stroked path are joined.
/// </summary>
public enum DrawingStrokeJoin
{
    /// <summary>Corners extend to a sharp point, subject to the paint's miter limit.</summary>
    Miter = 0,

    /// <summary>Corners are rounded with a circular arc.</summary>
    Round = 1,

    /// <summary>Corners are cut flat between the two segment edges.</summary>
    Bevel = 2,
}
