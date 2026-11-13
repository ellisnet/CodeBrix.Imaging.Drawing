namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>How a stroked path turns its corners.</summary>
public enum PathStrokeJoin
{
    /// <summary>A circular corner (the default, as for every other shape).</summary>
    Round = 0,
    /// <summary>A sharp point, cut to a bevel when it would reach beyond the miter limit.</summary>
    Miter = 1,
    /// <summary>A straight cut across the corner.</summary>
    Bevel = 2,
}
