using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// The camera: a <see cref="DrawingCamera" /> pans along a landscape, zooms in and rolls a little, all from keyframe
/// tracks. Each layer follows it by its own <see cref="DrawingLayer.Parallax" /> - the stars hardly move (0.15),
/// the far hills a little (0.4), the near hills more (0.75), the ground fully (1) - while the header and the read-out
/// are fixed to the frame (0), like a HUD.
/// </summary>
internal sealed class CameraScene : DemoScene
{
    private readonly DrawingSession session;

    /// <summary>Builds the landscape layers and the camera's tracks.</summary>
    public CameraScene()
    {
        session = CreateSession(DemoFrame.Width, DemoFrame.Height, Color.FromRgb(12, 14, 34));

        DrawingLayer sky = session.AddLayer("Stars", Ink);
        sky.Parallax = 0.15f;
        var random = new DrawingRandom(77);
        for (int i = 0; i < 260; i++)
        {
            float x = (float)random.Uniform(-500, 1900);
            float y = (float)random.Uniform(100, 430);
            sky.AddShape(new CircleShape(x, y, (float)random.Uniform(0.8, 2.2), 1, Color.FromRgba(230, 230, 255, (byte)random.NextInt(80, 230)), isFilled: true));
        }

        AddHills("Far hills", 0.4f, 470, 70, 0.006, Color.FromRgb(40, 44, 92));
        AddHills("Near hills", 0.75f, 540, 60, 0.011, Color.FromRgb(56, 72, 120));

        DrawingLayer ground = session.AddLayer("Ground", Color.FromRgb(30, 34, 50));
        ground.AddShape(new RectangleShape(-1200, 600, 3800, 400, 1, null, isFilled: true));
        for (int i = -10; i < 30; i++)
        {
            float x = i * 110;
            ground.AddShape(new RectangleShape(x - 4, 520, 8, 84, 1, Color.FromRgb(120, 100, 80), isFilled: true));
            ground.AddShape(new CircleShape(x, 512, 22, 1, Color.FromRgb((byte)(80 + ((i * 37) & 63)), 170, 110), isFilled: true));
        }

        DrawingLayer header = AddHeader(session, opaqueBand: true);
        header.Parallax = 0f;
        DrawingLayer hud = session.AddLayer("Hud", Accent);
        hud.Parallax = 0f;
        hud.AddShape(new RectangleShape(0, 664, DemoFrame.Width, 56, 1, Color.FromRgba(12, 14, 34, 200), isFilled: true));
        hud.AddShape(new LineShape(630, 360, 650, 360, 1.5f, Color.FromRgba(255, 176, 72, 160)));
        hud.AddShape(new LineShape(640, 350, 640, 370, 1.5f, Color.FromRgba(255, 176, 72, 160)));
        hud.AddShape(new AnimatedShape(t => new TextShape(Readout(), 32, 690, 16, session.Fonts, Ink)));

        session.Camera = new DrawingCamera();
        session.Animations.Bind(session.Camera, AnimatedProperty.Position, new PointTrack
        {
            { 0, new PointF(-300, 0) },
            { 4.5, new PointF(500, 0), EasingKind.SineInOut },
            { 8, new PointF(-100, 0), EasingKind.SineInOut },
        });
        session.Animations.Bind(session.Camera, AnimatedProperty.Scale, new FloatTrack { { 0, 1f }, { 3, 1f }, { 5, 1.7f, EasingKind.CubicInOut }, { 7.5, 1f, EasingKind.CubicInOut } });
        session.Animations.Bind(session.Camera, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 4, 6f, EasingKind.SineInOut }, { 8, -2f, EasingKind.SineInOut } });
    }

    /// <inheritdoc />
    public override string Name => "camera";

    /// <inheritdoc />
    public override string Title => "Camera and parallax";

    /// <inheritdoc />
    public override string Description => "DrawingSession.Camera (pan, zoom, roll; animated by tracks) and DrawingLayer.Parallax: 0 = HUD, 1 = moves with the scene";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private string Readout()
    {
        DrawingCamera camera = session.Camera;
        return camera == null
            ? string.Empty
            : string.Format(CultureInfo.InvariantCulture, "camera pan {0:0} px, zoom {1:0.00}x, roll {2:0.0} deg   parallax: stars 0.15, far 0.4, near 0.75, ground 1, HUD 0",
                camera.Pan.X, camera.Zoom, camera.Rotation);
    }

    private void AddHills(string name, float parallax, float baseline, float height, double frequency, Color color)
    {
        DrawingLayer layer = session.AddLayer(name, color);
        layer.Parallax = parallax;
        List<PointF> points = new List<PointF> { new PointF(-1400, 760) };
        for (float x = -1400; x <= 2700; x += 20)
        {
            double y = baseline - (height * (0.6 + (0.4 * Math.Sin(x * frequency)) + (0.25 * Math.Sin((x * frequency * 2.7) + 1.3))));
            points.Add(new PointF(x, (float)y));
        }

        points.Add(new PointF(2700, 760));
        layer.AddShape(new PolylineShape(points, 1, null, isClosed: true, isFilled: true));
    }
}
