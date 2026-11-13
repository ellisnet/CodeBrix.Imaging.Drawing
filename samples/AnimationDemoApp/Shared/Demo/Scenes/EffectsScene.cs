using System;
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
/// Shadow, glow and blur, computed from the drawn pixels with the planes blur (so both packages apply the same
/// arithmetic). Top: a card (a group) lifting off the page - its position, shadow offset, softness and darkness
/// all bound to tracks; a star whose glow pulses and changes colour; a focus pull between two shapes (their
/// blur radii bound to opposite tracks). Bottom: a whole layer with one shadow under everything on it, and the same
/// effects exported over transparency and laid on a checkerboard - the shadow and glow are true alpha.
/// </summary>
internal sealed class EffectsScene : DemoScene
{
    private const int PanelWidth = 560;
    private const int PanelHeight = 230;
    private const int PanelX = 680;
    private const int PanelY = 440;

    private readonly DrawingSession session;
    private readonly DrawingSession panel;
    private readonly byte[] board;
    private readonly byte[] panelPixels = new byte[PanelWidth * PanelHeight * 4];

    /// <summary>Builds the demonstrations and their bindings.</summary>
    public EffectsScene()
    {
        session = CreateSession(DemoFrame.Width, DemoFrame.Height, Color.FromRgb(214, 210, 200));
        AddHeader(session, true);
        DrawingLayer notes = session.AddLayer("Notes", Color.FromRgb(90, 90, 110));
        Label(notes, session, "a group's shadow: offset, softness and alpha bound to tracks", 230, 420, 14, Color.FromRgb(90, 90, 110), TextAlign.Center);
        Label(notes, session, "glow radius and colour bound to tracks", 640, 420, 14, Color.FromRgb(90, 90, 110), TextAlign.Center);
        Label(notes, session, "a focus pull: BlurRadius bound to tracks", 1050, 420, 14, Color.FromRgb(90, 90, 110), TextAlign.Center);
        Label(notes, session, "one shadow for a whole layer (DrawingLayer.Effects)", 340, 700, 14, Color.FromRgb(90, 90, 110), TextAlign.Center);
        Label(notes, session, "over transparency, on a checkerboard", PanelX + (PanelWidth / 2f), 700, 14, Color.FromRgb(90, 90, 110), TextAlign.Center);

        DrawingLayer things = session.AddLayer("Things", Ink);
        session.ActiveLayer = things;

        // The lifting card.
        var card = new DrawingGroup();
        card.Add(new RectangleShape(100, 170, 260, 180, 1, Color.FromRgb(250, 248, 244), isFilled: true, cornerRadius: 14));
        card.Add(new CircleShape(150, 220, 26, 1, Color.FromRgb(255, 176, 72), isFilled: true));
        card.Add(new TextShape("Lift", 192, 232, 34, session.Fonts, Color.FromRgb(40, 40, 60)));
        card.Add(new RectangleShape(126, 280, 208, 10, 1, Color.FromRgb(210, 210, 220), isFilled: true, cornerRadius: 5));
        card.Add(new RectangleShape(126, 302, 150, 10, 1, Color.FromRgb(210, 210, 220), isFilled: true, cornerRadius: 5));
        card.Effects = DrawingEffects.DropShadow(new PointF(0, 4), 6, Color.FromRgba(0, 0, 0, 120));
        session.DrawShape(card);
        session.Animations.Bind(card, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 10) }, { 2, new PointF(0, -14), EasingKind.SineInOut }, { 4, new PointF(0, 10), EasingKind.SineInOut } }.Loop());
        session.Animations.Bind(card, AnimatedProperty.ShadowOffset, new PointTrack { { 0, new PointF(0, 4) }, { 2, new PointF(6, 30), EasingKind.SineInOut }, { 4, new PointF(0, 4), EasingKind.SineInOut } }.Loop());
        session.Animations.Bind(card, AnimatedProperty.ShadowBlurRadius, new FloatTrack { { 0, 6f }, { 2, 34f, EasingKind.SineInOut }, { 4, 6f, EasingKind.SineInOut } }.Loop());
        session.Animations.Bind(card, AnimatedProperty.ShadowColor, new ColorTrack { { 0, Color.FromRgba(0, 0, 0, 130) }, { 2, Color.FromRgba(0, 0, 0, 80), EasingKind.SineInOut }, { 4, Color.FromRgba(0, 0, 0, 130), EasingKind.SineInOut } }.Loop());

        // The glowing star.
        DrawingShape star = session.DrawShape(new PathShape(PathGeometry.Star(new PointF(640, 265), 95, 42, 5),
            new RadialGradientFill(new PointF(640, 255), 100, Color.FromRgb(255, 250, 220), Color.FromRgb(255, 190, 60))));
        star.Effects = DrawingEffects.Glow(16, Color.FromRgb(255, 140, 40), 1.5f);
        session.Animations.Bind(star, AnimatedProperty.GlowRadius, new FloatTrack { { 0, 10f }, { 1, 44f, EasingKind.SineInOut }, { 2, 10f, EasingKind.SineInOut } }.Loop());
        session.Animations.Bind(star, AnimatedProperty.GlowColor, new ColorTrack
        {
            { 0, Color.FromRgb(255, 140, 40) },
            { 4, Color.FromRgb(255, 60, 160), EasingKind.SineInOut },
            { 8, Color.FromRgb(255, 140, 40), EasingKind.SineInOut },
        });

        // The focus pull.
        DrawingShape back = session.DrawCircle(1000, 250, 80, filled: true, color: Color.FromRgb(90, 150, 230));
        DrawingShape front = session.DrawRectangle(1030, 230, 150, 120, filled: true, color: Color.FromRgb(240, 110, 90), cornerRadius: 12);
        session.Animations.Bind(back, AnimatedProperty.BlurRadius, new FloatTrack { { 0, 0f }, { 1.5, 0f }, { 3, 22f, EasingKind.SineInOut }, { 5.5, 22f }, { 7, 0f, EasingKind.SineInOut } });
        session.Animations.Bind(front, AnimatedProperty.BlurRadius, new FloatTrack { { 0, 22f }, { 1.5, 22f }, { 3, 0f, EasingKind.SineInOut }, { 5.5, 0f }, { 7, 22f, EasingKind.SineInOut } });

        // One shadow for a whole layer.
        DrawingLayer titled = session.AddLayer("Titled", Color.FromRgb(70, 110, 200));
        session.ActiveLayer = titled;
        session.DrawText("Shadows", 70, 560, 84, Color.FromRgb(70, 110, 200));
        session.DrawCircle(505, 530, 40, filled: true, color: Color.FromRgb(255, 176, 72));
        session.DrawRectangle(565, 495, 70, 70, filled: true, color: Color.FromRgb(140, 210, 160), cornerRadius: 8);
        session.DrawLine(80, 610, 630, 610, thickness: 8, color: Color.FromRgb(70, 110, 200));
        titled.Effects = DrawingEffects.DropShadow(new PointF(8, 10), 8, Color.FromRgba(30, 20, 60, 150));
        session.Animations.Bind(titled, AnimatedProperty.ShadowOffset, new PointTrack { { 0, new PointF(-10, 10) }, { 8, new PointF(14, 10), EasingKind.SineInOut } });

        // The same effects over transparency.
        board = CreateCheckerboard(PanelWidth, PanelHeight).ToRgba();
        panel = CreateSession(PanelWidth, PanelHeight, Color.Transparent);
        panel.AddLayer("Shapes", Ink);
        panel.DrawCircle(110, 115, 60, filled: true, color: Color.FromRgb(255, 210, 90)).Effects =
            DrawingEffects.Glow(30, Color.FromRgb(255, 120, 40), 1.6f).WithShadow(new PointF(14, 18), 14, Color.FromRgba(0, 0, 0, 200));
        panel.DrawShape(new PathShape(PathGeometry.Parse("M 230 170 C 260 40, 330 40, 350 120 S 440 200, 480 60"), new PathStrokeStyle(16f), Color.FromRgb(120, 200, 255)))
            .Effects = DrawingEffects.DropShadow(new PointF(10, 12), 10, Color.FromRgba(0, 0, 40, 180));
        DrawingShape blurred = panel.DrawRectangle(430, 140, 100, 70, filled: true, color: Color.FromRgb(240, 110, 90), cornerRadius: 10);
        panel.Animations.Bind(blurred, AnimatedProperty.BlurRadius, new FloatTrack { { 0, 0f }, { 4, 20f, EasingKind.SineInOut }, { 8, 0f, EasingKind.SineInOut } });
    }

    /// <inheritdoc />
    public override string Name => "effects";

    /// <inheritdoc />
    public override string Title => "Shadow, glow and blur";

    /// <inheritdoc />
    public override string Description => "DrawingEffects on elements, groups and layers: DropShadow, Glow, Blur - computed in shared managed code; parameters bound to tracks";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;
        ExportFrame(session, rgba);
        panel.Time = seconds;
        panel.ExportPixels(panelPixels, new Size(PanelWidth, PanelHeight), includeBackground: false);
        FrameCompositor.Copy(board, PanelWidth, PanelHeight, rgba, PanelX, PanelY);
        FrameCompositor.Over(panelPixels, PanelWidth, PanelHeight, rgba, PanelX, PanelY);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        session.Dispose();
        panel.Dispose();
    }
}
