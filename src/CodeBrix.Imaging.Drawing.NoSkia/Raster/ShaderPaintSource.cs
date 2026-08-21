using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// A paint source that evaluates a <see cref="DrawingShader"/>: each device pixel is
/// mapped through an inverse transform into the shader's local coordinate space, and the
/// paint's alpha modulates the shader's output.
/// </summary>
internal sealed class ShaderPaintSource : PaintSource
{
    private readonly DrawingShader _shader;
    private readonly Matrix3x2 _deviceToLocal;
    private readonly float _alphaScale;

    /// <summary>
    /// Creates a shader paint source.
    /// </summary>
    /// <param name="shader">The shader to evaluate.</param>
    /// <param name="deviceToLocal">The transform from device coordinates to the shader's local space.</param>
    /// <param name="alphaScale">The paint alpha modulation, 0..1.</param>
    public ShaderPaintSource(DrawingShader shader, Matrix3x2 deviceToLocal, float alphaScale)
    {
        _shader = shader;
        _deviceToLocal = deviceToLocal;
        _alphaScale = alphaScale;
    }

    /// <inheritdoc />
    public override void GetColor(int x, int y, out float red, out float green, out float blue, out float alpha)
    {
        Vector2 local = Vector2.Transform(new Vector2(x + 0.5f, y + 0.5f), _deviceToLocal);
        _shader.GetLocalColor(local, out red, out green, out blue, out alpha);
        alpha *= _alphaScale;
    }
}
