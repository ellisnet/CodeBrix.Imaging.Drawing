using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;

namespace VenusRosetteAnimation.Shapes;

/// <summary>
/// A glowing body: a soft halo fading to nothing around a solid core with a pale centre. Used for the Sun and
/// the two planets.
/// </summary>
internal sealed class GlowShape : DrawingShape
{
    private readonly SKPoint center;
    private readonly float coreRadius;
    private readonly float haloRadius;
    private readonly byte haloAlpha;

    /// <summary>Creates the body.</summary>
    /// <param name="center">Its centre.</param>
    /// <param name="coreRadius">The solid core's radius.</param>
    /// <param name="haloRadius">How far the halo reaches.</param>
    /// <param name="color">Its colour.</param>
    /// <param name="haloAlpha">The halo's alpha next to the core.</param>
    public GlowShape(PointF center, float coreRadius, float haloRadius, Color color, byte haloAlpha = 120)
        : base(1, color)
    {
        this.center = new SKPoint(center.X, center.Y);
        this.coreRadius = coreRadius;
        this.haloRadius = haloRadius;
        this.haloAlpha = haloAlpha;
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        using (SKShader halo = SKShader.CreateRadialGradient(
            center,
            haloRadius,
            new[] { color.WithAlpha(haloAlpha), color.WithAlpha((byte)(haloAlpha / 4)), color.WithAlpha(0) },
            new[] { coreRadius / haloRadius, 0.45f, 1f },
            SKShaderTileMode.Clamp))
        using (SKPaint paint = new SKPaint { Shader = halo, IsAntialias = true })
        {
            canvas.DrawCircle(center, haloRadius, paint);
        }

        SKColor pale = new SKColor(
            (byte)((color.Red + 255) / 2),
            (byte)((color.Green + 255) / 2),
            (byte)((color.Blue + 255) / 2));
        using (SKShader core = SKShader.CreateRadialGradient(
            new SKPoint(center.X - (coreRadius * 0.3f), center.Y - (coreRadius * 0.3f)),
            coreRadius * 1.3f,
            new[] { pale, color },
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp))
        using (SKPaint paint = new SKPaint { Shader = core, IsAntialias = true })
        {
            canvas.DrawCircle(center, coreRadius, paint);
        }
    }
}
