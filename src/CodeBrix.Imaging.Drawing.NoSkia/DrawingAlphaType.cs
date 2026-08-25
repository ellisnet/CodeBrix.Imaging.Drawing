namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// How a bitmap's alpha channel is interpreted. This managed implementation stores pixels with
/// straight (unpremultiplied) alpha internally and honors the declared type at its
/// interop boundaries (<c>DrawingImage.ReadPixels</c> and encoded exports).
/// </summary>
public enum DrawingAlphaType
{
    /// <summary>An unknown or unset alpha type.</summary>
    Unknown = 0,

    /// <summary>Every pixel is fully opaque; the alpha channel is ignored.</summary>
    Opaque = 1,

    /// <summary>Color channels are premultiplied by the alpha channel.</summary>
    Premul = 2,

    /// <summary>Color channels are independent of the alpha channel (straight alpha).</summary>
    Unpremul = 3,
}
