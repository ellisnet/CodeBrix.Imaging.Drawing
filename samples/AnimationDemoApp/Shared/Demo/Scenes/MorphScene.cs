using System;
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
/// Path morphing. Left: a filled shape morphing circle -> star -> heart -> rounded square -> circle, one
/// <see cref="PathMorph" /> per step shown by a <see cref="PathShape" /> that exists only during its step (a time
/// window) with its MorphFraction bound to an eased track. Right: a countdown 3 -> 2 -> 1 -> 0 morphing the digits'
/// glyph outlines (<see cref="TextShape.GetOutline" />) - the "0" has a second contour, which grows out of its own
/// centre, as the correspondence rule says. The in-between's resampled points are marked.
/// </summary>
internal sealed class MorphScene : DemoScene
{
    private const double Step = 2.0;

    private readonly DrawingSession session;

    /// <summary>Builds the morph chains.</summary>
    public MorphScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "one PathMorph per step; MorphFraction bound to a track", 330, 650, 14, Dim, TextAlign.Center);
        Label(notes, session, "glyph outlines morphing; an extra contour grows from its centre", 950, 650, 14, Dim, TextAlign.Center);

        var center = new PointF(330, 390);
        PathGeometry circle = PathGeometry.Circle(center, 170);
        PathGeometry star = PathGeometry.Star(center, 200, 85, 5);
        PathGeometry heart = PathGeometry.Parse("M 330 560 C 120 430, 130 230, 260 230 C 300 230, 320 260, 330 285 C 340 260, 360 230, 400 230 C 530 230, 540 430, 330 560 Z");
        PathGeometry square = PathGeometry.RoundedRectangle(new RectangleF(170, 230, 320, 320), 40);
        var fill = new RadialGradientFill(new PointF(290, 330), 280, Color.FromRgb(255, 214, 120), Color.FromRgb(214, 70, 110));
        var outline = new PathStrokeStyle(4f);

        DrawingLayer shapes = session.AddLayer("Shapes", Ink);
        session.ActiveLayer = shapes;
        PathGeometry[] chain = { circle, star, heart, square, circle };
        for (int i = 0; i + 1 < chain.Length; i++)
        {
            var morph = new PathMorph(chain[i], chain[i + 1]);
            var shape = new PathShape(morph, 0f, outline, Color.FromRgb(255, 240, 220), null, fill)
            {
                StartTime = i == 0 ? null : i * Step,
                EndTime = i == chain.Length - 2 ? null : (i + 1) * Step,
            };
            session.DrawShape(shape);
            session.Animations.Bind(shape, AnimatedProperty.MorphFraction, new FloatTrack
            {
                { (i * Step) + 0.4, 0f },
                { ((i + 1) * Step) - 0.2, 1f, EasingKind.CubicInOut },
            });
        }

        // The countdown, from the glyph outlines of the bundled font, centred on (950, 420).
        DrawingLayer digits = session.AddLayer("Digits", Color.FromRgb(120, 180, 255));
        session.ActiveLayer = digits;
        PathGeometry[] glyphs = new PathGeometry[4];
        string[] texts = { "3", "2", "1", "0" };
        for (int i = 0; i < texts.Length; i++)
        {
            glyphs[i] = new TextShape(texts[i], 950, 540, 360, session.Fonts, align: TextAlign.Center).GetOutline();
        }

        for (int i = 0; i + 1 < glyphs.Length; i++)
        {
            var morph = new PathMorph(glyphs[i], glyphs[i + 1]);
            var shape = new PathShape(morph, 0f, new PathStrokeStyle(5f), Color.FromRgb(120, 180, 255))
            {
                StartTime = i == 0 ? null : i * Step,
                EndTime = i == glyphs.Length - 2 ? null : (i + 1) * Step,
            };
            session.DrawShape(shape);
            var progress = new FloatTrack
            {
                { (i * Step) + 0.6, 0f },
                { ((i + 1) * Step) - 0.1, 1f, EasingKind.QuartInOut },
            };
            session.Animations.Bind(shape, AnimatedProperty.MorphFraction, progress);

            // The resampled points of this step's in-between, as small dots (shown while it morphs).
            int step = i;
            var dots = new AnimatedShape(t =>
            {
                float fraction = progress.Evaluate(t);
                if (fraction <= 0f || fraction >= 1f) return null;
                var group = new DrawingGroup(Color.FromRgba(255, 176, 72, 200));
                PathGeometry inBetween = morph.GetPath(fraction);
                foreach (PathContour contour in inBetween.Contours)
                {
                    for (int k = 0; k < contour.SegmentCount; k += 20) group.Add(new CircleShape(contour[k].Start.X, contour[k].Start.Y, 4f, 1, null, isFilled: true));
                }

                return group;
            })
            {
                StartTime = step == 0 ? null : step * Step,
                EndTime = step == glyphs.Length - 2 ? null : (step + 1) * Step,
            };
            session.DrawShape(dots);
        }
    }

    /// <inheritdoc />
    public override string Name => "morph";

    /// <inheritdoc />
    public override string Title => "Path morphing";

    /// <inheritdoc />
    public override string Description => "PathMorph (contours paired, resampled by length, best start and direction), PathShape.MorphFraction, TextShape.GetOutline";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
