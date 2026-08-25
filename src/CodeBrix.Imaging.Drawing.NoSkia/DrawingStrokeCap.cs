namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How the ends of an open stroked path are capped.
/// </summary>
public enum DrawingStrokeCap
{
    /// <summary>The stroke ends exactly at the endpoint, with a flat edge.</summary>
    Butt = 0,

    /// <summary>The stroke ends with a semicircle centered on the endpoint.</summary>
    Round = 1,

    /// <summary>The stroke extends past the endpoint by half the stroke width, with a flat edge.</summary>
    Square = 2,
}
