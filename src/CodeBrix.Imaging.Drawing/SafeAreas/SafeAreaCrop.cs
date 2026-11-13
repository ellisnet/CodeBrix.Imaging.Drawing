using System;

namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// A crop a platform applies to a frame - for example the centered 3:4 crop a profile grid
/// shows of a 9:16 portrait video. Everything OUTSIDE the crop is unsafe. The crop is the
/// largest rectangle of the stated aspect ratio that fits the frame, centered in it, so it
/// adapts to any frame size; a crop whose aspect ratio is the frame's own removes nothing.
/// </summary>
public sealed class SafeAreaCrop
{
    /// <summary>The crop's name, reported as the zone name of the areas it cuts away.</summary>
    public string Name { get; }

    /// <summary>The width part of the crop's aspect ratio (3 in 3:4).</summary>
    public float AspectWidth { get; }

    /// <summary>The height part of the crop's aspect ratio (4 in 3:4).</summary>
    public float AspectHeight { get; }

    /// <summary>
    /// Creates a centered crop of the given aspect ratio.
    /// </summary>
    /// <param name="name">The crop's name (for example "profile grid 3:4").</param>
    /// <param name="aspectWidth">The width part of the aspect ratio; must be positive.</param>
    /// <param name="aspectHeight">The height part of the aspect ratio; must be positive.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either aspect part is not positive and finite.</exception>
    public SafeAreaCrop(string name, float aspectWidth, float aspectHeight)
    {
        if (String.IsNullOrWhiteSpace(name)) { throw new ArgumentException("A crop name is required.", nameof(name)); }
        if (!(aspectWidth > 0f) || Single.IsInfinity(aspectWidth)) { throw new ArgumentOutOfRangeException(nameof(aspectWidth)); }
        if (!(aspectHeight > 0f) || Single.IsInfinity(aspectHeight)) { throw new ArgumentOutOfRangeException(nameof(aspectHeight)); }
        Name = name.Trim();
        AspectWidth = aspectWidth;
        AspectHeight = aspectHeight;
    }

    /// <summary>
    /// The part of a frame this crop keeps: the largest centered rectangle of the crop's
    /// aspect ratio that fits the frame.
    /// </summary>
    /// <param name="frameSize">The frame size, in pixels.</param>
    /// <returns>The kept rectangle, in frame pixels.</returns>
    public RectangleF GetKeptRectangle(SizeF frameSize)
    {
        float aspect = AspectWidth / AspectHeight;
        float width = frameSize.Width;
        float height = width / aspect;
        if (height > frameSize.Height)
        {
            height = frameSize.Height;
            width = height * aspect;
        }
        return new RectangleF((frameSize.Width - width) / 2f, (frameSize.Height - height) / 2f, width, height);
    }

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"{Name} ({AspectWidth}:{AspectHeight})");
}
