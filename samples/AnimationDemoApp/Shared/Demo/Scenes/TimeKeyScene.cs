using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Time-keyed elements: 480 threads of a spirograph are added ONCE, each with a <see cref="DrawingElement.StartTime" />
/// (and every seventh with an <see cref="DrawingElement.EndTime" /> a moment later, so it flashes and goes). Every
/// frame is only <c>session.Time = t</c>; seeking backwards needs no rebuilding, and because the threads are in
/// the order they appear, the live view's layer cache draws only the newly arrived ones while playing forward. The
/// pen and the counts are <see cref="AnimatedShape" /> values rebuilt from the time.
/// </summary>
internal sealed class TimeKeyScene : DemoScene
{
    private const int ThreadCount = 480;
    private const double DrawSeconds = 7.2;
    private static readonly PointF Center = new PointF(640, 410);

    private readonly DrawingSession session;
    private readonly DrawingLayer threads;

    /// <summary>Builds every thread up front, each keyed to the moment it appears.</summary>
    public TimeKeyScene()
    {
        session = CreateFrameSession();
        threads = session.AddLayer("Threads", Ink);
        ColorRamp ramp = new ColorRamp(Color.FromRgb(246, 112, 168), Color.FromRgb(232, 146, 92), Color.FromRgb(156, 122, 248));
        for (int k = 0; k < ThreadCount; k++)
        {
            double appear = k * DrawSeconds / ThreadCount;
            (PointF a, PointF b) = Ends(appear);
            bool flash = k % 7 == 3;
            var line = new LineShape(a.X, a.Y, b.X, b.Y, flash ? 3 : 1.2f, ramp.At(k / (double)ThreadCount, flash ? (byte)255 : (byte)90))
            {
                StartTime = appear,
                EndTime = flash ? appear + 1.2 : null,
            };
            threads.AddShape(line);
        }

        DrawingLayer now = session.AddLayer("Now", Accent);
        now.AddShape(new AnimatedShape(t =>
        {
            if (t > DrawSeconds) return null;
            (PointF a, PointF b) = Ends(t);
            var pen = new DrawingGroup(Accent);
            pen.Add(new LineShape(a.X, a.Y, b.X, b.Y, 3));
            pen.Add(new CircleShape(a.X, a.Y, 7, 1, null, isFilled: true));
            pen.Add(new CircleShape(b.X, b.Y, 7, 1, null, isFilled: true));
            return pen;
        }));
        now.AddShape(new AnimatedShape(t => new TextShape(
            string.Format(CultureInfo.InvariantCulture, "{0} threads on the layer, {1} existing at {2:0.00} s", ThreadCount, CountVisible(t), t),
            32, 690, 16, session.Fonts, Ink)));
    }

    /// <inheritdoc />
    public override string Name => "time-keys";

    /// <inheritdoc />
    public override string Title => "Time-keyed elements";

    /// <inheritdoc />
    public override string Description => "DrawingElement.StartTime/EndTime: add everything once, set session.Time; seeking back needs no rebuild; appends stay incremental live";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    //The two ends of the thread drawn at a time: two points circling the centre at different speeds
    private static (PointF A, PointF B) Ends(double seconds)
    {
        double a = seconds / DrawSeconds * 2 * Math.PI;
        var outer = new PointF(Center.X + (270 * (float)Math.Cos(a * 3)), Center.Y + (270 * (float)Math.Sin(a * 3)));
        var inner = new PointF(Center.X + (110 * (float)Math.Cos(-a * 7)), Center.Y + (110 * (float)Math.Sin(-a * 7)));
        return (outer, inner);
    }

    private int CountVisible(double seconds)
    {
        int count = 0;
        foreach (DrawingElement element in threads.GetElements())
        {
            if (element.IsVisibleAt(seconds)) count++;
        }

        return count;
    }
}
