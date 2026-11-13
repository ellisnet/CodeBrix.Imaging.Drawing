using System;
using System.Collections.Generic;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Fills;
using CodeBrix.Imaging.Drawing.Models;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace AnimationDemoApp.Demo.Scenes;

/// <summary>
/// Layers switched on and off (<see cref="DrawingLayer.IsVisible" />, bound to <see cref="BoolTrack" />s through
/// <see cref="AnimatedProperty.IsVisible" />): a hidden layer is skipped entirely - not drawn, not exported - and
/// switches exactly at its keys' times. Left: five card layers revealed one after another and taken away in reverse.
/// Middle: a blinking "on air" layer (a looping track). Right: a matte layer that stays hidden yet still masks the
/// striped layer through <see cref="DrawingMask.FromLayer" />, shown on its own for a moment near the end. Below,
/// every layer's state read back from the session.
/// </summary>
internal sealed class LayerVisibilityScene : DemoScene
{
    private static readonly Color[] CardColors =
    {
        Color.FromRgb(240, 84, 110), Color.FromRgb(255, 128, 72), Color.FromRgb(255, 176, 72),
        Color.FromRgb(120, 200, 140), Color.FromRgb(120, 160, 255),
    };

    private readonly DrawingSession session;
    private readonly List<DrawingLayer> watched = new List<DrawingLayer>();
    private readonly DrawingLayer now;

    /// <summary>Builds the layers and their on/off tracks.</summary>
    public LayerVisibilityScene()
    {
        session = CreateFrameSession();
        DrawingLayer notes = session.AddLayer("Notes", Dim);
        Label(notes, session, "revealed in sequence, removed in reverse", 40, 128, 14, Ink);
        Label(notes, session, "a looping BoolTrack: 0.5 s on, 0.3 s off", 470, 128, 14, Ink);
        Label(notes, session, "a hidden matte layer still masks", 860, 128, 14, Ink);
        notes.AddShape(new RectangleShape(470, 160, 340, 260, 1.5f, Color.FromRgba(132, 138, 164, 90), cornerRadius: 12));
        notes.AddShape(new RectangleShape(860, 160, 380, 260, 1.5f, Color.FromRgba(132, 138, 164, 90), cornerRadius: 12));

        // Five cards, each on its own layer, fanned out.
        for (int i = 0; i < 5; i++)
        {
            DrawingLayer card = session.AddLayer("Card " + (i + 1), Ink);
            float x = 50 + (i * 44), y = 160 + (i * 34);
            card.AddShape(new RectangleShape(x, y, 200, 130, 1, CardColors[i], isFilled: true, cornerRadius: 12));
            card.AddShape(new RectangleShape(x, y, 200, 130, 2, Color.FromRgba(18, 20, 30, 200), cornerRadius: 12));
            card.AddShape(new TextShape("layer " + (i + 1), x + 16, y + 34, 20, session.Fonts, Color.FromRgb(24, 24, 36)));
            session.Animations.Bind(card, AnimatedProperty.IsVisible,
                new BoolTrack { { 0, false }, { 0.5 + (i * 0.6), true }, { 5.6 + ((4 - i) * 0.3), false } });
            watched.Add(card);
        }

        // The blinking sign.
        DrawingLayer onAir = session.AddLayer("On air", Ink);
        onAir.AddShape(new RectangleShape(530, 250, 220, 80, 1, Color.FromRgb(60, 20, 30), isFilled: true, cornerRadius: 14));
        onAir.AddShape(new RectangleShape(530, 250, 220, 80, 3, Color.FromRgb(240, 84, 110), cornerRadius: 14));
        onAir.AddShape(new CircleShape(570, 290, 14, 1, Color.FromRgb(255, 60, 70), isFilled: true));
        onAir.AddShape(new TextShape("ON AIR", 596, 302, 32, session.Fonts, Color.FromRgb(255, 220, 220)));
        var blink = new BoolTrack { { 0, true }, { 0.5, false }, { 0.8, true } };
        blink.LoopMode = TrackLoopMode.Loop;
        session.Animations.Bind(onAir, AnimatedProperty.IsVisible, blink);
        watched.Add(onAir);

        // The matte: a word sliding across, on a layer that is hidden except for a moment near the end.
        DrawingLayer matte = session.AddLayer("Matte", Color.White);
        DrawingShape word = new TextShape("MATTE", 1050, 320, 92, session.Fonts, Color.White, TextAlign.Center);
        matte.AddShape(word);
        session.Animations.Bind(word, AnimatedProperty.Position, new PointTrack { { 0, new PointF(-24, 0) }, { 4, new PointF(24, 0), EasingKind.SineInOut }, { 8, new PointF(-24, 0), EasingKind.SineInOut } });
        session.Animations.Bind(matte, AnimatedProperty.IsVisible, new BoolTrack { { 0, false }, { 6.4, true }, { 7.4, false } });
        watched.Add(matte);

        DrawingLayer stripes = session.AddLayer("Stripes", Ink);
        stripes.AddShape(new RectangleShape(870, 170, 360, 240, new LinearGradientFill(new PointF(870, 0), new PointF(1230, 0),
            Color.FromRgb(255, 120, 80), Color.FromRgb(120, 120, 255))));
        var moving = new DrawingGroup(Color.FromRgba(255, 255, 255, 120));
        for (int i = 0; i < 16; i++) moving.Add(new LineShape(760 + (i * 36), 420, 860 + (i * 36), 160, 10));
        stripes.AddShape(moving);
        session.Animations.Bind(moving, AnimatedProperty.Position, new PointTrack { { 0, new PointF(0, 0) }, { 8, new PointF(144, 0) } });
        stripes.Mask = DrawingMask.FromLayer(matte);
        watched.Add(stripes);

        Label(notes, session, "the Matte layer (IsVisible = false) masks the Stripes layer;", 860, 442, 13, Dim);
        Label(notes, session, "it is shown itself from 6.4 s to 7.4 s", 860, 462, 13, Dim);

        now = session.AddLayer("Now", Ink);
    }

    /// <inheritdoc />
    public override string Name => "layer-visibility";

    /// <inheritdoc />
    public override string Title => "Layer visibility: switching layers on and off";

    /// <inheritdoc />
    public override string Description => "DrawingLayer.IsVisible bound to a BoolTrack (AnimatedProperty.IsVisible): exact steps, hidden layers skipped, a hidden matte still masks";

    /// <inheritdoc />
    public override void Render(double seconds, Span<byte> rgba)
    {
        session.Time = seconds;

        // Every watched layer's state, read back from the session.
        now.Clear();
        Label(now, session, "DrawingLayer.IsVisible now:", 40, 540, 15, Ink);
        for (int i = 0; i < watched.Count; i++)
        {
            DrawingLayer layer = watched[i];
            float x = 40 + ((i % 5) * 240), y = 580 + ((i / 5) * 44);
            bool on = layer.IsVisible;
            now.AddShape(new RectangleShape(x, y - 22, 224, 32, 1, on ? Color.FromRgb(40, 70, 52) : Color.FromRgb(36, 38, 52), isFilled: true, cornerRadius: 8));
            now.AddShape(new CircleShape(x + 18, y - 6, 7, 1, on ? Color.FromRgb(110, 220, 140) : Color.FromRgb(90, 94, 116), isFilled: true));
            now.AddShape(new TextShape(layer.Name + (on ? "   on" : "   off"), x + 34, y, 16, session.Fonts, on ? Ink : Dim));
        }

        ExportFrame(session, rgba);
    }

    /// <inheritdoc />
    public override void Dispose() => session.Dispose();
}
