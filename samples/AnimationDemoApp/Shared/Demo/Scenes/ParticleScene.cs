using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Particles: three seeded <see cref="DrawingParticleSystem" /> values - a fountain, bursts of fireworks and falling
/// snow - drawn into the frame with <see cref="DrawingSession.DrawParticles" /> at each frame's time. The systems
/// are pure functions of time (nothing is stepped), so any frame can be drawn in any order and both packages place
/// every particle identically; only the dots' anti-aliased edges are drawn by each package's canvas.
/// </summary>
internal sealed class ParticleScene : DemoScene
{
    private readonly DrawingSession frame;
    private readonly DrawingLayer particles;
    private readonly DrawingLayer notes;
    private readonly DrawingParticleSystem fountain = new DrawingParticleSystem(1);
    private readonly DrawingParticleSystem fireworks = new DrawingParticleSystem(2);
    private readonly DrawingParticleSystem snow = new DrawingParticleSystem(3);

    /// <summary>Sets up the three systems and the frame.</summary>
    public ParticleScene()
    {
        fountain.AddEmitter(new DrawingParticleEmitter
        {
            X = 250f, Y = 640f, Rate = 90f, Lifetime = 2.2, LifetimeVariation = 0.6, Direction = -90f, Spread = 26f,
            Speed = 330f, SpeedVariation = 60f, GravityY = 260f, StartSize = 12f, EndSize = 4f, SizeVariation = 0.3f,
            StartOpacity = 1f, EndOpacity = 0f, StartColor = Color.FromRgb(140, 220, 255), EndColor = Color.FromRgb(40, 90, 255),
        });

        Color[] colours = { Color.FromRgb(255, 200, 90), Color.FromRgb(255, 90, 140), Color.FromRgb(150, 255, 160), Color.FromRgb(200, 160, 255) };
        for (int i = 0; i < 6; i++)
        {
            fireworks.AddEmitter(new DrawingParticleEmitter
            {
                X = 560f + ((i * 137) % 420), Y = 200f + ((i * 71) % 160), Burst = 110, StartTime = 0.3 + (i * 1.25), Lifetime = 1.7,
                LifetimeVariation = 0.5, Spread = 360f, Speed = 170f, SpeedVariation = 70f, GravityY = 70f,
                StartSize = 9f, EndSize = 3f, StartOpacity = 1f, EndOpacity = 0f, StartColor = Color.White, EndColor = colours[i % colours.Length],
            });
        }

        snow.AddEmitter(new DrawingParticleEmitter
        {
            X = 1130f, Y = 40f, SpawnRadius = 110f, Rate = 35f, StartTime = -6.0, Lifetime = 7.0, Direction = 90f, Spread = 23f,
            Speed = 70f, SpeedVariation = 20f, DriftX = -12f, StartSize = 7f, EndSize = 5f, SizeVariation = 0.4f,
            StartOpacity = 0.95f, EndOpacity = 0.6f, StartColor = Color.FromRgb(240, 245, 255), EndColor = Color.FromRgb(200, 215, 240),
        });

        //The header goes on last, over an opaque band: particles fly up past the top of the content area
        frame = CreateSession(DemoFrame.Width, DemoFrame.Height, Backdrop);
        DrawingLayer sky = frame.AddLayer("Sky", Backdrop);
        frame.ActiveLayer = sky;
        frame.DrawShape(new RectangleShape(0, ContentTop, DemoFrame.Width, DemoFrame.Height - ContentTop, new LinearGradientFill(
            new PointF(0, ContentTop), new PointF(0, DemoFrame.Height), Color.FromRgb(8, 10, 26), Color.FromRgb(30, 26, 52))));
        particles = frame.AddLayer("Particles", Ink);
        notes = frame.AddLayer("Notes", Dim);
        AddHeader(frame, true);
    }

    /// <inheritdoc />
    public override string Name => "particles";

    /// <inheritdoc />
    public override string Title => "Particles";

    /// <inheritdoc />
    public override string Description => "DrawingParticleSystem: particles as a pure function of time; session.DrawParticles draws them as soft dots";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        particles.Clear();
        frame.ActiveLayer = particles;
        ParticlesShape a = frame.DrawParticles(fountain, seconds);
        ParticlesShape b = frame.DrawParticles(fireworks, seconds, softness: 0.6f);
        ParticlesShape c = frame.DrawParticles(snow, seconds, softness: 0f);

        notes.Clear();
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "fountain: {0} live, softness 1", a.Particles.Count), 250, 690, 14, Dim, TextAlign.Center);
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "fireworks: {0} live, bursts, softness 0.6", b.Particles.Count), 760, 690, 14, Dim, TextAlign.Center);
        Label(notes, frame, string.Format(CultureInfo.InvariantCulture, "snow: {0} live, softness 0", c.Particles.Count), 1130, 690, 14, Dim, TextAlign.Center);
        ExportFrame(frame, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => frame.Dispose();
}
