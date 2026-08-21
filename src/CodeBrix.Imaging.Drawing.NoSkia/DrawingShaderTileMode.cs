namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How a gradient behaves outside its defined range, API-compatible with the SkiaSharp
/// <c>SKShaderTileMode</c> enumeration.
/// </summary>
public enum DrawingShaderTileMode
{
    /// <summary>Positions outside the range take the nearest edge color.</summary>
    Clamp = 0,

    /// <summary>The gradient repeats.</summary>
    Repeat = 1,

    /// <summary>The gradient repeats, mirrored on every other repetition.</summary>
    Mirror = 2,

    /// <summary>Positions outside the range are transparent.</summary>
    Decal = 3,
}
