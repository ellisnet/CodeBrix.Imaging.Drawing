using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Asking a path about points. Left: a star with a hole. A field of dots behind it is coloured by
/// <see cref="PathGeometry.GetSignedDistance" /> (blue inside, warm outside, banded every 20 units). Probes wander
/// over it: each is green inside and red outside (<see cref="PathGeometry.Contains" />), has a ring whose radius is
/// its <see cref="PathGeometry.DistanceToEdge" /> (so it just touches the edge) and a line to
/// <see cref="PathGeometry.GetNearestPoint" />. Right: a circle orbits a rounded hexagon and the gap between the two
/// areas is <see cref="PathGeometry.Distance(PathGeometry, PointF, float)" /> - the circle is one fixed geometry
/// asked about at an offset - drawn as a line between their nearest points, 0 when they touch.
/// </summary>
internal sealed class PathQueriesScene : DemoScene
{
    private static readonly PointF StarCenter = new PointF(320, 405);
    private static readonly PointF HexCenter = new PointF(960, 405);
    private static readonly Color Inside = Color.FromRgb(110, 220, 140);
    private static readonly Color Outside = Color.FromRgb(240, 90, 100);
    private static readonly Color InsideRing = Color.FromRgba(110, 220, 140, 150);
    private static readonly Color OutsideRing = Color.FromRgba(240, 90, 100, 150);
    private const float OrbitRadius = 50;

    private readonly DrawingSession session;
    private readonly PathGeometry star = PathGeometry.Star(StarCenter, 220, 100, 5).Subtract(PathGeometry.Circle(StarCenter, 46));
    private readonly PathGeometry hexagon;
    private readonly PathGeometry orbiter = PathGeometry.Circle(new PointF(0, 0), OrbitRadius);
    private readonly DrawingLayer now;

    /// <summary>Builds the shapes and the signed-distance field.</summary>
    public PathQueriesScene()
    {
        session = CreateFrameSession();
        DrawingLayer field = session.AddLayer("Field", Dim);
        session.ActiveLayer = field;

        for (float y = 120; y <= 690; y += 15)
        {
            for (float x = 30; x <= 610; x += 15)
            {
                float d = star.GetSignedDistance(new PointF(x, y));
                int band = (int)Math.Floor(Math.Abs(d) / 20f);
                float fade = Math.Max(0.15f, 1f - (band * 0.16f));
                Color color = d < 0
                    ? Color.FromRgba(90, 150, 255, (byte)(200 * fade))
                    : Color.FromRgba(255, 150, 80, (byte)(150 * fade));
                field.AddShape(new CircleShape(x, y, band % 2 == 0 ? 2.4f : 1.4f, 1, color, isFilled: true));
            }
        }
        session.DrawShape(new PathShape(star, 2f, Ink));

        var corners = new PointF[6];
        for (int i = 0; i < 6; i++)
        {
            double a = (i * Math.PI / 3) + (Math.PI / 6);
            corners[i] = new PointF(HexCenter.X + (float)(115 * Math.Cos(a)), HexCenter.Y + (float)(115 * Math.Sin(a)));
        }
        hexagon = PathGeometry.RoundedPolygon(corners, 18);
        session.DrawShape(new PathShape(hexagon, Color.FromRgb(70, 96, 160)));
        session.DrawShape(new PathShape(hexagon, 2f, Color.FromRgb(140, 170, 240)));

        Label(field, session, "Contains (green inside / red outside), DistanceToEdge (ring), GetNearestPoint (line); dots: GetSignedDistance", 30, 118, 13, Dim);
        Label(field, session, "hexagon.Distance(circle, offset): the gap between the two areas", 700, 118, 13, Dim);

        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "path-queries";

    /// <inheritdoc />
    public override string Title => "Path queries: inside, edge distance, nearest point, gap";

    /// <inheritdoc />
    public override string Description => "PathGeometry.Contains, DistanceToEdge, GetSignedDistance, GetNearestPoint and Distance(other, offset) - shared arithmetic, identical in both packages";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        now.Clear();

        // The probes: Lissajous wanderers over the star.
        for (int i = 0; i < 4; i++)
        {
            double phase = seconds * 2 * Math.PI / 8;
            var probe = new PointF(
                StarCenter.X + (float)(215 * Math.Sin((phase * (1 + i)) + (i * 1.3))),
                StarCenter.Y + (float)(200 * Math.Sin((phase * (2 + (i % 2))) + (i * 0.7))));
            bool inside = star.Contains(probe);
            float edge = star.DistanceToEdge(probe);
            PointF nearest = star.GetNearestPoint(probe);
            Color color = inside ? Inside : Outside;
            if (edge > 1f) now.AddShape(new CircleShape(probe.X, probe.Y, edge, 1.5f, inside ? InsideRing : OutsideRing));
            now.AddShape(new LineShape(probe.X, probe.Y, nearest.X, nearest.Y, 1.5f, Ink));
            now.AddShape(new CircleShape(nearest.X, nearest.Y, 3.5f, 1, Ink, isFilled: true));
            now.AddShape(new CircleShape(probe.X, probe.Y, 7, 1, color, isFilled: true));
            now.AddShape(new TextShape(star.GetSignedDistance(probe).ToString("+0.0;-0.0", CultureInfo.InvariantCulture),
                probe.X + 11, probe.Y - 9, 14, session.Fonts, color));
        }

        // The orbiting circle: one geometry round (0, 0), asked about at an offset.
        double angle = seconds * 2 * Math.PI / 8;
        float reach = 190 + (75 * (float)Math.Sin(seconds * 2 * Math.PI / 2.6));
        var offset = new PointF(HexCenter.X + (reach * (float)Math.Cos(angle)), HexCenter.Y + (0.8f * reach * (float)Math.Sin(angle)));
        float gap = hexagon.Distance(orbiter, offset);
        bool touching = gap <= 0f;
        Color circleColor = touching ? Color.FromRgba(240, 90, 100, 200) : Color.FromRgba(255, 176, 72, 170);
        now.AddShape(new CircleShape(offset.X, offset.Y, OrbitRadius, 1, circleColor, isFilled: true));
        if (!touching)
        {
            // The nearest pair, found by stepping between the two shapes' nearest points (both are convex).
            PointF onCircle = offset;
            PointF onHex = hexagon.GetNearestPoint(onCircle);
            for (int i = 0; i < 8; i++)
            {
                PointF local = orbiter.GetNearestPoint(new PointF(onHex.X - offset.X, onHex.Y - offset.Y));
                onCircle = new PointF(local.X + offset.X, local.Y + offset.Y);
                onHex = hexagon.GetNearestPoint(onCircle);
            }
            now.AddShape(new LineShape(onHex.X, onHex.Y, onCircle.X, onCircle.Y, 2, Ink));
            now.AddShape(new CircleShape(onHex.X, onHex.Y, 4, 1, Ink, isFilled: true));
            now.AddShape(new CircleShape(onCircle.X, onCircle.Y, 4, 1, Ink, isFilled: true));
            // The number beside the gap line's middle, on the side away from the hexagon's centre.
            float dx = onCircle.X - onHex.X, dy = onCircle.Y - onHex.Y, length = MathF.Max(1f, MathF.Sqrt((dx * dx) + (dy * dy)));
            float nx = -dy / length, ny = dx / length;
            float mx = (onHex.X + onCircle.X) / 2, my = (onHex.Y + onCircle.Y) / 2;
            if ((nx * (mx - HexCenter.X)) + (ny * (my - HexCenter.Y)) < 0) { nx = -nx; ny = -ny; }
            now.AddShape(new TextShape(gap.ToString("0.0", CultureInfo.InvariantCulture), mx + (nx * 22), my + (ny * 22) + 5, 15, session.Fonts, Ink, TextAlign.Center));
        }
        string readout = touching
            ? "Distance = 0 - the areas touch or overlap"
            : string.Format(CultureInfo.InvariantCulture, "Distance = {0:0.00}", gap);
        now.AddShape(new TextShape(readout, 960, 690, 18, session.Fonts, touching ? Outside : Accent, TextAlign.Center));

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
