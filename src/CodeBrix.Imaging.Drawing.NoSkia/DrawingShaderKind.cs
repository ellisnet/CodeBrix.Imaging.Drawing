namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// What a <see cref="DrawingShader"/> paints - the discriminator that says which of the
/// shader's inspection properties carry meaningful values.
/// </summary>
public enum DrawingShaderKind
{
    /// <summary>One solid color, in <see cref="DrawingShader.Color"/>.</summary>
    Color = 0,

    /// <summary>A gradient along the axis from <see cref="DrawingShader.Start"/> to <see cref="DrawingShader.End"/>.</summary>
    LinearGradient = 1,

    /// <summary>A gradient outward from <see cref="DrawingShader.Center"/> to <see cref="DrawingShader.Radius"/>.</summary>
    RadialGradient = 2,

    /// <summary>A gradient between two circles - the SVG focal radial gradient model.</summary>
    TwoPointConicalGradient = 3,

    /// <summary>A repeating tile drawn from a recorded picture - how a pattern fill paints.</summary>
    Picture = 4,
}
