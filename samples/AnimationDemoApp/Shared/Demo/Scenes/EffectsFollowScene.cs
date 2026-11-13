using System;
using System.Globalization;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Effects follow scale and camera. A tabletop scene under a camera that zooms in and out and rolls a little: every
/// shadow, glow and blur grows and shrinks with it, staying in proportion to the objects. Two cards spin on the
/// table: the left one's shadow is <see cref="ShadowDirection.FixedInScene" /> (the light stays where it is in the
/// scene, so the shadow always falls the same way - and turns only with the camera's roll), the right one's is
/// <see cref="ShadowDirection.RotatesWithElement" /> (the shadow is carried round with the card). Between them a
/// pulsing star shows the glow and shadow scaling with the element's own scale. The header, the read-out and the
/// captions are on layers with parallax 0, so the camera leaves them alone.
/// </summary>
internal sealed class EffectsFollowScene : DemoScene
{
    private static readonly Color Table = Color.FromRgb(222, 216, 204);
    private static readonly Color Caption = Color.FromRgb(60, 62, 80);

    private readonly DrawingSession session;
    private readonly FloatTrack zoom = new FloatTrack { { 0, 0.85f }, { 2.5, 1.45f, EasingKind.SineInOut }, { 5.5, 0.85f, EasingKind.SineInOut }, { 8, 0.85f } };
    private readonly FloatTrack roll = new FloatTrack { { 0, 0f }, { 4, 0f }, { 5.5, 14f, EasingKind.SineInOut }, { 7.5, 0f, EasingKind.SineInOut } };

    /// <summary>Builds the table, the cards, the star and the camera's tracks.</summary>
    public EffectsFollowScene()
    {
        session = CreateSession(DemoFrame.Width, DemoFrame.Height, Backdrop);

        DrawingLayer table = session.AddLayer("Table", Table);
        table.AddShape(new RectangleShape(40, 120, 1200, 520, 1, Table, isFilled: true, cornerRadius: 24));
        // The light, fixed in the scene: a sun and an arrow the way its light falls.
        table.AddShape(new CircleShape(230, 210, 26, 1, Color.FromRgb(255, 196, 70), isFilled: true));
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            table.AddShape(new LineShape((float)(230 + (34 * Math.Cos(a))), (float)(210 + (34 * Math.Sin(a))),
                (float)(230 + (44 * Math.Cos(a))), (float)(210 + (44 * Math.Sin(a))), 4, Color.FromRgb(255, 196, 70)));
        }
        table.AddShape(new ArrowShape(270, 250, 325, 305, 4, Color.FromRgb(200, 150, 60), 16));
        table.AddShape(new TextShape("light", 206, 276, 16, session.Fonts, Color.FromRgb(160, 120, 50)));

        DrawingLayer things = session.AddLayer("Things", Ink);
        session.ActiveLayer = things;
        AddCard(things, 340, 390, "fixed", Color.FromRgb(70, 120, 220), ShadowDirection.FixedInScene, 1);
        AddCard(things, 940, 390, "turns", Color.FromRgb(220, 90, 80), ShadowDirection.RotatesWithElement, 1);

        DrawingShape star = session.DrawShape(new PathShape(PathGeometry.Star(new PointF(640, 390), 70, 30, 5),
            new RadialGradientFill(new PointF(640, 380), 80, Color.FromRgb(255, 250, 220), Color.FromRgb(255, 176, 60))));
        star.Effects = DrawingEffects.Glow(14, Color.FromRgb(255, 120, 40), 1.4f).WithShadow(new PointF(14, 16), 8, Color.FromRgba(40, 30, 20, 140));
        session.Animations.Bind(star, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(640, 390) } });
        session.Animations.Bind(star, AnimatedProperty.Scale, new FloatTrack { { 0, 0.6f }, { 2, 1.25f, EasingKind.SineInOut }, { 4, 0.6f, EasingKind.SineInOut } }.Loop());

        DrawingLayer header = AddHeader(session, opaqueBand: true);
        header.Parallax = 0f;
        DrawingLayer hud = session.AddLayer("Hud", Ink);
        hud.Parallax = 0f;
        hud.AddShape(new RectangleShape(0, 652, DemoFrame.Width, 68, 1, Backdrop, isFilled: true));
        hud.AddShape(new TextShape("ShadowDirection.FixedInScene - the light stays put", 340, 676, 15, session.Fonts, Ink, TextAlign.Center));
        hud.AddShape(new TextShape("the star: glow and shadow scale with the element", 640, 700, 13, session.Fonts, Dim, TextAlign.Center));
        hud.AddShape(new TextShape("ShadowDirection.RotatesWithElement - turns with it", 940, 676, 15, session.Fonts, Ink, TextAlign.Center));
        hud.AddShape(new AnimatedShape(t => new TextShape(Readout(t), 1248, 46, 16, session.Fonts, Accent, TextAlign.Right)));

        session.Camera = new DrawingCamera();
        session.Animations.Bind(session.Camera, AnimatedProperty.Scale, zoom);
        session.Animations.Bind(session.Camera, AnimatedProperty.Rotation, roll);
    }

    /// <inheritdoc />
    public override string Name => "effects-follow";

    /// <inheritdoc />
    public override string Title => "Effects follow scale and camera";

    /// <inheritdoc />
    public override string Description => "Shadows, glows and blurs scale with the element, its groups, its layer and the camera; ShadowDirection: fixed in the scene or turning with the element";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();

    //A card that spins once in the scene's 8 seconds, its shadow falling by the given rule
    private void AddCard(DrawingLayer layer, float cx, float cy, string word, Color color, ShadowDirection direction, int turns)
    {
        var card = new DrawingGroup();
        card.Add(new RectangleShape(cx - 110, cy - 70, 220, 140, 1, Color.FromRgb(250, 248, 244), isFilled: true, cornerRadius: 16));
        card.Add(new RectangleShape(cx - 110, cy - 70, 220, 34, 1, color, isFilled: true, cornerRadius: 16));
        card.Add(new RectangleShape(cx - 110, cy - 50, 220, 14, 1, color, isFilled: true));
        card.Add(new TextShape(word, cx, cy + 34, 40, session.Fonts, Color.FromRgb(40, 40, 60), TextAlign.Center));
        card.Effects = DrawingEffects.DropShadow(new PointF(22, 22), 10, Color.FromRgba(30, 24, 20, 150), direction);
        layer.AddShape(card);
        session.Animations.Bind(card, AnimatedProperty.Anchor, new PointTrack { { 0, new PointF(cx, cy) } });
        session.Animations.Bind(card, AnimatedProperty.Rotation, new FloatTrack { { 0, 0f }, { 8, 360f * turns } });
    }

    private string Readout(double t)
        => string.Format(CultureInfo.InvariantCulture, "camera zoom {0:F2}x  roll {1:F0} deg", zoom.Evaluate(t), roll.Evaluate(t));
}
