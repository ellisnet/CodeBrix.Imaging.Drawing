using System;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Transforms, opacity and groups. Left: element transforms - a bar turning about its end, a square scaling about
/// its centre, an arrow fading as one unit - each with its transformed bounds outlined. Middle (exported over
/// transparency, laid on a checkerboard): two overlapping discs faded as separate elements (the overlap shows
/// through) against the same discs faded as one <see cref="DrawingGroup" /> (it does not). Right: a whole layer
/// turned and scaled by its <see cref="DrawingLayer.Transform" />.
/// </summary>
internal sealed class TransformScene : DemoScene
{
    private const int PanelWidth = 360;
    private const int PanelHeight = 520;
    private const int PanelX = 460;
    private const int PanelY = 150;

    private readonly DrawingSession frame;
    private readonly DrawingLayer bounds;
    private readonly DrawingShape bar;
    private readonly DrawingShape square;
    private readonly DrawingShape arrow;
    private readonly DrawingSession panel;
    private readonly DrawingPlaneImage checkerboard = CreateCheckerboard(PanelWidth, PanelHeight);
    private readonly byte[] board = new byte[PanelWidth * PanelHeight * 4];
    private readonly byte[] panelPixels = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Builds the three demonstrations and their bindings.</summary>
    public TransformScene()
    {
        frame = CreateFrameSession();
        DrawingLayer notes = frame.AddLayer("Notes", Dim);
        Label(notes, frame, "element transforms (bounds outlined)", 40, 140, 14, Dim);
        Label(notes, frame, "faded elements | faded group (over transparency)", PanelX, 140, 14, Dim);
        Label(notes, frame, "a layer transform (the whole layer)", 880, 140, 14, Dim);

        DrawingLayer shapes = frame.AddLayer("Shapes", Accent);
        frame.ActiveLayer = shapes;
        bar = frame.DrawRectangle(200, 281, 110, 18, filled: true, color: Color.FromRgb(120, 170, 255));
        frame.Animations.Bind(bar, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(200, 290) } });
        frame.Animations.Bind(bar, AnimatedProperty.Rotation, Looping(new FloatTrack { { 0, 0f }, { 4, 360f } }, TrackLoopMode.Loop));

        square = frame.DrawRectangle(170, 430, 80, 80, filled: true, color: Color.FromRgb(140, 230, 160), cornerRadius: 10);
        frame.Animations.Bind(square, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(210, 470) } });
        frame.Animations.Bind(square, AnimatedProperty.Scale, Looping(new FloatTrack { { 0, 0.5f }, { 2, 1.4f, EasingKind.SineInOut } }, TrackLoopMode.PingPong));
        frame.Animations.Bind(square, AnimatedProperty.Rotation, Looping(new FloatTrack { { 0, -20f }, { 2, 20f, EasingKind.SineInOut } }, TrackLoopMode.PingPong));

        arrow = frame.DrawArrow(80, 660, 330, 620, thickness: 16, color: Color.FromRgba(255, 120, 150, 255), headLength: 60);
        frame.Animations.Bind(arrow, AnimatedProperty.Opacity, Looping(new FloatTrack { { 0, 1f }, { 2, 0.25f, EasingKind.SineInOut } }, TrackLoopMode.PingPong));

        DrawingLayer orbit = frame.AddLayer("Orbit", Color.FromRgb(255, 210, 120));
        frame.ActiveLayer = orbit;
        frame.DrawCircle(1050, 410, 26, filled: true);
        for (int i = 0; i < 6; i++)
        {
            double angle = i * Math.PI / 3;
            frame.DrawCircle(1050 + (130 * (float)Math.Cos(angle)), 410 + (130 * (float)Math.Sin(angle)), 14, filled: true,
                color: Color.FromRgb((byte)(120 + (i * 20)), 170, 255));
        }

        frame.DrawText("layer", 1050, 560, 18, Ink, TextAlign.Center);
        frame.Animations.Bind(orbit, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(1050, 410) } });
        frame.Animations.Bind(orbit, AnimatedProperty.Rotation, Looping(new FloatTrack { { 0, 0f }, { 8, 360f } }, TrackLoopMode.Loop));
        frame.Animations.Bind(orbit, AnimatedProperty.Scale, Looping(new FloatTrack { { 0, 0.8f }, { 2, 1.1f, EasingKind.SineInOut } }, TrackLoopMode.PingPong));

        bounds = frame.AddLayer("Bounds", Color.FromRgba(236, 236, 244, 120));

        // The faded discs, in a session of their own exported over transparency.
        panel = CreateSession(PanelWidth, PanelHeight, Color.Transparent);
        panel.AddLayer("Discs", Color.FromRgb(255, 176, 72));
        DrawingShape left = panel.DrawCircle(130, 150, 80, filled: true);
        DrawingShape right = panel.DrawCircle(230, 150, 80, filled: true, color: Color.FromRgb(120, 170, 255));
        var pair = new DrawingGroup();
        pair.Add(new CircleShape(130, 380, 80, 1, null, isFilled: true));
        pair.Add(new CircleShape(230, 380, 80, 1, Color.FromRgb(120, 170, 255), isFilled: true));
        panel.DrawShape(pair);
        FloatTrack fade = Looping(new FloatTrack { { 0, 1f }, { 2, 0.35f, EasingKind.SineInOut } }, TrackLoopMode.PingPong);
        panel.Animations.Bind(left, AnimatedProperty.Opacity, fade);
        panel.Animations.Bind(right, AnimatedProperty.Opacity, fade);
        panel.Animations.Bind(pair, AnimatedProperty.Opacity, fade);
        checkerboard.ToRgba(board);
    }

    /// <inheritdoc />
    public override string Name => "transforms";

    /// <inheritdoc />
    public override string Title => "Transforms, opacity and groups";

    /// <inheritdoc />
    public override string Description => "DrawingElement.Transform/Opacity, DrawingGroup (fades as one unit), DrawingLayer.Transform; GetTransformedBounds";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        frame.Time = seconds;
        bounds.Clear();
        foreach (DrawingShape shape in new[] { bar, square, arrow })
        {
            RectangleF? box = shape.GetTransformedBounds();
            if (box.HasValue) bounds.AddShape(new RectangleShape(box.Value.X, box.Value.Y, box.Value.Width, box.Value.Height, 1.5f));
        }

        ExportFrame(frame, rgba);
        panel.Time = seconds;
        panel.ExportPixels(panelPixels, new Size(PanelWidth, PanelHeight), includeBackground: false);
        FrameCompositor.Copy(board, PanelWidth, PanelHeight, rgba, PanelX, PanelY);
        FrameCompositor.Over(panelPixels, PanelWidth, PanelHeight, rgba, PanelX, PanelY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        frame.Dispose();
        panel.Dispose();
    }

    private static FloatTrack Looping(FloatTrack track, TrackLoopMode mode)
    {
        track.LoopMode = mode;
        return track;
    }
}
