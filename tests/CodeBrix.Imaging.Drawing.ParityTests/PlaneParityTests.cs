extern alias noskia;
extern alias skia;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;
using N = noskia::CodeBrix.Imaging.Drawing.Planes;
using S = skia::CodeBrix.Imaging.Drawing.Planes;

namespace CodeBrix.Imaging.Drawing.ParityTests;

/// <summary>
/// Runs identical pixel-plane work - float planes and images, filters, compositing, 8-bit
/// conversion, transitions, seeded randomness, noise, particles and motion blur - through the
/// SkiaSharp-backed assembly (alias <c>skia</c>) and the fully managed assembly (alias
/// <c>noskia</c>) and asserts the results are BYTE-FOR-BYTE identical. Unlike the session parity
/// suite there is no tolerance: this code is computed in shared managed arithmetic, never by a
/// rasterizer, so the two packages must agree exactly. Each scenario is written once per alias
/// (the two assemblies declare the same type names, so the code cannot be shared generically).
/// </summary>
public class PlaneParityTests
{
    private const int Width = 160;
    private const int Height = 96;

    private static readonly (float Sigma, float Weight)[] Blurs = { (1.5f, 0.8f), (5f, 0.9f) };

    //Deterministic RGBA test frames: an opaque gradient and a partly transparent pattern
    private static byte[] FrameA()
    {
        var rgba = new byte[Width * Height * 4];
        for (int i = 0, o = 0; i < Width * Height; i++, o += 4)
        {
            int x = i % Width, y = i / Width;
            rgba[o] = (byte)(x * 255 / Width);
            rgba[o + 1] = (byte)(y * 255 / Height);
            rgba[o + 2] = (byte)((x ^ y) & 255);
            rgba[o + 3] = 255;
        }
        return rgba;
    }

    private static byte[] FrameB()
    {
        var rgba = new byte[Width * Height * 4];
        for (int i = 0, o = 0; i < Width * Height; i++, o += 4)
        {
            int x = i % Width, y = i / Width;
            rgba[o] = (byte)(200 - (y % 50));
            rgba[o + 1] = (byte)((x * 7) & 255);
            rgba[o + 2] = 40;
            rgba[o + 3] = (byte)(((x / 8) + (y / 8)) % 3 == 0 ? 0 : ((x * 3) & 255));
        }
        return rgba;
    }

    private static Vector4 Pattern(int x, int y)
        => new Vector4(MathF.Sin(x * 0.07f) + 1.2f, (y % 13) / 12f, ((x * y) % 97) / 48f, x < 20 ? 0f : ((x + (2 * y)) % 11) / 10f);

    private static void AssertSameBytes(byte[] skia, byte[] noSkia)
    {
        skia.Length.Should().Be(noSkia.Length);
        skia.Should().Equal(noSkia);
    }

    private static byte[] FloatBytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void The_two_aliases_are_different_assemblies()
    {
        //Arrange + Act
        string skiaAssembly = typeof(S.DrawingPlane).Assembly.GetName().Name;
        string noSkiaAssembly = typeof(N.DrawingPlane).Assembly.GetName().Name;

        //Assert - otherwise every other test here would compare one assembly with itself
        skiaAssembly.Should().Be("CodeBrix.Imaging.Drawing");
        noSkiaAssembly.Should().Be("CodeBrix.Imaging.Drawing.NoSkia");
    }

    //---------------------------------------------------------------------------------------------
    //Planes and images

    private static byte[] SkiaImagePipeline()
    {
        S.DrawingPlaneImage image = new S.DrawingPlaneImage(Width, Height).FillParallel(Pattern);
        S.DrawingPlaneImage glow = image.CreateBloom(4, Blurs, 0.6f);
        image.CompositeAdd(glow).GaussianBlur(2.5f).ApplyVignette(80f, 46f, 120f, 60f, 0.38f, 0.4f)
            .ApplyToneCurve(0.15f, 1.08f).ApplyGamma(1f / 2.2f);
        S.DrawingPlaneImage half = image.Downsampled(2).Resized(Width, Height, S.DrawingPlaneResampling.Bilinear);
        S.DrawingPlaneImage corners = image.Resized(37, 23, S.DrawingPlaneResampling.BilinearAlignedCorners)
            .Resized(Width, Height, S.DrawingPlaneResampling.Nearest);
        S.DrawingPlane mask = new S.DrawingPlane(Width, Height).Fill((x, y) => (x % 32) / 31f);
        image.Lerp(half, 0.3f).BlendImage(corners, mask).BlendColor(Color.FromRgba(255, 180, 40, 200), mask.Clone().Multiply(0.5f))
            .CompositeOver(half, 0.4f).Lerp(corners, mask).Multiply(1.1f, 0.9f, 1f).Add(0.02f, 0f, -0.01f).Clamp(0f, 4f);
        return image.ToRgba();
    }

    private static byte[] NoSkiaImagePipeline()
    {
        N.DrawingPlaneImage image = new N.DrawingPlaneImage(Width, Height).FillParallel(Pattern);
        N.DrawingPlaneImage glow = image.CreateBloom(4, Blurs, 0.6f);
        image.CompositeAdd(glow).GaussianBlur(2.5f).ApplyVignette(80f, 46f, 120f, 60f, 0.38f, 0.4f)
            .ApplyToneCurve(0.15f, 1.08f).ApplyGamma(1f / 2.2f);
        N.DrawingPlaneImage half = image.Downsampled(2).Resized(Width, Height, N.DrawingPlaneResampling.Bilinear);
        N.DrawingPlaneImage corners = image.Resized(37, 23, N.DrawingPlaneResampling.BilinearAlignedCorners)
            .Resized(Width, Height, N.DrawingPlaneResampling.Nearest);
        N.DrawingPlane mask = new N.DrawingPlane(Width, Height).Fill((x, y) => (x % 32) / 31f);
        image.Lerp(half, 0.3f).BlendImage(corners, mask).BlendColor(Color.FromRgba(255, 180, 40, 200), mask.Clone().Multiply(0.5f))
            .CompositeOver(half, 0.4f).Lerp(corners, mask).Multiply(1.1f, 0.9f, 1f).Add(0.02f, 0f, -0.01f).Clamp(0f, 4f);
        return image.ToRgba();
    }

    [Fact]
    public void Float_image_pipeline_is_byte_identical()
    {
        //Arrange + Act
        byte[] skia = SkiaImagePipeline();
        byte[] noSkia = NoSkiaImagePipeline();

        //Assert - and the frame really carries translucency
        AssertSameBytes(skia, noSkia);
        skia.Where((b, i) => i % 4 == 3).Should().Contain(alpha => alpha < 255);
    }

    private static float[] SkiaPlanePipeline()
    {
        S.DrawingPlane plane = new S.DrawingPlane(Width, Height).Fill((x, y) => ((x * 31) + (y * 17)) % 23 / 7f);
        S.DrawingPlane bloom = plane.CreateBloom(4, Blurs, 0.6f, new Size(Width * 2, Height * 2), S.DrawingPlaneResampling.BilinearAlignedCorners)
            .Downsampled(2);
        plane.GaussianBlur(3.2f).Add(bloom, 0.5f).ApplyToneCurve(0.15f, 1.08f).ApplyVignette(70f, 40f, 90f, 70f, 0.5f, 0.2f)
            .ApplyGamma(0.8f).Lerp(bloom, 0.25f).Min(1.5f).Max(0.01f);
        S.DrawingPlane vignette = S.DrawingPlane.CreateVignette(Width, Height, 80f, 46f, 120f, 60f, 0.38f, 0.4f);
        return plane.Multiply(vignette).Resized(201, 77, S.DrawingPlaneResampling.Bilinear).Data;
    }

    private static float[] NoSkiaPlanePipeline()
    {
        N.DrawingPlane plane = new N.DrawingPlane(Width, Height).Fill((x, y) => ((x * 31) + (y * 17)) % 23 / 7f);
        N.DrawingPlane bloom = plane.CreateBloom(4, Blurs, 0.6f, new Size(Width * 2, Height * 2), N.DrawingPlaneResampling.BilinearAlignedCorners)
            .Downsampled(2);
        plane.GaussianBlur(3.2f).Add(bloom, 0.5f).ApplyToneCurve(0.15f, 1.08f).ApplyVignette(70f, 40f, 90f, 70f, 0.5f, 0.2f)
            .ApplyGamma(0.8f).Lerp(bloom, 0.25f).Min(1.5f).Max(0.01f);
        N.DrawingPlane vignette = N.DrawingPlane.CreateVignette(Width, Height, 80f, 46f, 120f, 60f, 0.38f, 0.4f);
        return plane.Multiply(vignette).Resized(201, 77, N.DrawingPlaneResampling.Bilinear).Data;
    }

    [Fact]
    public void Float_plane_pipeline_is_bit_identical()
    {
        //Arrange + Act
        float[] skia = SkiaPlanePipeline();
        float[] noSkia = NoSkiaPlanePipeline();

        //Assert - compare the raw float bits, not just the rounded bytes
        AssertSameBytes(FloatBytes(skia), FloatBytes(noSkia));
    }

    [Fact]
    public void Eight_bit_and_imaging_conversions_are_identical()
    {
        //Arrange
        byte[] frame = FrameB();

        //Act
        byte[] skiaBytes = S.DrawingPlaneImage.FromRgba(frame, Width, Height).ToRgba();
        byte[] noSkiaBytes = N.DrawingPlaneImage.FromRgba(frame, Width, Height).ToRgba();
        using Image<Rgba32> skiaImage = S.DrawingPlaneImage.FromRgba(frame, Width, Height).ToImagingImage();
        using Image<Rgba32> noSkiaImage = N.DrawingPlaneImage.FromRgba(frame, Width, Height).ToImagingImage();
        byte[] skiaRoundTrip = S.DrawingPlaneImage.FromImagingImage(skiaImage).ToRgba();
        byte[] noSkiaRoundTrip = N.DrawingPlaneImage.FromImagingImage(noSkiaImage).ToRgba();

        //Assert - and both round-trip the transparent frame exactly
        AssertSameBytes(skiaBytes, noSkiaBytes);
        AssertSameBytes(skiaRoundTrip, noSkiaRoundTrip);
        skiaBytes.Should().Equal(frame);
        skiaRoundTrip.Should().Equal(frame);
    }

    //---------------------------------------------------------------------------------------------
    //Transitions

    private static IEnumerable<S.DrawingTransition> SkiaTransitions()
    {
        yield return S.DrawingTransition.Dissolve();
        yield return S.DrawingTransition.FadeThroughColor(Color.Black);
        yield return S.DrawingTransition.FadeThroughColor(Color.Transparent);
        foreach (S.DrawingTransitionDirection direction in Enum.GetValues<S.DrawingTransitionDirection>())
        {
            yield return S.DrawingTransition.Wipe(direction);
            yield return S.DrawingTransition.Wipe(direction, 12.5f);
            yield return S.DrawingTransition.Slide(direction);
            yield return S.DrawingTransition.Push(direction);
        }
    }

    private static IEnumerable<N.DrawingTransition> NoSkiaTransitions()
    {
        yield return N.DrawingTransition.Dissolve();
        yield return N.DrawingTransition.FadeThroughColor(Color.Black);
        yield return N.DrawingTransition.FadeThroughColor(Color.Transparent);
        foreach (N.DrawingTransitionDirection direction in Enum.GetValues<N.DrawingTransitionDirection>())
        {
            yield return N.DrawingTransition.Wipe(direction);
            yield return N.DrawingTransition.Wipe(direction, 12.5f);
            yield return N.DrawingTransition.Slide(direction);
            yield return N.DrawingTransition.Push(direction);
        }
    }

    private static readonly float[] Fractions = { 0f, 0.13f, 0.5f, 0.77f, 1f };

    [Fact]
    public void Byte_frame_transitions_are_byte_identical()
    {
        //Arrange
        byte[] a = FrameA();
        byte[] b = FrameB();
        S.DrawingTransition[] skiaTransitions = SkiaTransitions().ToArray();
        N.DrawingTransition[] noSkiaTransitions = NoSkiaTransitions().ToArray();
        var skia = new byte[a.Length];
        var noSkia = new byte[a.Length];

        //Act + Assert
        noSkiaTransitions.Length.Should().Be(skiaTransitions.Length);
        for (int t = 0; t < skiaTransitions.Length; t++)
        {
            foreach (float fraction in Fractions)
            {
                skiaTransitions[t].Apply(a, b, skia, Width, Height, fraction);
                noSkiaTransitions[t].Apply(a, b, noSkia, Width, Height, fraction);
                AssertSameBytes(skia, noSkia);
            }
        }
    }

    [Fact]
    public void Float_image_transitions_are_bit_identical()
    {
        //Arrange
        S.DrawingPlaneImage skiaA = S.DrawingPlaneImage.FromRgba(FrameA(), Width, Height);
        S.DrawingPlaneImage skiaB = new S.DrawingPlaneImage(Width, Height).Fill(Pattern);
        N.DrawingPlaneImage noSkiaA = N.DrawingPlaneImage.FromRgba(FrameA(), Width, Height);
        N.DrawingPlaneImage noSkiaB = new N.DrawingPlaneImage(Width, Height).Fill(Pattern);
        S.DrawingTransition[] skiaTransitions = SkiaTransitions().ToArray();
        N.DrawingTransition[] noSkiaTransitions = NoSkiaTransitions().ToArray();

        //Act + Assert
        for (int t = 0; t < skiaTransitions.Length; t++)
        {
            foreach (float fraction in Fractions)
            {
                S.DrawingPlaneImage skia = skiaTransitions[t].Apply(skiaA, skiaB, fraction);
                N.DrawingPlaneImage noSkia = noSkiaTransitions[t].Apply(noSkiaA, noSkiaB, fraction);
                AssertSameBytes(FloatBytes(skia.R.Data), FloatBytes(noSkia.R.Data));
                AssertSameBytes(FloatBytes(skia.A.Data), FloatBytes(noSkia.A.Data));
                AssertSameBytes(skia.ToRgba(), noSkia.ToRgba());
            }
        }
    }

    //---------------------------------------------------------------------------------------------
    //Randomness, noise, particles, motion blur

    [Fact]
    public void Seeded_random_sequences_are_identical()
    {
        //Arrange
        var skia = new S.DrawingRandom(31337);
        var noSkia = new N.DrawingRandom(31337);
        S.DrawingRandom skiaKeyed = S.DrawingRandom.ForKey(5, 99);
        N.DrawingRandom noSkiaKeyed = N.DrawingRandom.ForKey(5, 99);

        //Act + Assert
        for (int i = 0; i < 2000; i++)
        {
            skia.NextUInt64().Should().Be(noSkia.NextUInt64());
            skia.Normal(1, 2).Should().Be(noSkia.Normal(1, 2));
            skia.NextInt(-7, 1000).Should().Be(noSkia.NextInt(-7, 1000));
            skia.NextSingle().Should().Be(noSkia.NextSingle());
            skiaKeyed.Uniform(-1, 1).Should().Be(noSkiaKeyed.Uniform(-1, 1));
        }
    }

    [Fact]
    public void Noise_fills_are_bit_identical()
    {
        //Arrange
        var skiaValue = new S.DrawingPlane(Width, Height);
        var noSkiaValue = new N.DrawingPlane(Width, Height);
        var skiaGradient = new S.DrawingPlane(Width, Height);
        var noSkiaGradient = new N.DrawingPlane(Width, Height);

        //Act
        S.DrawingNoise.Fill(skiaValue, S.DrawingNoiseKind.Value, 8, 0.05, null, 5, 2.1, 0.45, 3.5, -7);
        N.DrawingNoise.Fill(noSkiaValue, N.DrawingNoiseKind.Value, 8, 0.05, null, 5, 2.1, 0.45, 3.5, -7);
        S.DrawingNoise.Fill(skiaGradient, S.DrawingNoiseKind.Gradient, -3, 0.031, 12.75, 4);
        N.DrawingNoise.Fill(noSkiaGradient, N.DrawingNoiseKind.Gradient, -3, 0.031, 12.75, 4);

        //Assert
        AssertSameBytes(FloatBytes(skiaValue.Data), FloatBytes(noSkiaValue.Data));
        AssertSameBytes(FloatBytes(skiaGradient.Data), FloatBytes(noSkiaGradient.Data));
    }

    private static S.DrawingParticleEmitter SkiaEmitter(float x) => new S.DrawingParticleEmitter
    {
        X = x, Y = 300f, Rate = 45f, Burst = 12, StartTime = 0.5, EndTime = 6.0, Lifetime = 2.5f, LifetimeVariation = 0.7f,
        Direction = -80f, Spread = 63f, Speed = 160f, SpeedVariation = 40f, GravityY = 120f, DriftX = 8f, SpawnRadius = 6f,
        StartSize = 9f, EndSize = 2f, SizeVariation = 0.3f, StartOpacity = 1f, EndOpacity = 0f,
        StartColor = Color.FromRgba(255, 230, 120, 255), EndColor = Color.FromRgba(200, 40, 10, 60),
    };

    private static N.DrawingParticleEmitter NoSkiaEmitter(float x) => new N.DrawingParticleEmitter
    {
        X = x, Y = 300f, Rate = 45f, Burst = 12, StartTime = 0.5, EndTime = 6.0, Lifetime = 2.5f, LifetimeVariation = 0.7f,
        Direction = -80f, Spread = 63f, Speed = 160f, SpeedVariation = 40f, GravityY = 120f, DriftX = 8f, SpawnRadius = 6f,
        StartSize = 9f, EndSize = 2f, SizeVariation = 0.3f, StartOpacity = 1f, EndOpacity = 0f,
        StartColor = Color.FromRgba(255, 230, 120, 255), EndColor = Color.FromRgba(200, 40, 10, 60),
    };

    [Fact]
    public void Particle_systems_are_identical_at_every_time()
    {
        //Arrange
        var skia = new S.DrawingParticleSystem(2026);
        var noSkia = new N.DrawingParticleSystem(2026);
        skia.AddEmitter(SkiaEmitter(100f));
        skia.AddEmitter(SkiaEmitter(400f));
        noSkia.AddEmitter(NoSkiaEmitter(100f));
        noSkia.AddEmitter(NoSkiaEmitter(400f));

        //Act + Assert
        for (double time = 0; time <= 8; time += 0.37)
        {
            var skiaParticles = skia.GetParticles(time)
                .Select(p => (p.EmitterIndex, p.Index, p.BirthTime, p.Age, p.Lifetime, p.X, p.Y, p.Size, p.Opacity, p.Color.ToHex())).ToList();
            var noSkiaParticles = noSkia.GetParticles(time)
                .Select(p => (p.EmitterIndex, p.Index, p.BirthTime, p.Age, p.Lifetime, p.X, p.Y, p.Size, p.Opacity, p.Color.ToHex())).ToList();
            noSkiaParticles.Should().Equal(skiaParticles);
        }
    }

    [Fact]
    public void Motion_blur_is_identical_for_byte_frames_and_float_images()
    {
        //Arrange
        var frames = new List<byte[]> { FrameA(), FrameB(), FrameA().Select((v, i) => (byte)(i % 4 == 3 ? v : 255 - v)).ToArray() };

        //Act
        byte[] skiaBytes = S.DrawingMotionBlur.Average(frames, Width, Height);
        byte[] noSkiaBytes = N.DrawingMotionBlur.Average(frames, Width, Height);
        S.DrawingPlaneImage skiaImage = S.DrawingMotionBlur.Average(frames.Select(f => S.DrawingPlaneImage.FromRgba(f, Width, Height)).ToList());
        N.DrawingPlaneImage noSkiaImage = N.DrawingMotionBlur.Average(frames.Select(f => N.DrawingPlaneImage.FromRgba(f, Width, Height)).ToList());

        //Assert
        AssertSameBytes(skiaBytes, noSkiaBytes);
        AssertSameBytes(FloatBytes(skiaImage.G.Data), FloatBytes(noSkiaImage.G.Data));
        AssertSameBytes(FloatBytes(skiaImage.A.Data), FloatBytes(noSkiaImage.A.Data));
        AssertSameBytes(skiaImage.ToRgba(), noSkiaImage.ToRgba());
    }
}
