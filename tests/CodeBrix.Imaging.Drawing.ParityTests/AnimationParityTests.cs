extern alias noskia;
extern alias skia;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.TestSupport;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;
using NA = noskia::CodeBrix.Imaging.Drawing.Animation;
using NL = noskia::CodeBrix.Imaging.Drawing.Layout;
using NM = noskia::CodeBrix.Imaging.Drawing.Models;
using NR = noskia::CodeBrix.Imaging.Drawing.Rendering;
using NS = noskia::CodeBrix.Imaging.Drawing.Shapes;
using NSafe = noskia::CodeBrix.Imaging.Drawing.SafeAreas;
using NSession = noskia::CodeBrix.Imaging.Drawing.DrawingSession;
using NSessionOptions = noskia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using NSequence = noskia::CodeBrix.Imaging.Drawing.FrameSequenceRenderer;
using SA = skia::CodeBrix.Imaging.Drawing.Animation;
using SL = skia::CodeBrix.Imaging.Drawing.Layout;
using SM = skia::CodeBrix.Imaging.Drawing.Models;
using SR = skia::CodeBrix.Imaging.Drawing.Rendering;
using SS = skia::CodeBrix.Imaging.Drawing.Shapes;
using SSafe = skia::CodeBrix.Imaging.Drawing.SafeAreas;
using SSession = skia::CodeBrix.Imaging.Drawing.DrawingSession;
using SSessionOptions = skia::CodeBrix.Imaging.Drawing.DrawingSessionOptions;
using SSequence = skia::CodeBrix.Imaging.Drawing.FrameSequenceRenderer;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Parity for the animation model. Everything COMPUTED in shared managed code - keyframe
/// track values, timeline frames, cues and timecodes, playback state, transform and camera
/// geometry, group bounds, layout positions and safe rectangles, safe-area findings of
/// transformed elements, debug read-out lines - must be IDENTICAL between the two assemblies.
/// What is drawn through each backend's antialiased canvas (transformed elements, faded
/// groups, the camera, time windows, animated shapes, frame sequences) must match within the
/// existing image tolerance. Each scenario is written once per alias (the two assemblies
/// declare the same type names).
/// </summary>
public class AnimationParityTests
{
    private static readonly Size Frame = new Size(320, 200);

    private static byte[] RawToPng(byte[] rgba, Size size)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(rgba, size.Width, size.Height, PngFormat.Instance);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    private static readonly double[] SampleTimes = { -1, 0, 0.13, 0.5, 0.999, 1, 1.7, 2.5, 3, 4.25, 7 };

    [Fact]
    public void Keyframe_tracks_evaluate_identically()
    {
        //Arrange
        var sFloat = new SA.FloatTrack { { 0, 0f }, { 1, 10f, SA.EasingKind.ElasticOut }, { 2, 3f, SA.EasingKind.BackInOut } };
        sFloat.AddHold(3, -4f);
        sFloat.LoopMode = SA.TrackLoopMode.PingPong;
        var nFloat = new NA.FloatTrack { { 0, 0f }, { 1, 10f, NA.EasingKind.ElasticOut }, { 2, 3f, NA.EasingKind.BackInOut } };
        nFloat.AddHold(3, -4f);
        nFloat.LoopMode = NA.TrackLoopMode.PingPong;
        var sPoint = new SA.PointTrack { { 0, new PointF(0, 0) }, { 2.5, new PointF(300, -40), SA.EasingKind.QuintInOut } };
        var nPoint = new NA.PointTrack { { 0, new PointF(0, 0) }, { 2.5, new PointF(300, -40), NA.EasingKind.QuintInOut } };
        sPoint.LoopMode = SA.TrackLoopMode.Loop;
        nPoint.LoopMode = NA.TrackLoopMode.Loop;
        var sColor = new SA.ColorTrack { { 0, Color.FromRgba(255, 0, 40, 0) }, { 3, Color.FromRgb(20, 200, 255), SA.EasingKind.SineInOut } };
        var nColor = new NA.ColorTrack { { 0, Color.FromRgba(255, 0, 40, 0) }, { 3, Color.FromRgb(20, 200, 255), NA.EasingKind.SineInOut } };

        //Act + Assert
        foreach (double t in SampleTimes)
        {
            sFloat.Evaluate(t).Should().Be(nFloat.Evaluate(t));
            sPoint.Evaluate(t).Should().Be(nPoint.Evaluate(t));
            sColor.Evaluate(t).ToPixel<Rgba32>().Should().Be(nColor.Evaluate(t).ToPixel<Rgba32>());
        }
    }

    [Fact]
    public void Timelines_cues_and_playback_compute_identically()
    {
        //Arrange
        var s = new SA.AnimationTimeline(21.0, 29.97);
        var n = new NA.AnimationTimeline(21.0, 29.97);
        s.AddCue("intro", 0, 1, SA.EasingKind.SmoothStep);
        n.AddCue("intro", 0, 1, NA.EasingKind.SmoothStep);
        s.AddCue("draw", 1, 20.5);
        n.AddCue("draw", 1, 20.5);
        s.AddMarker("end", 20.5);
        n.AddMarker("end", 20.5);
        var sPlay = new SA.PlaybackController(s) { Speed = 1.5 };
        var nPlay = new NA.PlaybackController(n) { Speed = 1.5 };
        sPlay.Play();
        nPlay.Play();

        //Act + Assert
        s.FrameCount.Should().Be(n.FrameCount);
        foreach (double t in SampleTimes.Concat(new[] { 20.5, 20.51, 15.333 }))
        {
            s.FrameAt(t).Should().Be(n.FrameAt(t));
            s.FormatTimecode(t).Should().Be(n.FormatTimecode(t));
            s.Progress("intro", t).Should().Be(n.Progress("intro", t));
            s.Progress("draw", t).Should().Be(n.Progress("draw", t));
            s.GetActiveCues(t).Select(c => c.Name).Should().Equal(n.GetActiveCues(t).Select(c => c.Name));
            sPlay.Advance(0.07).Should().Be(nPlay.Advance(0.07));
            sPlay.Time.Should().Be(nPlay.Time);
        }
    }

    [Fact]
    public void Transform_camera_and_layout_geometry_compute_identically()
    {
        //Arrange
        var sTransform = new SM.DrawingTransform(new PointF(12.5f, -3f), 33f, 1.7f, 0.6f, new PointF(40, 20));
        var nTransform = new NM.DrawingTransform(new PointF(12.5f, -3f), 33f, 1.7f, 0.6f, new PointF(40, 20));
        var sCamera = new SM.DrawingCamera { Pan = new PointF(30, 12), Zoom = 1.8f, Rotation = -17f };
        var nCamera = new NM.DrawingCamera { Pan = new PointF(30, 12), Zoom = 1.8f, Rotation = -17f };
        var bounds = new RectangleF(5, 7, 91, 33);
        var sLayout = new SL.DrawingLayout(new Size(1080, 1920), SSafe.SafeAreaGuides.InstagramReelsAd);
        var nLayout = new NL.DrawingLayout(new Size(1080, 1920), NSafe.SafeAreaGuides.InstagramReelsAd);

        //Act + Assert
        sTransform.ToMatrix().Should().Be(nTransform.ToMatrix());
        sTransform.TransformBounds(bounds).Should().Be(nTransform.TransformBounds(bounds));
        foreach (float parallax in new[] { 0f, 0.35f, 1f, 1.6f })
        {
            sCamera.GetMatrix(Frame, parallax).Should().Be(nCamera.GetMatrix(Frame, parallax));
            sCamera.ToView(new PointF(77, 31), Frame, parallax).Should().Be(nCamera.ToView(new PointF(77, 31), Frame, parallax));
        }
        sLayout.SafeArea.Should().Be(nLayout.SafeArea);
        sLayout.Place(new SL.LayoutPosition(SL.LayoutAnchor.BottomCenter, SL.LayoutReference.SafeArea, 3, -9, 0.1f, -0.05f))
            .Should().Be(nLayout.Place(new NL.LayoutPosition(NL.LayoutAnchor.BottomCenter, NL.LayoutReference.SafeArea, 3, -9, 0.1f, -0.05f)));
        SSafe.SafeAreaGuides.TikTok.GetSafeRectangle(new SizeF(1080, 1920))
            .Should().Be(NSafe.SafeAreaGuides.TikTok.GetSafeRectangle(new SizeF(1080, 1920)));
    }

    [Fact]
    public void Group_bounds_findings_and_overlay_lines_compute_identically()
    {
        //Arrange
        using SSession skiaSession = BuildSkiaScene(Color.Black);
        using NSession noSkiaSession = BuildNoSkiaScene(Color.Black);
        var sOverlay = new SR.DebugOverlay(null, new SA.AnimationTimeline(4, 25)) { ShowCues = true };
        var nOverlay = new NR.DebugOverlay(null, new NA.AnimationTimeline(4, 25)) { ShowCues = true };

        foreach (double t in new[] { 0.0, 0.8, 1.6, 2.4 })
        {
            //Act
            skiaSession.Time = t;
            noSkiaSession.Time = t;
            List<string> skiaFindings = skiaSession.CheckSafeArea(SSafe.SafeAreaGuides.BroadcastTitleSafe, Frame,
                new SSafe.SafeAreaCheckOptions { CheckAllElements = true }).Select(f => f.ToString()).ToList();
            List<string> noSkiaFindings = noSkiaSession.CheckSafeArea(NSafe.SafeAreaGuides.BroadcastTitleSafe, Frame,
                new NSafe.SafeAreaCheckOptions { CheckAllElements = true }).Select(f => f.ToString()).ToList();

            //Assert
            skiaFindings.Should().Equal(noSkiaFindings);
            sOverlay.GetLines(t).Should().Equal(nOverlay.GetLines(t));
        }
        AssertTheSceneHasFindings(skiaSession);
    }

    private static void AssertTheSceneHasFindings(SSession session)
    {
        session.CheckSafeArea(SSafe.SafeAreaGuides.BroadcastTitleSafe, Frame,
            new SSafe.SafeAreaCheckOptions { CheckAllElements = true }).Count.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.8, true)]
    [InlineData(1.6, false)]
    [InlineData(2.4, false)]
    public void Animated_scenes_draw_alike(double seconds, bool includeBackground)
    {
        //Arrange
        using SSession skiaSession = BuildSkiaScene(includeBackground ? Color.FromRgb(12, 14, 30) : Color.Transparent);
        using NSession noSkiaSession = BuildNoSkiaScene(includeBackground ? Color.FromRgb(12, 14, 30) : Color.Transparent);

        //Act
        skiaSession.Time = seconds;
        noSkiaSession.Time = seconds;
        byte[] skia = skiaSession.ExportPixels(Frame, includeBackground);
        byte[] noSkia = noSkiaSession.ExportPixels(Frame, includeBackground);

        //Assert
        ImageComparison.AssertSimilar(RawToPng(skia, Frame), RawToPng(noSkia, Frame), "animated scene at " + seconds + " s");
    }

    [Fact]
    public void Frame_sequences_draw_alike_whatever_the_thread_count()
    {
        //Arrange
        var sTimeline = new SA.AnimationTimeline(2.4, 5);
        var nTimeline = new NA.AnimationTimeline(2.4, 5);
        var skiaRenderer = new SSequence(sTimeline, () => BuildSkiaScene(Color.FromRgb(12, 14, 30)), Frame) { MaxDegreeOfParallelism = 3 };
        var noSkiaRenderer = new NSequence(nTimeline, () => BuildNoSkiaScene(Color.FromRgb(12, 14, 30)), Frame) { MaxDegreeOfParallelism = 2 };
        var skiaFrames = new List<byte[]>();
        var noSkiaFrames = new List<byte[]>();

        //Act
        skiaRenderer.Render(0, sTimeline.FrameCount - 1, (f, s, rgba) => skiaFrames.Add(rgba.ToArray()), TestContext.Current.CancellationToken);
        noSkiaRenderer.Render(0, nTimeline.FrameCount - 1, (f, s, rgba) => noSkiaFrames.Add(rgba.ToArray()), TestContext.Current.CancellationToken);

        //Assert
        skiaFrames.Count.Should().Be(noSkiaFrames.Count);
        for (int i = 0; i < skiaFrames.Count; i++)
        {
            ImageComparison.AssertSimilar(RawToPng(skiaFrames[i], Frame), RawToPng(noSkiaFrames[i], Frame), "sequence frame " + i);
        }
    }

    //The same scene in each assembly: transformed shapes, a faded rotating group, time windows,
    //  an animated shape, a layer transform and a camera with a parallax HUD layer
    private static SSession BuildSkiaScene(Color background)
    {
        var session = new SSession(new SSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = background });
        session.AddLayer("Scene", Color.FromRgb(240, 240, 250));
        for (int i = 0; i < 12; i++)
        {
            session.DrawLine(10 + (i * 25), 10, 300 - (i * 20), 190, thickness: 3, color: Color.FromRgb((byte)(i * 20), 140, 230)).StartTime = i * 0.2;
        }
        SS.DrawingShape ball = session.DrawCircle(30, 100, 16, filled: true, color: Color.FromRgb(255, 170, 60));
        session.Animations.Bind(ball, SM.AnimatedProperty.Position, new SA.PointTrack { { 0, new PointF(0, 0) }, { 2.4, new PointF(260, 0), SA.EasingKind.CubicInOut } });
        var group = new SS.DrawingGroup(Color.FromRgb(120, 255, 140));
        group.Add(new SS.RectangleShape(130, 60, 60, 60, 1, null, isFilled: true));
        group.Add(new SS.CircleShape(190, 90, 30, 1, null, isFilled: true));
        group.Opacity = 0.6f;
        session.Animations.Bind(group, SM.AnimatedProperty.Rotation, new SA.FloatTrack { { 0, 0f }, { 2.4, 120f } });
        session.Animations.Bind(group, SM.AnimatedProperty.Anchor, new SA.PointTrack { { 0, new PointF(170, 90) } });
        session.DrawShape(group);
        session.DrawShape(new SS.AnimatedShape(t => new SS.CircleShape(160, 170, 4 + (float)(t * 10), 2, Color.White)));
        session.ActiveLayer.Transform = new SM.DrawingTransform(new PointF(4, -6));
        session.ActiveLayer = session.AddLayer("Hud", Color.White);
        session.DrawRectangle(290, 8, 22, 22, filled: true);
        session.ActiveLayer.Parallax = 0f;
        session.Camera = new SM.DrawingCamera();
        session.Animations.Bind(session.Camera, SM.AnimatedProperty.Scale, new SA.FloatTrack { { 0, 1f }, { 2.4, 1.4f } });
        return session;
    }

    private static NSession BuildNoSkiaScene(Color background)
    {
        var session = new NSession(new NSessionOptions { CalibrationSize = Frame, LayerOpacity = 255, BackgroundFillColor = background });
        session.AddLayer("Scene", Color.FromRgb(240, 240, 250));
        for (int i = 0; i < 12; i++)
        {
            session.DrawLine(10 + (i * 25), 10, 300 - (i * 20), 190, thickness: 3, color: Color.FromRgb((byte)(i * 20), 140, 230)).StartTime = i * 0.2;
        }
        NS.DrawingShape ball = session.DrawCircle(30, 100, 16, filled: true, color: Color.FromRgb(255, 170, 60));
        session.Animations.Bind(ball, NM.AnimatedProperty.Position, new NA.PointTrack { { 0, new PointF(0, 0) }, { 2.4, new PointF(260, 0), NA.EasingKind.CubicInOut } });
        var group = new NS.DrawingGroup(Color.FromRgb(120, 255, 140));
        group.Add(new NS.RectangleShape(130, 60, 60, 60, 1, null, isFilled: true));
        group.Add(new NS.CircleShape(190, 90, 30, 1, null, isFilled: true));
        group.Opacity = 0.6f;
        session.Animations.Bind(group, NM.AnimatedProperty.Rotation, new NA.FloatTrack { { 0, 0f }, { 2.4, 120f } });
        session.Animations.Bind(group, NM.AnimatedProperty.Anchor, new NA.PointTrack { { 0, new PointF(170, 90) } });
        session.DrawShape(group);
        session.DrawShape(new NS.AnimatedShape(t => new NS.CircleShape(160, 170, 4 + (float)(t * 10), 2, Color.White)));
        session.ActiveLayer.Transform = new NM.DrawingTransform(new PointF(4, -6));
        session.ActiveLayer = session.AddLayer("Hud", Color.White);
        session.DrawRectangle(290, 8, 22, 22, filled: true);
        session.ActiveLayer.Parallax = 0f;
        session.Camera = new NM.DrawingCamera();
        session.Animations.Bind(session.Camera, NM.AnimatedProperty.Scale, new NA.FloatTrack { { 0, 1f }, { 2.4, 1.4f } });
        return session;
    }
}
