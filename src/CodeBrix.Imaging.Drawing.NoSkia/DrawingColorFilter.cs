using System;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A per-pixel color transformation assigned to <see cref="DrawingPaint.ColorFilter"/>.
/// This managed implementation supports color matrices, per-channel tables, blend-mode
/// tinting, and the luminance-to-alpha filter that implements SVG luminance masks.
/// </summary>
public abstract class DrawingColorFilter
{
    private protected DrawingColorFilter()
    {
    }

    /// <summary>
    /// Creates a filter that applies a 4x5 color matrix (20 values, row-major, in the
    /// R G B A order SVG feColorMatrix uses, with offsets in the fifth column).
    /// </summary>
    /// <param name="matrix">The 20 matrix values.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentException">Thrown when the matrix does not hold exactly 20 values.</exception>
    public static DrawingColorFilter CreateColorMatrix(float[] matrix)
    {
        if (matrix == null || matrix.Length != 20)
        {
            throw new ArgumentException("A color matrix requires exactly 20 values.", nameof(matrix));
        }
        return new MatrixFilter((float[])matrix.Clone());
    }

    /// <summary>
    /// Creates a filter that remaps each channel through a 256-entry lookup table
    /// (<c>null</c> tables leave the channel unchanged).
    /// </summary>
    /// <param name="alphaTable">The alpha table, or <c>null</c>.</param>
    /// <param name="redTable">The red table, or <c>null</c>.</param>
    /// <param name="greenTable">The green table, or <c>null</c>.</param>
    /// <param name="blueTable">The blue table, or <c>null</c>.</param>
    /// <returns>The filter.</returns>
    public static DrawingColorFilter CreateTable(byte[] alphaTable, byte[] redTable, byte[] greenTable, byte[] blueTable)
        => new TableFilter(alphaTable, redTable, greenTable, blueTable);

    /// <summary>
    /// Creates a filter that blends a constant color over each pixel with the given mode.
    /// </summary>
    /// <param name="color">The blend color.</param>
    /// <param name="mode">The blend mode.</param>
    /// <returns>The filter.</returns>
    public static DrawingColorFilter CreateBlendMode(DrawingColor color, DrawingBlendMode mode)
        => new BlendFilter(color, mode);

    /// <summary>
    /// Creates the luminance-to-alpha filter: each pixel's alpha becomes its luminance
    /// times its alpha and its color becomes transparent black - how SVG luminance masks
    /// are realized.
    /// </summary>
    /// <returns>The filter.</returns>
    public static DrawingColorFilter CreateLumaColor() => new LumaFilter();

    /// <summary>
    /// What this filter does - the discriminator that says which of the properties below
    /// carry meaningful values.
    /// </summary>
    public abstract DrawingColorFilterKind Kind { get; }

    /// <summary>
    /// The 20 matrix values, for <see cref="DrawingColorFilterKind.ColorMatrix"/>;
    /// <c>null</c> otherwise. The returned array is a copy, so changing it does not change
    /// the filter.
    /// </summary>
    public virtual float[] Matrix => null;

    /// <summary>
    /// The alpha lookup table, for <see cref="DrawingColorFilterKind.Table"/>; <c>null</c>
    /// when the channel is unchanged or the filter is another kind. The returned array is a
    /// copy.
    /// </summary>
    public virtual byte[] AlphaTable => null;

    /// <summary>The red lookup table; see <see cref="AlphaTable"/>.</summary>
    public virtual byte[] RedTable => null;

    /// <summary>The green lookup table; see <see cref="AlphaTable"/>.</summary>
    public virtual byte[] GreenTable => null;

    /// <summary>The blue lookup table; see <see cref="AlphaTable"/>.</summary>
    public virtual byte[] BlueTable => null;

    /// <summary>
    /// The blended color, for <see cref="DrawingColorFilterKind.BlendMode"/>; transparent
    /// black otherwise.
    /// </summary>
    public virtual DrawingColor Color => default;

    /// <summary>
    /// The mode the color is blended with, for
    /// <see cref="DrawingColorFilterKind.BlendMode"/>;
    /// <see cref="DrawingBlendMode.SrcOver"/> otherwise.
    /// </summary>
    public virtual DrawingBlendMode BlendMode => DrawingBlendMode.SrcOver;

    /// <summary>
    /// Applies the filter to one straight-alpha color (components 0..1) - for the
    /// rendering internals.
    /// </summary>
    internal abstract void Apply(ref float red, ref float green, ref float blue, ref float alpha);

    private sealed class MatrixFilter : DrawingColorFilter
    {
        private readonly float[] _matrix;

        public MatrixFilter(float[] matrix)
        {
            _matrix = matrix;
        }

        public override DrawingColorFilterKind Kind => DrawingColorFilterKind.ColorMatrix;

        public override float[] Matrix => (float[])_matrix.Clone();

        internal override void Apply(ref float red, ref float green, ref float blue, ref float alpha)
        {
            float[] m = _matrix;
            float newRed = (m[0] * red) + (m[1] * green) + (m[2] * blue) + (m[3] * alpha) + m[4];
            float newGreen = (m[5] * red) + (m[6] * green) + (m[7] * blue) + (m[8] * alpha) + m[9];
            float newBlue = (m[10] * red) + (m[11] * green) + (m[12] * blue) + (m[13] * alpha) + m[14];
            float newAlpha = (m[15] * red) + (m[16] * green) + (m[17] * blue) + (m[18] * alpha) + m[19];
            red = Math.Clamp(newRed, 0f, 1f);
            green = Math.Clamp(newGreen, 0f, 1f);
            blue = Math.Clamp(newBlue, 0f, 1f);
            alpha = Math.Clamp(newAlpha, 0f, 1f);
        }
    }

    private sealed class TableFilter : DrawingColorFilter
    {
        private readonly byte[] _alphaTable;
        private readonly byte[] _redTable;
        private readonly byte[] _greenTable;
        private readonly byte[] _blueTable;

        public TableFilter(byte[] alphaTable, byte[] redTable, byte[] greenTable, byte[] blueTable)
        {
            _alphaTable = alphaTable;
            _redTable = redTable;
            _greenTable = greenTable;
            _blueTable = blueTable;
        }

        public override DrawingColorFilterKind Kind => DrawingColorFilterKind.Table;

        public override byte[] AlphaTable => Copy(_alphaTable);

        public override byte[] RedTable => Copy(_redTable);

        public override byte[] GreenTable => Copy(_greenTable);

        public override byte[] BlueTable => Copy(_blueTable);

        private static byte[] Copy(byte[] table) => table != null ? (byte[])table.Clone() : null;

        internal override void Apply(ref float red, ref float green, ref float blue, ref float alpha)
        {
            red = Remap(_redTable, red);
            green = Remap(_greenTable, green);
            blue = Remap(_blueTable, blue);
            alpha = Remap(_alphaTable, alpha);
        }

        private static float Remap(byte[] table, float value)
        {
            if (table == null || table.Length < 256) { return value; }
            int index = Math.Clamp((int)((value * 255f) + 0.5f), 0, 255);
            return table[index] / 255f;
        }
    }

    private sealed class BlendFilter : DrawingColorFilter
    {
        private readonly DrawingColor _color;
        private readonly float _red;
        private readonly float _green;
        private readonly float _blue;
        private readonly float _alpha;
        private readonly DrawingBlendMode _mode;

        public BlendFilter(DrawingColor color, DrawingBlendMode mode)
        {
            _color = color;
            _red = color.Red / 255f;
            _green = color.Green / 255f;
            _blue = color.Blue / 255f;
            _alpha = color.Alpha / 255f;
            _mode = mode;
        }

        public override DrawingColorFilterKind Kind => DrawingColorFilterKind.BlendMode;

        public override DrawingColor Color => _color;

        public override DrawingBlendMode BlendMode => _mode;

        internal override void Apply(ref float red, ref float green, ref float blue, ref float alpha)
        {
            //The filter color is the blend source; the filtered pixel is the destination
            Raster.Blender.Blend(_mode, _red, _green, _blue, _alpha, red, green, blue, alpha,
                out red, out green, out blue, out alpha);
        }
    }

    private sealed class LumaFilter : DrawingColorFilter
    {
        public override DrawingColorFilterKind Kind => DrawingColorFilterKind.LumaColor;

        internal override void Apply(ref float red, ref float green, ref float blue, ref float alpha)
        {
            //Rec. 709 luminance, matching Skia's SkLumaColorFilter
            float luminance = (0.2126f * red) + (0.7152f * green) + (0.0722f * blue);
            alpha *= Math.Clamp(luminance, 0f, 1f);
            red = green = blue = 0f;
        }
    }
}
