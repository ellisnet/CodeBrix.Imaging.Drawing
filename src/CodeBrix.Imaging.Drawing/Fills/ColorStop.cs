using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Fills;

/// <summary>
/// One color at one position along a gradient or a color ramp: the building block shared by
/// <see cref="GradientFill"/> (and its linear and radial kinds) and
/// <see cref="CodeBrix.Imaging.Drawing.Animation.ColorRamp"/>, so one list of stops can paint
/// a shape and drive an animated color alike.
/// </summary>
public readonly struct ColorStop : IEquatable<ColorStop>
{
    /// <summary>
    /// The stop's position, from 0 (the start of the gradient or ramp) to 1 (its end).
    /// </summary>
    public float Position { get; }

    /// <summary>
    /// The color at <see cref="Position"/>, including its alpha (straight, not premultiplied).
    /// </summary>
    public Color Color { get; }

    /// <summary>
    /// Creates a color stop.
    /// </summary>
    /// <param name="position">The position, 0..1.</param>
    /// <param name="color">The color at that position.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="position"/> is NaN or outside 0..1.
    /// </exception>
    public ColorStop(float position, Color color)
    {
        if (Single.IsNaN(position) || position < 0f || position > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        Position = position;
        Color = color;
    }

    /// <inheritdoc />
    public bool Equals(ColorStop other) => Position.Equals(other.Position) && Color.Equals(other.Color);

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ColorStop other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Position, Color);

    /// <summary>Compares two stops for equality of position and color.</summary>
    /// <param name="left">The first stop.</param>
    /// <param name="right">The second stop.</param>
    /// <returns><c>true</c> when both position and color are equal.</returns>
    public static bool operator ==(ColorStop left, ColorStop right) => left.Equals(right);

    /// <summary>Compares two stops for inequality of position or color.</summary>
    /// <param name="left">The first stop.</param>
    /// <param name="right">The second stop.</param>
    /// <returns><c>true</c> when the position or the color differs.</returns>
    public static bool operator !=(ColorStop left, ColorStop right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"{Position:0.###}: {Color}";

    //Validates and copies a caller's stop list: at least two stops, positions non-decreasing
    internal static ColorStop[] ValidateAndCopy(IReadOnlyList<ColorStop> stops, string parameterName)
    {
        if (stops == null) { throw new ArgumentNullException(parameterName); }
        if (stops.Count < 2) { throw new ArgumentException("At least two color stops are required.", parameterName); }

        var copy = new ColorStop[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            copy[i] = stops[i];
            if (i > 0 && copy[i].Position < copy[i - 1].Position)
            {
                throw new ArgumentException("Color stop positions must be in non-decreasing order.", parameterName);
            }
        }
        return copy;
    }

    //Evenly spaced stops for a plain list of colors
    internal static ColorStop[] EvenlySpaced(IReadOnlyList<Color> colors, string parameterName)
    {
        if (colors == null) { throw new ArgumentNullException(parameterName); }
        if (colors.Count < 2) { throw new ArgumentException("At least two colors are required.", parameterName); }

        var stops = new ColorStop[colors.Count];
        for (int i = 0; i < colors.Count; i++)
        {
            float position = i == colors.Count - 1 ? 1f : i / (float)(colors.Count - 1);
            stops[i] = new ColorStop(position, colors[i]);
        }
        return stops;
    }
}
