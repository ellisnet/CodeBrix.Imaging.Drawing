using System;

namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// The four edge bands of a frame that a <see cref="SafeAreaGuide"/> treats as unsafe, each
/// as a FRACTION of the frame's height (top, bottom) or width (left, right) - so one guide
/// fits any frame size of its format. For example a top of 0.14 on a 1080 x 1920 portrait
/// frame marks the top 268.8 pixels.
/// </summary>
public readonly struct SafeAreaMargins : IEquatable<SafeAreaMargins>
{
    /// <summary>No margins at all.</summary>
    public static readonly SafeAreaMargins None = default;

    /// <summary>
    /// Margins that cover the entire frame (half the frame from each edge): a guide with them
    /// marks the whole frame unsafe - usually together with a <see cref="SafeAreaTimeRule"/>.
    /// </summary>
    public static readonly SafeAreaMargins WholeFrame = new SafeAreaMargins(0.5f, 0.5f, 0.5f, 0.5f);

    /// <summary>The unsafe band along the top edge, as a fraction of the frame height.</summary>
    public float Top { get; }

    /// <summary>The unsafe band along the bottom edge, as a fraction of the frame height.</summary>
    public float Bottom { get; }

    /// <summary>The unsafe band along the left edge, as a fraction of the frame width.</summary>
    public float Left { get; }

    /// <summary>The unsafe band along the right edge, as a fraction of the frame width.</summary>
    public float Right { get; }

    /// <summary>Indicates whether every margin is zero.</summary>
    public bool IsEmpty => Top == 0f && Bottom == 0f && Left == 0f && Right == 0f;

    /// <summary>
    /// Creates margins from four fractions of the frame.
    /// </summary>
    /// <param name="top">The top band, as a fraction of the frame height, 0..1.</param>
    /// <param name="bottom">The bottom band, as a fraction of the frame height, 0..1.</param>
    /// <param name="left">The left band, as a fraction of the frame width, 0..1.</param>
    /// <param name="right">The right band, as a fraction of the frame width, 0..1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a fraction is NaN or outside 0..1, or when top + bottom or left + right exceeds 1.
    /// </exception>
    public SafeAreaMargins(float top, float bottom, float left, float right)
    {
        Validate(top, nameof(top));
        Validate(bottom, nameof(bottom));
        Validate(left, nameof(left));
        Validate(right, nameof(right));
        if (top + bottom > 1f) { throw new ArgumentOutOfRangeException(nameof(bottom), "The top and bottom margins together exceed the frame."); }
        if (left + right > 1f) { throw new ArgumentOutOfRangeException(nameof(right), "The left and right margins together exceed the frame."); }
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
    }

    /// <summary>
    /// Creates margins of the same fraction on every edge - for example 0.05 for the
    /// broadcast title-safe area (90% of the width and of the height).
    /// </summary>
    /// <param name="fraction">The fraction of the frame's width (left, right) and height (top, bottom), 0..0.5.</param>
    /// <returns>The margins.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fraction"/> is NaN or outside 0..0.5.</exception>
    public static SafeAreaMargins Uniform(float fraction) => new SafeAreaMargins(fraction, fraction, fraction, fraction);

    /// <summary>
    /// The larger of two margins on every edge - the margins whose bands cover both.
    /// </summary>
    /// <param name="a">The first margins.</param>
    /// <param name="b">The second margins.</param>
    /// <returns>The edge-by-edge maximum (capped so opposite edges never exceed the frame).</returns>
    public static SafeAreaMargins Max(SafeAreaMargins a, SafeAreaMargins b)
    {
        float top = Math.Max(a.Top, b.Top);
        float bottom = Math.Min(Math.Max(a.Bottom, b.Bottom), 1f - top);
        float left = Math.Max(a.Left, b.Left);
        float right = Math.Min(Math.Max(a.Right, b.Right), 1f - left);
        return new SafeAreaMargins(top, bottom, left, right);
    }

    /// <inheritdoc />
    public bool Equals(SafeAreaMargins other)
        => Top.Equals(other.Top) && Bottom.Equals(other.Bottom) && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is SafeAreaMargins other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Top, Bottom, Left, Right);

    /// <summary>Compares two margins for equality.</summary>
    /// <param name="left">The first margins.</param>
    /// <param name="right">The second margins.</param>
    /// <returns><c>true</c> when all four fractions are equal.</returns>
    public static bool operator ==(SafeAreaMargins left, SafeAreaMargins right) => left.Equals(right);

    /// <summary>Compares two margins for inequality.</summary>
    /// <param name="left">The first margins.</param>
    /// <param name="right">The second margins.</param>
    /// <returns><c>true</c> when any fraction differs.</returns>
    public static bool operator !=(SafeAreaMargins left, SafeAreaMargins right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => FormattableString.Invariant($"top {Top:P1}, bottom {Bottom:P1}, left {Left:P1}, right {Right:P1}");

    private static void Validate(float value, string name)
    {
        if (Single.IsNaN(value) || value < 0f || value > 1f) { throw new ArgumentOutOfRangeException(name); }
    }
}
