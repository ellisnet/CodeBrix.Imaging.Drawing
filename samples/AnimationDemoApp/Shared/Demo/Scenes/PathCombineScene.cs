using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Paths as areas, worked out again on every frame. Top: a circle slides across a star and the four boolean
/// combinations of the two are filled (<see cref="PathGeometry.Union" />, <see cref="PathGeometry.Intersect" />,
/// <see cref="PathGeometry.Subtract" />, <see cref="PathGeometry.ExclusiveOr" />). Middle left: a stroke turned into
/// a fillable outline while it draws on (<see cref="PathGeometry.ToStrokeOutline" />) - filled with a gradient, its
/// polygon edge traced in white. Middle right: a star inflated and deflated (<see cref="PathGeometry.Inflate" />),
/// over fixed offset rings. Bottom: outlined Roboto text with a centred stroke - as the font draws it (the lines
/// where a glyph's strokes overlap show) and with <see cref="TextBlockShape.MergeOverlaps" /> (clean outlines).
/// </summary>
internal sealed class PathCombineScene : DemoScene
{
    private const string Signature = "M 60 520 C 110 400, 210 390, 250 470 S 360 570, 420 470 Q 470 390 530 460 T 610 470";
    private static readonly string[] Operations = { "Union", "Intersect", "Subtract", "ExclusiveOr" };
    private static readonly Color Result = Color.FromRgb(255, 176, 72);
    private static readonly Color Input = Color.FromRgba(200, 205, 230, 150);

    private readonly DrawingSession session;

    /// <summary>Builds every shape; the combined ones are animated shapes rebuilt from the time.</summary>
    public PathCombineScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        DrawingLayer art = session.AddLayer("Art", Ink);
        session.ActiveLayer = art;

        for (int i = 0; i < Operations.Length; i++)
        {
            int index = i;
            float cx = 40 + (i * 305) + 140;
            Label(notes, session, "star." + Operations[i] + "(circle)", cx, 350, 15, Ink, TextAlign.Center);
            session.DrawShape(new AnimatedShape(t => Combined(index, cx, t)));
        }

        // Stroke to outline while it draws on.
        PathGeometry signature = PathGeometry.Parse(Signature);
        session.DrawPath(signature, 1.2f, Color.FromRgba(132, 138, 164, 120));
        var gradient = new LinearGradientFill(new PointF(60, 400), new PointF(610, 560), Color.FromRgb(120, 200, 255), Color.FromRgb(236, 90, 160));
        var style = new PathStrokeStyle(26f, PathStrokeCap.Round, PathStrokeJoin.Miter);
        session.DrawShape(new AnimatedShape(t =>
        {
            float end = (float)Math.Clamp((t % 4.0) / 3.0, 0.02, 1.0);
            PathGeometry outline = signature.Trim(0f, end).ToStrokeOutline(style);
            var group = new DrawingGroup();
            group.Add(new PathShape(outline, gradient));
            group.Add(new PathShape(outline, 1.2f, Color.FromRgba(255, 255, 255, 220)));
            return group;
        }));
        Label(notes, session, "Trim(0, t).ToStrokeOutline(26 px, round caps, miter joins) - a fill, its polygon edge in white", 40, 580, 14, Ink);

        // Inflate and deflate.
        PathGeometry star = PathGeometry.Star(new PointF(960, 470), 72, 34, 6);
        foreach (float ring in new[] { -14f, 14f, 30f })
        {
            session.DrawPath(star.Inflate(ring), 1.2f, Color.FromRgba(132, 138, 164, 130));
        }
        session.DrawShape(new AnimatedShape(t =>
        {
            float distance = Distance(t);
            var group = new DrawingGroup();
            PathGeometry grown = star.Inflate(distance, PathStrokeJoin.Miter, 2f);
            if (!grown.IsEmpty) { group.Add(new PathShape(grown, 1f, Color.FromRgba(255, 176, 72, 230), isFilled: true)); }
            group.Add(new PathShape(star, 2f, Ink));
            group.Add(new TextShape(string.Format(CultureInfo.InvariantCulture, "Inflate({0:+0.0;-0.0;0.0}, Miter, 2)", distance),
                1088, 466, 15, session.Fonts, Result));
            return group;
        }));
        Label(notes, session, "rings: Inflate -14, 14, 30", 1088, 490, 13, Dim);

        // Outlined text: the font's overlapping contours, and merged.
        TextLayoutResult text = session.LayoutText("HEAT WAVE", 70);
        var stroke = new PathStrokeStyle(3f);
        session.DrawShape(new TextBlockShape(text, 40, 618, stroke, Ink));
        session.DrawShape(new TextBlockShape(text, 680, 618, stroke, Ink) { MergeOverlaps = true });
        Label(notes, session, "a centred stroke on Roboto: overlap lines show", 40, 610, 14, Dim);
        Label(notes, session, "MergeOverlaps = true: each glyph's contours merged", 680, 610, 14, Dim);
    }

    /// <inheritdoc />
    public override string Name => "path-combine";

    /// <inheritdoc />
    public override string Title => "Combining paths, stroke to outline, inflate";

    /// <inheritdoc />
    public override string Description => "PathGeometry Union / Intersect / Subtract / ExclusiveOr, ToStrokeOutline, Inflate / Deflate and merged glyph outlines - exact integer polygon arithmetic";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    //Grows from -16 (deflated) to +26 and back over the scene
    private static float Distance(double t) => -16f + (42f * (float)PingPong(t, 8));

    //One boolean combination of a star and a circle that slides across it
    private static DrawingShape Combined(int operation, float cx, double t)
    {
        PathGeometry star = PathGeometry.Star(new PointF(cx, 226), 92, 40, 5);
        float offset = -72f + (144f * (float)PingPong(t, 4));
        PathGeometry circle = PathGeometry.Circle(new PointF(cx + offset, 226), 56);
        PathGeometry result = operation switch
        {
            0 => star.Union(circle),
            1 => star.Intersect(circle),
            2 => star.Subtract(circle),
            _ => star.ExclusiveOr(circle),
        };
        var group = new DrawingGroup();
        if (!result.IsEmpty) { group.Add(new PathShape(result, 1f, Result, isFilled: true)); }
        group.Add(new PathShape(star, 1.5f, Input));
        group.Add(new PathShape(circle, 1.5f, Input));
        return group;
    }
}
