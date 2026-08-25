using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A 32-bit RGBA color: the color is packed as ARGB (alpha in the highest byte) and the
/// components are exposed as bytes.
/// </summary>
public readonly struct DrawingColor : IEquatable<DrawingColor>
{
    private readonly uint _value;

    /// <summary>
    /// A fully transparent black color (all components zero) - the default value.
    /// </summary>
    public static readonly DrawingColor Empty = new DrawingColor(0);

    /// <summary>
    /// Creates a color from a packed ARGB value (alpha in bits 24-31, red in bits 16-23,
    /// green in bits 8-15, blue in bits 0-7).
    /// </summary>
    /// <param name="value">The packed ARGB value.</param>
    public DrawingColor(uint value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a fully opaque color from red, green, and blue components.
    /// </summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    public DrawingColor(byte red, byte green, byte blue)
        : this(red, green, blue, 255)
    {
    }

    /// <summary>
    /// Creates a color from red, green, blue, and alpha components.
    /// </summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <param name="alpha">The alpha component (0 = fully transparent, 255 = fully opaque).</param>
    public DrawingColor(byte red, byte green, byte blue, byte alpha)
    {
        _value = ((uint)alpha << 24) | ((uint)red << 16) | ((uint)green << 8) | blue;
    }

    /// <summary>The alpha component (0 = fully transparent, 255 = fully opaque).</summary>
    public byte Alpha => (byte)((_value >> 24) & 0xFF);

    /// <summary>The red component.</summary>
    public byte Red => (byte)((_value >> 16) & 0xFF);

    /// <summary>The green component.</summary>
    public byte Green => (byte)((_value >> 8) & 0xFF);

    /// <summary>The blue component.</summary>
    public byte Blue => (byte)(_value & 0xFF);

    /// <summary>
    /// Returns this color with the alpha component replaced.
    /// </summary>
    /// <param name="alpha">The new alpha component.</param>
    /// <returns>The same RGB color at the new alpha.</returns>
    public DrawingColor WithAlpha(byte alpha) => new DrawingColor(Red, Green, Blue, alpha);

    /// <summary>
    /// Returns this color with the red component replaced.
    /// </summary>
    /// <param name="red">The new red component.</param>
    /// <returns>The color with the new red component.</returns>
    public DrawingColor WithRed(byte red) => new DrawingColor(red, Green, Blue, Alpha);

    /// <summary>
    /// Returns this color with the green component replaced.
    /// </summary>
    /// <param name="green">The new green component.</param>
    /// <returns>The color with the new green component.</returns>
    public DrawingColor WithGreen(byte green) => new DrawingColor(Red, green, Blue, Alpha);

    /// <summary>
    /// Returns this color with the blue component replaced.
    /// </summary>
    /// <param name="blue">The new blue component.</param>
    /// <returns>The color with the new blue component.</returns>
    public DrawingColor WithBlue(byte blue) => new DrawingColor(Red, Green, blue, Alpha);

    /// <summary>
    /// Converts a packed ARGB value to a color.
    /// </summary>
    /// <param name="value">The packed ARGB value.</param>
    public static implicit operator DrawingColor(uint value) => new DrawingColor(value);

    /// <summary>
    /// Converts a color to its packed ARGB value.
    /// </summary>
    /// <param name="color">The color to convert.</param>
    public static explicit operator uint(DrawingColor color) => color._value;

    /// <summary>Compares two colors for exact component equality.</summary>
    /// <param name="left">The first color.</param>
    /// <param name="right">The second color.</param>
    /// <returns><c>true</c> when all four components are equal.</returns>
    public static bool operator ==(DrawingColor left, DrawingColor right) => left._value == right._value;

    /// <summary>Compares two colors for inequality.</summary>
    /// <param name="left">The first color.</param>
    /// <param name="right">The second color.</param>
    /// <returns><c>true</c> when any component differs.</returns>
    public static bool operator !=(DrawingColor left, DrawingColor right) => left._value != right._value;

    /// <inheritdoc />
    public bool Equals(DrawingColor other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is DrawingColor other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => $"#{_value:x8}";
}
