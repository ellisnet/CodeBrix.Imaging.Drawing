using System;
using CodeBrix.Imaging.Drawing.Planes;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Tests.TestSupport;
using CodeBrix.Imaging.PixelFormats;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Shapes;

public class ParticlesShapeTests
{
    //One motionless particle of diameter `size` at (500, 500), alive from 0 to 10 s
    private static DrawingParticleSystem StillDot(float size, float opacity, Color color)
    {
        var system = new DrawingParticleSystem(7);
        system.AddEmitter(new DrawingParticleEmitter
        {
            X = 500f, Y = 500f, Burst = 1, Lifetime = 10f, Speed = 0f,
            StartSize = size, EndSize = size, StartOpacity = opacity, EndOpacity = opacity,
            StartColor = color, EndColor = color,
        });
        return system;
    }

    private static byte[] Render(DrawingParticleSystem system, double time, float softness)
    {
        using var session = new DrawingSession(new DrawingSessionOptions { LayerOpacity = 255 });
        session.AddLayer("Particles", Color.Black);
        session.DrawParticles(system, time, softness);
        return session.ExportPixels(new Size(200, 200), includeBackground: false);
    }

    [Fact]
    public void Constructor_takes_the_particles_of_that_moment()
    {
        //Arrange
        var system = new DrawingParticleSystem(3);
        system.AddEmitter(new DrawingParticleEmitter { X = 100f, Y = 200f, Rate = 20f, Lifetime = 1.5f, Speed = 50f, Spread = 57f });

        //Act
        var shape = new ParticlesShape(system, 2.25);

        //Assert
        shape.ParticleSystem.Should().BeSameAs(system);
        shape.Time.Should().Be(2.25);
        shape.Softness.Should().Be(1f);
        shape.Particles.Should().Equal(system.GetParticles(2.25));
        shape.Particles.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Particles_are_a_snapshot_that_later_emitter_changes_do_not_touch()
    {
        //Arrange
        DrawingParticleSystem system = StillDot(40f, 1f, Color.White);
        var shape = new ParticlesShape(system, 1.0);

        //Act
        system.Emitters[0].X = 900f;

        //Assert
        shape.Particles[0].X.Should().Be(500f);
    }

    [Fact]
    public void A_soft_dot_is_full_colour_at_the_centre_and_fades_to_nothing_at_the_edge()
    {
        //Arrange - diameter 400 calibrated = radius 40 px in a 200 px export of the 1000 unit drawing
        DrawingParticleSystem system = StillDot(400f, 1f, Color.FromRgb(255, 180, 40));

        //Act
        byte[] pixels = Render(system, 1.0, 1f);

        //Assert - straight-alpha colour stays the particle's colour while alpha falls off linearly
        Rgba32 centre = TestAssets.PixelAt(pixels, 200, 100, 100);
        centre.A.Should().BeGreaterThan((byte)240);
        centre.R.Should().BeInRange((byte)250, (byte)255);
        centre.G.Should().BeInRange((byte)175, (byte)185);
        Rgba32 half = TestAssets.PixelAt(pixels, 200, 120, 100);
        half.A.Should().BeInRange((byte)115, (byte)140);
        half.G.Should().BeInRange((byte)172, (byte)188);
        TestAssets.PixelAt(pixels, 200, 145, 100).A.Should().Be((byte)0);
    }

    [Fact]
    public void Zero_softness_draws_a_solid_disc_at_the_particle_opacity()
    {
        //Arrange
        DrawingParticleSystem system = StillDot(400f, 0.5f, Color.FromRgb(40, 90, 250));

        //Act
        byte[] pixels = Render(system, 1.0, 0f);

        //Assert - alpha = colour alpha x opacity, the same across the disc
        TestAssets.PixelAt(pixels, 200, 100, 100).A.Should().BeInRange((byte)126, (byte)130);
        TestAssets.PixelAt(pixels, 200, 130, 100).A.Should().BeInRange((byte)126, (byte)130);
        TestAssets.PixelAt(pixels, 200, 130, 100).B.Should().BeInRange((byte)248, (byte)252);
        TestAssets.PixelAt(pixels, 200, 145, 100).A.Should().Be((byte)0);
    }

    [Fact]
    public void Nothing_is_drawn_before_a_particle_is_born()
    {
        //Arrange
        DrawingParticleSystem system = StillDot(400f, 1f, Color.White);
        system.Emitters[0].StartTime = 5.0;

        //Act
        byte[] pixels = Render(system, 1.0, 1f);
        var shape = new ParticlesShape(system, 1.0);

        //Assert
        TestAssets.CountInked(pixels).Should().Be(0);
        shape.Particles.Count.Should().Be(0);
        shape.GetBounds().Should().Be(RectangleF.Empty);
    }

    [Fact]
    public void GetBounds_is_the_union_of_the_dots()
    {
        //Arrange
        DrawingParticleSystem system = StillDot(40f, 1f, Color.White);
        system.AddEmitter(new DrawingParticleEmitter { X = 100f, Y = 700f, Burst = 1, Lifetime = 10f, StartSize = 10f, EndSize = 10f });

        //Act
        RectangleF? bounds = new ParticlesShape(system, 1.0).GetBounds();

        //Assert
        bounds.Should().Be(new RectangleF(95f, 480f, 425f, 225f));
    }

    [Fact]
    public void Constructor_rejects_bad_arguments()
    {
        //Arrange
        DrawingParticleSystem system = StillDot(10f, 1f, Color.White);

        //Act
        Action noSystem = () => _ = new ParticlesShape(null, 0);
        Action badTime = () => _ = new ParticlesShape(system, double.NaN);
        Action badSoftness = () => _ = new ParticlesShape(system, 0, 1.5f);

        //Assert
        noSystem.Should().Throw<ArgumentNullException>();
        badTime.Should().Throw<ArgumentOutOfRangeException>();
        badSoftness.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DrawParticles_adds_the_shape_to_the_active_layer()
    {
        //Arrange
        using var session = new DrawingSession();
        session.AddLayer("Particles", Color.Black);

        //Act
        ParticlesShape shape = session.DrawParticles(StillDot(40f, 1f, Color.White), 1.0);

        //Assert
        session.Layers[0].GetElements().Should().Contain(shape);
    }
}
