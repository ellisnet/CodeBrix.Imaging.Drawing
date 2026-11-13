extern alias noskia;
extern alias skia;

using System.IO;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;
using NE = noskia::CodeBrix.Imaging.Drawing.Effects;
using NF = noskia::CodeBrix.Imaging.Drawing.Fills;
using NI = noskia::CodeBrix.Imaging.Drawing.Images;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NP = noskia::CodeBrix.Imaging.Drawing.Paths;
using NPl = noskia::CodeBrix.Imaging.Drawing.Planes;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NSession = noskia::CodeBrix.Imaging.Drawing.DrawingSession;
using NSessionOptions = noskia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using SE = skia::CodeBrix.Imaging.Drawing.Effects;
using SF = skia::CodeBrix.Imaging.Drawing.Fills;
using SI = skia::CodeBrix.Imaging.Drawing.Images;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SP = skia::CodeBrix.Imaging.Drawing.Paths;
using SPl = skia::CodeBrix.Imaging.Drawing.Planes;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using SSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;
using SSessionOptions = skia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for what paths, pictures, sprites, masks and effects DRAW: identical scenes through
/// both assemblies, compared within the image tolerance (the content is rasterized by each
/// backend's anti-aliased canvas), over an opaque background and over transparency. Effects are
/// computed from the drawn pixels in shared managed code, so a scene whose content both canvases
/// draw identically (a picture copied 1:1) also gets near-identical effects.
/// </summary>
public class DrawingFeatureParityTests
{
    private static readonly Size Frame = new Size(400, 300);
    private const string Signature = "M 30 80 C 60 20, 110 20, 120 80 S 180 140, 210 80 Q 240 40 270 80 T 330 80";

    private static byte[] RawToPng(byte[] rgba, Size size)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, size.Width, size.Height, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static byte[] Picture()
    {
        var rgba = new byte[32 * 16 * 4];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int o = ((y * 32) + x) * 4;
                rgba[o] = (byte)(x * 8);
                rgba[o + 1] = (byte)(y * 16);
                rgba[o + 2] = (byte)(255 - (x * 8));
                rgba[o + 3] = (byte)(((x / 4) + (y / 4)) % 2 == 0 ? 255 : 160);
            }
        }
        return rgba;
    }

    private static void Compare(byte[] skia, byte[] noSkia, string label)
        => ImageComparison.AssertSimilar(RawToPng(skia, Frame), RawToPng(noSkia, Frame), label);

    private static SSession SkiaSession()
    {
        var session = new SSession(new SSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(236, 232, 224) });
        session.AddLayer("A", Color.FromRgb(30, 40, 120));
        return session;
    }

    private static NSession NoSkiaSession()
    {
        var session = new NSession(new NSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = Color.FromRgb(236, 232, 224) });
        session.AddLayer("A", Color.FromRgb(30, 40, 120));
        return session;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Paths_with_trim_dashes_fills_and_morphs_draw_alike(bool includeBackground)
    {
        //Arrange
        using SSession skia = SkiaSession();
        SP.PathGeometry sPath = SP.PathGeometry.Parse(Signature);
        skia.DrawShape(new SS.PathShape(sPath, 6f) { TrimEnd = 0.8f, TrimStart = 0.1f });
        skia.DrawShape(new SS.PathShape(SP.PathGeometry.Star(new PointF(330, 200), 60, 25, 5),
            new SP.PathStrokeStyle(4f, SP.PathStrokeCap.Round, SP.PathStrokeJoin.Miter, 4f, new[] { 10f, 6f }), Color.FromRgb(200, 30, 30), Color.FromRgb(255, 210, 60)));
        skia.DrawShape(new SS.PathShape(new SP.PathMorph(SP.PathGeometry.Circle(new PointF(80, 200), 50), SP.PathGeometry.RegularPolygon(new PointF(80, 200), 60, 3)), 0.4f, 5f, Color.FromRgb(0, 120, 60)));
        skia.DrawShape(new SS.PathShape(SP.PathGeometry.RoundedPolygon(new[] { new PointF(170, 150), new PointF(260, 160), new PointF(220, 270) }, 18),
            new SF.LinearGradientFill(new PointF(170, 150), new PointF(260, 270), Color.Red, Color.Blue)));
        skia.DrawShape(new SS.PathShape(SP.PathGeometry.Line(new PointF(20, 120), new PointF(380, 120)), new SP.PathStrokeStyle(7f, dashPattern: new[] { 0f, 16f })));
        using NSession noSkia = NoSkiaSession();
        NP.PathGeometry nPath = NP.PathGeometry.Parse(Signature);
        noSkia.DrawShape(new NS.PathShape(nPath, 6f) { TrimEnd = 0.8f, TrimStart = 0.1f });
        noSkia.DrawShape(new NS.PathShape(NP.PathGeometry.Star(new PointF(330, 200), 60, 25, 5),
            new NP.PathStrokeStyle(4f, NP.PathStrokeCap.Round, NP.PathStrokeJoin.Miter, 4f, new[] { 10f, 6f }), Color.FromRgb(200, 30, 30), Color.FromRgb(255, 210, 60)));
        noSkia.DrawShape(new NS.PathShape(new NP.PathMorph(NP.PathGeometry.Circle(new PointF(80, 200), 50), NP.PathGeometry.RegularPolygon(new PointF(80, 200), 60, 3)), 0.4f, 5f, Color.FromRgb(0, 120, 60)));
        noSkia.DrawShape(new NS.PathShape(NP.PathGeometry.RoundedPolygon(new[] { new PointF(170, 150), new PointF(260, 160), new PointF(220, 270) }, 18),
            new NF.LinearGradientFill(new PointF(170, 150), new PointF(260, 270), Color.Red, Color.Blue)));
        noSkia.DrawShape(new NS.PathShape(NP.PathGeometry.Line(new PointF(20, 120), new PointF(380, 120)), new NP.PathStrokeStyle(7f, dashPattern: new[] { 0f, 16f })));

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, includeBackground);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, includeBackground);

        //Assert - the round-capped zero-length dashes draw as dots in both
        TestAssetsPixel(skiaPixels, 116, 120).A.Should().BeGreaterThan(200);
        TestAssetsPixel(noSkiaPixels, 116, 120).A.Should().BeGreaterThan(200);
        Compare(skiaPixels, noSkiaPixels, "paths, background " + includeBackground);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Pictures_and_sprites_draw_alike(bool includeBackground)
    {
        //Arrange
        using SI.ImageSource sImage = SI.ImageSource.FromRgba(Picture(), 32, 16);
        using SI.SpriteSheet sSheet = SI.SpriteSheet.FromGrid(sImage, 2, 1);
        using SSession skia = SkiaSession();
        var sShape = new SS.ImageShape(sImage, new RectangleF(40, 40, 192, 96), tint: Color.FromRgba(255, 230, 200, 220));
        sShape.Transform = SM.DrawingTransform.Rotate(20, new PointF(136, 88));
        sShape.Opacity = 0.8f;
        skia.DrawShape(sShape);
        skia.DrawShape(new SS.ImageShape(sImage, new RectangleF(250, 30, 128, 64), new Rectangle(8, 0, 16, 8), SI.ImageSampling.Nearest));
        skia.DrawSprite(sSheet, new RectangleF(260, 160, 96, 96), 4, SI.SpritePlayback.PingPong);
        skia.Time = 0.3;
        using NI.ImageSource nImage = NI.ImageSource.FromRgba(Picture(), 32, 16);
        using NI.SpriteSheet nSheet = NI.SpriteSheet.FromGrid(nImage, 2, 1);
        using NSession noSkia = NoSkiaSession();
        var nShape = new NS.ImageShape(nImage, new RectangleF(40, 40, 192, 96), tint: Color.FromRgba(255, 230, 200, 220));
        nShape.Transform = NM.DrawingTransform.Rotate(20, new PointF(136, 88));
        nShape.Opacity = 0.8f;
        noSkia.DrawShape(nShape);
        noSkia.DrawShape(new NS.ImageShape(nImage, new RectangleF(250, 30, 128, 64), new Rectangle(8, 0, 16, 8), NI.ImageSampling.Nearest));
        noSkia.DrawSprite(nSheet, new RectangleF(260, 160, 96, 96), 4, NI.SpritePlayback.PingPong);
        noSkia.Time = 0.3;

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, includeBackground);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, includeBackground);

        //Assert
        Compare(skiaPixels, noSkiaPixels, "pictures, background " + includeBackground);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Clips_mattes_and_plane_masks_draw_alike(bool includeBackground)
    {
        //Arrange
        SPl.DrawingPlane sRamp = new SPl.DrawingPlane(400, 300).Fill((x, y) => x / 399f);
        NPl.DrawingPlane nRamp = new NPl.DrawingPlane(400, 300).Fill((x, y) => x / 399f);
        using SSession skia = SkiaSession();
        skia.DrawRectangle(20, 20, 360, 260, filled: true);
        skia.Layers[0].Mask = SE.DrawingMask.Inside(SP.PathGeometry.Star(new PointF(200, 150), 140, 60, 6));
        skia.ActiveLayer = skia.AddLayer("B", Color.FromRgb(220, 120, 20));
        var sGroup = new SS.DrawingGroup();
        sGroup.Add(new SS.CircleShape(80, 80, 60, 1, null, true));
        sGroup.Mask = SE.DrawingMask.Outside(SP.PathGeometry.Circle(new PointF(80, 80), 25));
        sGroup.Transform = SM.DrawingTransform.Rotate(30, new PointF(80, 80));
        skia.DrawShape(sGroup);
        skia.ActiveLayer = skia.AddLayer("C", Color.FromRgb(20, 160, 160));
        skia.DrawRectangle(240, 180, 150, 110, filled: true);
        skia.Layers[2].Mask = SE.DrawingMask.FromPlane(sRamp);
        using NSession noSkia = NoSkiaSession();
        noSkia.DrawRectangle(20, 20, 360, 260, filled: true);
        noSkia.Layers[0].Mask = NE.DrawingMask.Inside(NP.PathGeometry.Star(new PointF(200, 150), 140, 60, 6));
        noSkia.ActiveLayer = noSkia.AddLayer("B", Color.FromRgb(220, 120, 20));
        var nGroup = new NS.DrawingGroup();
        nGroup.Add(new NS.CircleShape(80, 80, 60, 1, null, true));
        nGroup.Mask = NE.DrawingMask.Outside(NP.PathGeometry.Circle(new PointF(80, 80), 25));
        nGroup.Transform = NM.DrawingTransform.Rotate(30, new PointF(80, 80));
        noSkia.DrawShape(nGroup);
        noSkia.ActiveLayer = noSkia.AddLayer("C", Color.FromRgb(20, 160, 160));
        noSkia.DrawRectangle(240, 180, 150, 110, filled: true);
        noSkia.Layers[2].Mask = NE.DrawingMask.FromPlane(nRamp);

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, includeBackground);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, includeBackground);

        //Assert
        Compare(skiaPixels, noSkiaPixels, "masks, background " + includeBackground);
    }

    [Fact]
    public void Layer_mattes_draw_alike()
    {
        //Arrange
        var sMatte = new SM.DrawingLayer("Matte", Color.White);
        sMatte.AddShape(new SS.TextShape("MATTE", 30, 200, 120, CreateSkiaFonts()));
        using SSession skia = SkiaSession();
        skia.DrawShape(new SS.RectangleShape(0, 0, 400, 300, new SF.LinearGradientFill(new PointF(0, 0), new PointF(400, 0), Color.Orange, Color.Purple)));
        skia.Layers[0].Mask = SE.DrawingMask.FromLayer(sMatte);
        var nMatte = new NM.DrawingLayer("Matte", Color.White);
        nMatte.AddShape(new NS.TextShape("MATTE", 30, 200, 120, CreateNoSkiaFonts()));
        using NSession noSkia = NoSkiaSession();
        noSkia.DrawShape(new NS.RectangleShape(0, 0, 400, 300, new NF.LinearGradientFill(new PointF(0, 0), new PointF(400, 0), Color.Orange, Color.Purple)));
        noSkia.Layers[0].Mask = NE.DrawingMask.FromLayer(nMatte);

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, false);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, false);

        //Assert
        Compare(skiaPixels, noSkiaPixels, "layer matte");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Shadows_glows_and_blurs_draw_alike(bool includeBackground)
    {
        //Arrange
        using SSession skia = SkiaSession();
        skia.DrawCircle(90, 90, 50, filled: true, color: Color.Gold).Effects =
            SE.DrawingEffects.DropShadow(new PointF(10, 14), 18, Color.FromRgba(0, 0, 0, 170)).WithGlow(22, Color.FromRgba(255, 90, 0, 220), 1.5f);
        skia.DrawRectangle(190, 40, 90, 90, filled: true).Effects = SE.DrawingEffects.Blur(9);
        skia.ActiveLayer = skia.AddLayer("Shadowed", Color.FromRgb(40, 140, 200));
        skia.DrawPath(SP.PathGeometry.Parse(Signature).Transformed(System.Numerics.Matrix3x2.CreateTranslation(20, 150)), 9f);
        skia.Layers[1].Effects = SE.DrawingEffects.DropShadow(new PointF(-6, 9), 6, Color.FromRgba(0, 0, 60, 200));
        using NSession noSkia = NoSkiaSession();
        noSkia.DrawCircle(90, 90, 50, filled: true, color: Color.Gold).Effects =
            NE.DrawingEffects.DropShadow(new PointF(10, 14), 18, Color.FromRgba(0, 0, 0, 170)).WithGlow(22, Color.FromRgba(255, 90, 0, 220), 1.5f);
        noSkia.DrawRectangle(190, 40, 90, 90, filled: true).Effects = NE.DrawingEffects.Blur(9);
        noSkia.ActiveLayer = noSkia.AddLayer("Shadowed", Color.FromRgb(40, 140, 200));
        noSkia.DrawPath(NP.PathGeometry.Parse(Signature).Transformed(System.Numerics.Matrix3x2.CreateTranslation(20, 150)), 9f);
        noSkia.Layers[1].Effects = NE.DrawingEffects.DropShadow(new PointF(-6, 9), 6, Color.FromRgba(0, 0, 60, 200));

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, includeBackground);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, includeBackground);

        //Assert
        Compare(skiaPixels, noSkiaPixels, "effects, background " + includeBackground);
    }

    [Fact]
    public void Effects_of_identically_drawn_content_are_computed_alike()
    {
        //Arrange - an opaque picture copied 1:1 (both canvases draw it exactly), blurred and shadowed
        var opaque = new byte[64 * 48 * 4];
        for (int i = 0; i < opaque.Length; i += 4) { opaque[i] = (byte)(i % 251); opaque[i + 1] = (byte)(i % 127); opaque[i + 2] = 90; opaque[i + 3] = 255; }
        using SI.ImageSource sImage = SI.ImageSource.FromRgba(opaque, 64, 48);
        using NI.ImageSource nImage = NI.ImageSource.FromRgba(opaque, 64, 48);
        using SSession skia = SkiaSession();
        skia.DrawShape(new SS.ImageShape(sImage, new RectangleF(100, 100, 64, 48), null, SI.ImageSampling.Nearest)).Effects =
            SE.DrawingEffects.Blur(6).WithShadow(new PointF(20, 20), 10, Color.Black);
        using NSession noSkia = NoSkiaSession();
        noSkia.DrawShape(new NS.ImageShape(nImage, new RectangleF(100, 100, 64, 48), null, NI.ImageSampling.Nearest)).Effects =
            NE.DrawingEffects.Blur(6).WithShadow(new PointF(20, 20), 10, Color.Black);

        //Act
        byte[] skiaPixels = skia.ExportPixels(Frame, true);
        byte[] noSkiaPixels = noSkia.ExportPixels(Frame, true);

        //Assert - only the final 8-bit compositing rounding may differ
        ImageComparison.Stats stats = ImageComparison.Compare(RawToPng(skiaPixels, Frame), RawToPng(noSkiaPixels, Frame));
        stats.MaxDelta.Should().BeLessThanOrEqualTo(2);
    }

    private static Rgba32 TestAssetsPixel(byte[] rgba, int x, int y)
    {
        int i = ((y * Frame.Width) + x) * 4;
        return new Rgba32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
    }

    private static skia::CodeBrix.Imaging.Drawing.Text.FontRegistry CreateSkiaFonts()
    {
        var fonts = new skia::CodeBrix.Imaging.Drawing.Text.FontRegistry();
        fonts.RegisterFont(SvgTestAssets.TestFontPath);
        return fonts;
    }

    private static noskia::CodeBrix.Imaging.Drawing.Text.FontRegistry CreateNoSkiaFonts()
    {
        var fonts = new noskia::CodeBrix.Imaging.Drawing.Text.FontRegistry();
        fonts.RegisterFont(SvgTestAssets.TestFontPath);
        return fonts;
    }
}
