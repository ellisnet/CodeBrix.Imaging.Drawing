namespace CodeBrix.Imaging.Drawing.Images;

/// <summary>How a picture's pixels are sampled when it is drawn at another size or angle.</summary>
public enum ImageSampling
{
    /// <summary>Bilinear interpolation between the four nearest source pixels (in premultiplied colour) - smooth (the default).</summary>
    Linear = 0,
    /// <summary>The nearest source pixel - hard, blocky pixels (pixel art, sprite sheets drawn at whole multiples).</summary>
    Nearest = 1,
}
