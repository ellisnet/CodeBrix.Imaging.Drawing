using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Shapes;
using SkiaSharp;

namespace VenusRosetteAnimation.Shapes;

/// <summary>
/// The backdrop: a deep plum glow around the Sun fading to near-black indigo at the edges, with a dim, fixed
/// starfield (seeded, so every run and every frame shows the same stars).
/// </summary>
internal sealed class SkyShape : DrawingShape
{
    private readonly float width;
    private readonly float height;
    private readonly SKPoint glowCenter;
    private readonly float glowRadius;
    private readonly List<Star> stars = new List<Star>();

    /// <summary>Creates the sky.</summary>
    /// <param name="width">The frame's width.</param>
    /// <param name="height">The frame's height.</param>
    /// <param name="glowCenterX">The centre of the glow (the Sun), x.</param>
    /// <param name="glowCenterY">The centre of the glow, y.</param>
    /// <param name="glowRadius">How far the glow reaches.</param>
    /// <param name="starCount">How many stars.</param>
    /// <param name="seed">The starfield's seed.</param>
    public SkyShape(float width, float height, float glowCenterX, float glowCenterY, float glowRadius, int starCount = 620, int seed = 8131)
        : base(1, (SKColor?)null)
    {
        this.width = width;
        this.height = height;
        glowCenter = new SKPoint(glowCenterX, glowCenterY);
        this.glowRadius = glowRadius;
        Random random = new Random(seed);
        SKColor[] tints = { new SKColor(232, 232, 255), new SKColor(255, 222, 236), new SKColor(206, 220, 255), new SKColor(255, 238, 214) };
        for (int i = 0; i < starCount; i++)
        {
            double u = random.NextDouble();
            double v = random.NextDouble();
            float x = (float)(random.NextDouble() * width);
            float y = (float)(random.NextDouble() * height);
            float radius = 0.45f + (float)(1.05 * u * u);
            byte alpha = (byte)(22 + (118 * v * v * v));
            SKColor tint = tints[random.Next(tints.Length)];
            stars.Add(new Star(x, y, radius, tint.WithAlpha(alpha), u > 0.965));
        }
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        SKRect frame = SKRect.Create(0, 0, width, height);
        using (SKPaint backdrop = new SKPaint())
        {
            backdrop.Color = new SKColor(7, 6, 16);
            canvas.DrawRect(frame, backdrop);
        }

        using (SKShader glow = SKShader.CreateRadialGradient(
            glowCenter,
            glowRadius,
            new[] { new SKColor(44, 20, 58), new SKColor(26, 13, 42), new SKColor(7, 6, 16) },
            new[] { 0f, 0.45f, 1f },
            SKShaderTileMode.Clamp))
        using (SKPaint paint = new SKPaint { Shader = glow, IsAntialias = true })
        {
            canvas.DrawRect(frame, paint);
        }

        using SKPaint dot = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (Star star in stars)
        {
            if (star.Halo)
            {
                dot.Color = star.Color.WithAlpha((byte)(star.Color.Alpha / 5));
                canvas.DrawCircle(star.X, star.Y, star.Radius * 3.2f, dot);
            }

            dot.Color = star.Color;
            canvas.DrawCircle(star.X, star.Y, star.Radius, dot);
        }
    }

    private readonly record struct Star(float X, float Y, float Radius, SKColor Color, bool Halo);
}
