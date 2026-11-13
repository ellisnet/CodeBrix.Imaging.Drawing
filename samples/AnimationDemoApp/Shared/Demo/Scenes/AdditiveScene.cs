using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Additive bindings (<see cref="BindingMode" />): several bindings of one property combine as
/// <c>(base x every factor) + every addition</c>, whatever order they were made in. Left: a pendulum's swing (a
/// looping keyframe track, <see cref="BindingMode.Replace" />) plus a flutter (a function of time,
/// <see cref="BindingMode.Add" />) - the flutter is bound FIRST and still adds. Middle: a star's scale track times a
/// pulse (<see cref="BindingMode.Multiply" />). Right: a box going round a path
/// (<see cref="DrawingAnimations.BindAlongPath(DrawingElement, PathGeometry, KeyframeTrack{float}, bool)" />) with
/// shakes added to its position. Faint ghosts carry the base motion alone; under each, a plot of the parts and the
/// result over the scene, and a read-out of the values now (the result read back from the element).
/// </summary>
internal sealed class AdditiveScene : DemoScene
{
    private const float PlotTop = 470, PlotHeight = 90, PlotWidth = 360;
    private static readonly float[] PanelLeft = { 40, 460, 880 };
    private static readonly Color BaseColor = Color.FromRgb(132, 138, 164);
    private static readonly Color PartColor = Color.FromRgb(120, 180, 255);
    private static readonly PointF Pivot = new PointF(220, 185);
    private static readonly PointF StarCenter = new PointF(640, 290);

    private readonly DrawingSession session;
    private readonly FloatTrack swing = new FloatTrack { { 0, -26f }, { 1.3, 26f, EasingKind.SineInOut }, { 2.6, -26f, EasingKind.SineInOut } }.Loop();
    private readonly FloatTrack grow = new FloatTrack { { 0, 0.6f }, { 4, 1.25f, EasingKind.SineInOut }, { 8, 0.6f, EasingKind.SineInOut } };
    private readonly FloatTrack lap = new FloatTrack { { 0, 0f }, { 8, 1f } };
    private readonly PathGeometry loop = PathGeometry.Ellipse(new PointF(1060, 290), 150, 110);
    private readonly DrawingGroup pendulum;
    private readonly DrawingShape star;
    private readonly DrawingShape box;
    private readonly DrawingLayer now;

    /// <summary>Builds the three panels, their ghosts, bindings and plots.</summary>
    public AdditiveScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        session.ActiveLayer = notes;
        Label(notes, session, "rotation: swing (Replace) + flutter (Add)", PanelLeft[0], 128, 14, Ink);
        Label(notes, session, "the flutter is bound before the swing", PanelLeft[0], 148, 12, Dim);
        Label(notes, session, "scale: grow track (Replace) x pulse (Multiply)", PanelLeft[1], 128, 14, Ink);
        Label(notes, session, "the ghost outline is the track alone", PanelLeft[1], 148, 12, Dim);
        Label(notes, session, "position: BindAlongPath + shake (Add)", PanelLeft[2], 128, 14, Ink);
        Label(notes, session, "the ghost box is the path alone", PanelLeft[2], 148, 12, Dim);
        session.DrawShape(new PathShape(loop, new PathStrokeStyle(1.5f, dashPattern: new[] { 6f, 6f }), Color.FromRgba(132, 138, 164, 110)));
        session.DrawShape(new CircleShape(Pivot.X, Pivot.Y, 5, 1, Dim, isFilled: true));

        DrawingLayer ghosts = session.AddLayer("Ghosts", Ink);
        DrawingLayer things = session.AddLayer("Things", Ink);

        // Left: a pendulum drawn hanging from (0, 0) and placed at the pivot.
        DrawingGroup ghostPendulum = Pendulum(Color.FromRgba(132, 138, 164, 70), Color.FromRgba(132, 138, 164, 70));
        ghosts.AddShape(ghostPendulum);
        session.Animations.Bind(ghostPendulum, AnimatedProperty.Rotation, swing);
        pendulum = Pendulum(Ink, Accent);
        things.AddShape(pendulum);
        session.Animations.Bind(pendulum, AnimatedProperty.Rotation, t => Flutter(t), BindingMode.Add);
        session.Animations.Bind(pendulum, AnimatedProperty.Rotation, swing);

        // Middle: a star scaled about its centre.
        PathGeometry starPath = PathGeometry.Star(StarCenter, 100, 44, 5);
        var ghostStar = new PathShape(starPath, new PathStrokeStyle(2f, dashPattern: new[] { 6f, 5f }), Color.FromRgba(132, 138, 164, 160))
        {
            Transform = new DrawingTransform(new PointF(0, 0), anchor: StarCenter),
        };
        ghosts.AddShape(ghostStar);
        session.Animations.Bind(ghostStar, AnimatedProperty.Scale, grow);
        star = new PathShape(starPath, Color.FromRgb(255, 176, 72)) { Transform = new DrawingTransform(new PointF(0, 0), anchor: StarCenter) };
        things.AddShape(star);
        session.Animations.Bind(star, AnimatedProperty.Scale, grow);
        session.Animations.Bind(star, AnimatedProperty.Scale, t => Pulse(t), BindingMode.Multiply);

        // Right: a box (drawn round (0, 0)) going round the loop; shakes added on top.
        var ghostBox = new RectangleShape(-22, -22, 44, 44, 2, Color.FromRgba(132, 138, 164, 150), cornerRadius: 6);
        ghosts.AddShape(ghostBox);
        session.Animations.BindAlongPath(ghostBox, loop, lap);
        box = new RectangleShape(-22, -22, 44, 44, 1, Color.FromRgb(120, 180, 255), isFilled: true, cornerRadius: 6);
        things.AddShape(box);
        session.Animations.Bind(box, AnimatedProperty.Position, t => Shake(t), BindingMode.Add);
        session.Animations.BindAlongPath(box, loop, lap);

        // The plots: base, part and result over the scene's 8 seconds.
        Plot(notes, 0, t => swing.Evaluate(t), t => Flutter(t), (b, p) => b + p, -40, 40, "swing", "flutter", "sum");
        Plot(notes, 1, t => grow.Evaluate(t), t => Pulse(t), (b, p) => b * p, 0.4f, 1.6f, "grow", "pulse", "product");
        Plot(notes, 2, t => 0f, t => Shake(t).X, (b, p) => p, -12, 12, "", "shake x", "");

        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "additive";

    /// <inheritdoc />
    public override string Title => "Additive bindings: motions that add up";

    /// <inheritdoc />
    public override string Description => "BindingMode Replace / Multiply / Add - (base x factors) + additions, in any binding order; tracks and functions of time";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        now.Clear();
        for (int i = 0; i < 3; i++)
        {
            float x = PanelLeft[i] + (PlotWidth * (float)(seconds / 8.0));
            now.AddShape(new LineShape(x, PlotTop - 4, x, PlotTop + PlotHeight + 4, 2, Ink));
        }
        PointF shake = Shake(seconds);
        PointF onPath = loop.GetPointAt(lap.Evaluate(seconds)).Position;
        PointF at = box.Transform.Translation;
        Readout(0, Signed(swing.Evaluate(seconds)) + " + " + Signed(Flutter(seconds)) + " = " + Signed(pendulum.Transform.Rotation) + " deg");
        Readout(1, string.Format(CultureInfo.InvariantCulture, "{0:0.000} x {1:0.000} = {2:0.000}",
            grow.Evaluate(seconds), Pulse(seconds), star.Transform.ScaleX));
        Readout(2, string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}) + ({2}, {3}) = ({4:0.0}, {5:0.0})",
            onPath.X, onPath.Y, Signed(shake.X), Signed(shake.Y), at.X, at.Y));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    //A value with its sign, rounded to one decimal (a rounded negative zero shows as 0.0)
    private static string Signed(float value)
        => (Math.Round(value, 1) + 0.0).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);

    //A quick flutter that swells and fades twice over the scene
    private static float Flutter(double t) => (float)(7 * Math.Sin(2 * Math.PI * 3.2 * t) * Math.Pow(Math.Sin(Math.PI * t / 4), 2));

    //A heartbeat-like pulse about 1
    private static float Pulse(double t) => (float)(1 + (0.16 * Math.Sin(2 * Math.PI * 1.5 * t)));

    //Two bursts of shaking, each rising and dying away over a second
    private static PointF Shake(double t)
    {
        double envelope = 0;
        if (t >= 2 && t < 3) envelope = Math.Sin(Math.PI * (t - 2));
        else if (t >= 5 && t < 6) envelope = Math.Sin(Math.PI * (t - 5));
        return new PointF((float)(10 * envelope * Math.Sin(2 * Math.PI * 13 * t)), (float)(10 * envelope * Math.Cos(2 * Math.PI * 17 * t)));
    }

    private static DrawingGroup Pendulum(Color rod, Color bob)
    {
        var group = new DrawingGroup();
        group.Add(new LineShape(0, 0, 0, 178, 3, rod));
        group.Add(new CircleShape(0, 200, 24, 1, bob, isFilled: true));
        group.Transform = new DrawingTransform(Pivot);
        return group;
    }

    private void Readout(int panel, string text)
        => now.AddShape(new TextShape(text, PanelLeft[panel], PlotTop + PlotHeight + 50, 16, session.Fonts, Accent));

    //A small plot of a base curve, a part and their combination
    private void Plot(DrawingLayer layer, int panel, Func<double, float> baseAt, Func<double, float> partAt, Func<float, float, float> combine,
        float low, float high, string baseName, string partName, string resultName)
    {
        float left = PanelLeft[panel];
        layer.AddShape(new RectangleShape(left, PlotTop, PlotWidth, PlotHeight, 1, Color.FromRgb(28, 31, 44), isFilled: true, cornerRadius: 4));
        var baseLine = new List<PointF>();
        var partLine = new List<PointF>();
        var resultLine = new List<PointF>();
        for (int i = 0; i <= 480; i++)
        {
            double t = i * 8.0 / 480;
            float x = left + (PlotWidth * i / 480f);
            float b = baseAt(t), p = partAt(t);
            baseLine.Add(new PointF(x, Y(b)));
            partLine.Add(new PointF(x, Y(p)));
            resultLine.Add(new PointF(x, Y(combine(b, p))));
        }
        if (baseName.Length > 0) layer.AddShape(new PolylineShape(baseLine, 1.5f, BaseColor));
        layer.AddShape(new PolylineShape(partLine, 1.5f, PartColor));
        if (resultName.Length > 0) layer.AddShape(new PolylineShape(resultLine, 2f, Accent));

        float key = PlotTop + PlotHeight + 22;
        float x0 = left;
        if (baseName.Length > 0) { Label(layer, session, baseName, x0, key, 13, BaseColor); x0 += 70; }
        Label(layer, session, partName, x0, key, 13, PartColor);
        x0 += 80;
        if (resultName.Length > 0) Label(layer, session, resultName, x0, key, 13, Accent);

        float Y(float value) => PlotTop + PlotHeight - (PlotHeight * (value - low) / (high - low));
    }
}
