namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>How the open ends of a stroked path (and of each dash) are finished.</summary>
public enum PathStrokeCap
{
    /// <summary>A half circle beyond the end point (the default, as for every other shape).</summary>
    Round = 0,
    /// <summary>Flat, exactly at the end point.</summary>
    Butt = 1,
    /// <summary>A half square beyond the end point.</summary>
    Square = 2,
}
