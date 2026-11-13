using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Every easing curve of the library at once: the named curves of <see cref="EasingKind" />, three
/// <see cref="Easing.Steps(double, int, StepPosition)" /> staircases and the four CSS keyword
/// <see cref="CubicBezierEasing" /> curves, each drawn as a graph with a dot running along it, and a second dot
/// sliding along the bottom of its panel at the eased position.
/// </summary>
internal sealed class EasingScene : DemoScene
{
    private const int Columns = 8;
    private const float PanelWidth = 146;
    private const float PanelHeight = 116;
    private const float Gap = 6;
    private const float Left = 32;
    private const float GraphLow = -0.35f;
    private const float GraphHigh = 1.35f;
    private const double RunSeconds = 2.0;
    private const double PauseSeconds = 0.6;

    private readonly List<(string Label, Func<double, double> Curve)> curves = new List<(string, Func<double, double>)>();
    private readonly DrawingSession session;
    private readonly DrawingLayer moving;

    /// <summary>Builds the graphs (drawn once; only the dots move).</summary>
    public EasingScene()
    {
        foreach (EasingKind kind in Enum.GetValues<EasingKind>()) curves.Add((kind.ToString(), Easing.GetFunction(kind)));
        curves.Add(("Steps(4, End)", t => Easing.Steps(t, 4)));
        curves.Add(("Steps(4, Start)", t => Easing.Steps(t, 4, StepPosition.Start)));
        curves.Add(("Steps(3, Both)", t => Easing.Steps(t, 3, StepPosition.Both)));
        curves.Add(("CSS ease", CubicBezierEasing.Ease.Evaluate));
        curves.Add(("CSS ease-in", CubicBezierEasing.EaseIn.Evaluate));
        curves.Add(("CSS ease-out", CubicBezierEasing.EaseOut.Evaluate));
        curves.Add(("CSS ease-in-out", CubicBezierEasing.EaseInOut.Evaluate));

        session = CreateFrameSession();
        DrawingLayer graphs = session.AddLayer("Graphs", Dim);
        for (int i = 0; i < curves.Count; i++)
        {
            (float x, float y) = PanelOrigin(i);
            graphs.AddShape(new RectangleShape(x, y, PanelWidth, PanelHeight, 1, Color.FromRgba(132, 138, 164, 60), cornerRadius: 6));
            graphs.AddShape(new LineShape(x + 8, GraphY(y, 0), x + PanelWidth - 8, GraphY(y, 0), 1, Color.FromRgba(132, 138, 164, 70)));
            graphs.AddShape(new LineShape(x + 8, GraphY(y, 1), x + PanelWidth - 8, GraphY(y, 1), 1, Color.FromRgba(132, 138, 164, 70)));
            PointF[] points = new PointF[97];
            for (int s = 0; s < points.Length; s++)
            {
                double t = s / (double)(points.Length - 1);
                points[s] = new PointF(GraphX(x, t), GraphY(y, curves[i].Curve(t)));
            }

            graphs.AddShape(new PolylineShape(points, 2, Color.FromRgb(120, 170, 255)));
            Label(graphs, session, curves[i].Label, x + 8, y + 15, 11, Ink);
        }

        moving = session.AddLayer("Dots", Accent);
    }

    /// <inheritdoc />
    public override string Name => "easing";

    /// <inheritdoc />
    public override string Title => "Easing curves";

    /// <inheritdoc />
    public override string Description => "Easing.Evaluate for every EasingKind, Easing.Steps and CubicBezierEasing: a dot on each graph, and one at the eased position below it";

    /// <inheritdoc />
    public override double DurationSeconds => 8;

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        double t = Math.Min(1, (seconds % (RunSeconds + PauseSeconds)) / RunSeconds);
        moving.Clear();
        for (int i = 0; i < curves.Count; i++)
        {
            (float x, float y) = PanelOrigin(i);
            double value = curves[i].Curve(t);
            moving.AddShape(new CircleShape(GraphX(x, t), GraphY(y, value), 3.5f, 1, Accent, isFilled: true));
            float trackY = y + PanelHeight - 9;
            moving.AddShape(new LineShape(x + 8, trackY, x + PanelWidth - 8, trackY, 1, Color.FromRgba(132, 138, 164, 60)));
            moving.AddShape(new CircleShape((float)Easing.Lerp(x + 10, x + PanelWidth - 10, value), trackY, 4, 1, Ink, isFilled: true));
        }

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    private static (float X, float Y) PanelOrigin(int index)
        => (Left + ((index % Columns) * (PanelWidth + Gap)), ContentTop + 6 + ((index / Columns) * (PanelHeight + Gap)));

    private static float GraphX(float panelX, double t) => (float)Easing.Lerp(panelX + 10, panelX + PanelWidth - 10, t);

    private static float GraphY(float panelY, double value)
    {
        float top = panelY + 22;
        float bottom = panelY + PanelHeight - 18;
        return (float)Easing.Lerp(bottom, top, (value - GraphLow) / (GraphHigh - GraphLow));
    }
}
