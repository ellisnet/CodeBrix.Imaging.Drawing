using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Rendering;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Shapes;

/// <summary>
/// The live particles of a <see cref="DrawingParticleSystem"/> at one moment, drawn as soft
/// round dots - the way to put the data-only particle system into a drawing session. Each
/// particle is a dot of diameter <see cref="DrawingParticle.Size"/> centred on its
/// (<see cref="DrawingParticle.X"/>, <see cref="DrawingParticle.Y"/>), in the particle's own
/// <see cref="DrawingParticle.Color"/> with that colour's alpha multiplied by
/// <see cref="DrawingParticle.Opacity"/>; particle positions and sizes are read as calibrated
/// drawing units (pixels when the calibration size is the frame size). Dots draw in the
/// system's order (by emitter, then birth number), so later particles paint over earlier ones.
/// The layer's colour and the shape's <see cref="DrawingShape.Color"/> are not used.
/// <para>
/// The particles are computed ONCE, when the shape is created: like every shape, it is a
/// fixed piece of a drawing. To animate, create a new shape per frame (for example in a fresh
/// session, or after clearing the layer) - <see cref="DrawingParticleSystem.GetParticles(double)"/>
/// is a pure function of time, so any frame can be drawn in any order.
/// </para>
/// </summary>
public sealed class ParticlesShape : DrawingShape
{
    private readonly DrawingParticle[] _particles;

    /// <summary>The particle system the particles were taken from.</summary>
    public DrawingParticleSystem ParticleSystem { get; }

    /// <summary>The moment, in seconds, at which the particles were taken.</summary>
    public double Time { get; }

    /// <summary>
    /// How soft each dot's edge is, 0..1: the fraction of the radius, measured inwards from
    /// the edge, over which the dot fades linearly from its colour to transparent. 1 (the
    /// default) fades all the way from the centre; 0 draws a plain anti-aliased disc.
    /// </summary>
    public float Softness { get; }

    /// <summary>The particles this shape draws, in drawing order.</summary>
    public IReadOnlyList<DrawingParticle> Particles => _particles;

    /// <summary>
    /// Takes the live particles of <paramref name="system"/> at <paramref name="time"/> and
    /// creates a shape that draws them as soft dots.
    /// </summary>
    /// <param name="system">The particle system to draw.</param>
    /// <param name="time">The moment, in seconds, whose particles to draw.</param>
    /// <param name="softness">How soft each dot's edge is, 0..1 (see <see cref="Softness"/>); 1 by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="system"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="time"/> is not finite or <paramref name="softness"/> is outside 0..1.</exception>
    public ParticlesShape(DrawingParticleSystem system, double time, float softness = 1f)
        : base(Stroke.DefaultWidth, (Color?)null)
    {
        if (system == null) { throw new ArgumentNullException(nameof(system)); }
        if (!double.IsFinite(time)) { throw new ArgumentOutOfRangeException(nameof(time)); }
        if (!(softness >= 0f && softness <= 1f)) { throw new ArgumentOutOfRangeException(nameof(softness)); }
        ParticleSystem = system;
        Time = time;
        Softness = softness;
        var particles = new List<DrawingParticle>();
        system.GetParticles(time, particles);
        _particles = particles.ToArray();
    }

    /// <summary>Each live particle's dot (a circle of its size, where its size is above 0) as closed contours.</summary>
    /// <returns>The outline.</returns>
    public override PathGeometry GetOutline()
    {
        var builder = new PathGeometryBuilder();
        foreach (DrawingParticle particle in Particles)
        {
            if (particle.Size > 0f) { builder.AddCircle(new PointF(particle.X, particle.Y), particle.Size / 2f); }
        }
        return builder.Build();
    }

    /// <summary>
    /// Reports the union of the dots' squares (centre plus and minus half the size); an empty
    /// rectangle when no particle is alive.
    /// </summary>
    /// <returns>The bounds in calibrated drawing units.</returns>
    public override RectangleF? GetBounds()
    {
        bool any = false;
        float left = 0f, top = 0f, right = 0f, bottom = 0f;
        foreach (DrawingParticle particle in _particles)
        {
            if (!(particle.Size > 0f)) { continue; }
            float half = particle.Size / 2f;
            float l = particle.X - half, t = particle.Y - half, r = particle.X + half, b = particle.Y + half;
            if (!any)
            {
                left = l; top = t; right = r; bottom = b;
                any = true;
                continue;
            }
            left = Math.Min(left, l);
            top = Math.Min(top, t);
            right = Math.Max(right, r);
            bottom = Math.Max(bottom, b);
        }
        return any ? new RectangleF(left, top, right - left, bottom - top) : RectangleF.Empty;
    }

    /// <summary>The area the dots cover: one disc per particle that is drawn (size above 0, alpha above 0), whatever its softness.</summary>
    /// <param name="tolerance">Not used (the discs are filled).</param>
    /// <returns>One geometry.</returns>
    public override IReadOnlyList<PathGeometry> GetCoverageAreas(float tolerance)
    {
        var builder = new PathGeometryBuilder();
        foreach (DrawingParticle particle in _particles)
        {
            if (!(particle.Size > 0f)) { continue; }
            float opacity = particle.Opacity > 0f ? Math.Min(particle.Opacity, 1f) : 0f;
            if (DrawingRenderer.ScaleAlpha(GraphicsInterop.ToGraphics(particle.Color).Alpha, opacity) == 0) { continue; }
            builder.AddCircle(new PointF(particle.X, particle.Y), particle.Size / 2f);
        }
        return new[] { builder.Build() };
    }

    /// <inheritdoc />
    public override void Draw(SKCanvas canvas, SKColor color)
    {
        foreach (DrawingParticle particle in _particles)
        {
            if (!(particle.Size > 0f)) { continue; }
            float opacity = particle.Opacity > 0f ? Math.Min(particle.Opacity, 1f) : 0f;
            SKColor core = GraphicsInterop.ToGraphics(particle.Color);
            byte alpha = DrawingRenderer.ScaleAlpha(core.Alpha, opacity);
            if (alpha == 0) { continue; }
            core = core.WithAlpha(alpha);
            float radius = particle.Size / 2f;

            if (Softness <= 0f)
            {
                using SKPaint solid = CreateFillPaint(core);
                canvas.DrawCircle(particle.X, particle.Y, radius, solid);
                continue;
            }

            //A radial gradient from the dot's colour to the same colour at alpha 0 (so the
            //  fade never darkens); the paint itself is opaque because its alpha would
            //  otherwise scale the shader's output a second time
            SKColor edge = core.WithAlpha(0);
            SKColor[] colors = Softness >= 1f ? new[] { core, edge } : new[] { core, core, edge };
            float[] positions = Softness >= 1f ? new[] { 0f, 1f } : new[] { 0f, 1f - Softness, 1f };
            SKShader shader = SKShader.CreateRadialGradient(new SKPoint(particle.X, particle.Y), radius,
                colors, positions, SKShaderTileMode.Clamp);
            using (SKPaint soft = CreateFillPaint(SKColors.Black))
            {
                soft.Shader = shader;
                canvas.DrawCircle(particle.X, particle.Y, radius, soft);
                soft.Shader = null;
            }
            (shader as IDisposable)?.Dispose();
        }
    }
}
