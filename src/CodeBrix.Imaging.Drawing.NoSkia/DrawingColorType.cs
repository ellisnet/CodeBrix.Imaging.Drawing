namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The in-memory pixel layout of a bitmap (reduced to the layouts this managed
/// implementation supports - both are 32 bits per pixel).
/// </summary>
public enum DrawingColorType
{
    /// <summary>An unknown or unset color type.</summary>
    Unknown = 0,

    /// <summary>8 bits per channel in red, green, blue, alpha byte order.</summary>
    Rgba8888 = 1,

    /// <summary>8 bits per channel in blue, green, red, alpha byte order.</summary>
    Bgra8888 = 2,
}
