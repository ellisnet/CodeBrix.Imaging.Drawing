using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Imaging.Drawing.Planes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Imaging.Drawing.Tests.Planes;

public class DrawingParticleSystemTests
{
    private static DrawingParticleSystem Fountain(long seed)
    {
        var system = new DrawingParticleSystem(seed);
        system.AddEmitter(new DrawingParticleEmitter
        {
            X = 100f,
            Y = 200f,
            Rate = 30f,
            Burst = 5,
            Lifetime = 2f,
            LifetimeVariation = 0.5f,
            Direction = -90f,
            Spread = 46f,
            Speed = 120f,
            SpeedVariation = 30f,
            GravityY = 98f,
            DriftX = 5f,
            SpawnRadius = 3f,
            StartSize = 6f,
            EndSize = 1f,
            SizeVariation = 0.2f,
            StartOpacity = 1f,
            EndOpacity = 0f,
            StartColor = Color.Yellow,
            EndColor = Color.Red,
        });
        return system;
    }

    [Fact]
    public void Burst_particles_are_born_at_start_time()
    {
        //Arrange
        var system = new DrawingParticleSystem(1);
        system.AddEmitter(new DrawingParticleEmitter { Burst = 4, StartTime = 1.0, Lifetime = 1f });

        //Act
        IReadOnlyList<DrawingParticle> before = system.GetParticles(0.99);
        IReadOnlyList<DrawingParticle> during = system.GetParticles(1.5);
        IReadOnlyList<DrawingParticle> after = system.GetParticles(2.0);

        //Assert
        before.Should().BeEmpty();
        during.Select(p => p.Index).Should().Equal(0L, 1L, 2L, 3L);
        during.Should().OnlyContain(p => p.BirthTime == 1.0 && p.Age == 0.5);
        after.Should().BeEmpty();
    }

    [Fact]
    public void Continuous_particles_are_born_every_one_over_rate_seconds()
    {
        //Arrange
        var system = new DrawingParticleSystem(1);
        system.AddEmitter(new DrawingParticleEmitter { Rate = 4f, Lifetime = 100f });

        //Act
        IReadOnlyList<DrawingParticle> particles = system.GetParticles(1.0);

        //Assert - births at 0, 0.25, 0.5, 0.75 and 1.0
        particles.Select(p => p.BirthTime).Should().Equal(0.0, 0.25, 0.5, 0.75, 1.0);
        particles.Select(p => p.Index).Should().Equal(0L, 1L, 2L, 3L, 4L);
    }

    [Fact]
    public void Particles_die_after_their_lifetime_and_emission_stops_at_stop_time()
    {
        //Arrange
        var system = new DrawingParticleSystem(1);
        system.AddEmitter(new DrawingParticleEmitter { Rate = 2f, Lifetime = 1f, EndTime = 3.0 });

        //Act
        IReadOnlyList<DrawingParticle> at2 = system.GetParticles(2.25);
        IReadOnlyList<DrawingParticle> at4 = system.GetParticles(3.75);

        //Assert - at 2.25 the particles born at 1.5 and 2.0 live; the last one (2.5) dies at 3.5
        at2.Select(p => p.BirthTime).Should().Equal(1.5, 2.0);
        at4.Should().BeEmpty();
    }

    [Fact]
    public void Motion_follows_the_documented_ballistic_formula()
    {
        //Arrange - no randomness: no spread or variations, no spawn radius
        var system = new DrawingParticleSystem(3);
        system.AddEmitter(new DrawingParticleEmitter
        {
            X = 10f, Y = 20f, Burst = 1, Lifetime = 10f, Direction = 0f, Speed = 50f,
            GravityX = 2f, GravityY = 9.75f, DriftX = 1f, DriftY = -3f,
            StartSize = 10f, EndSize = 20f, StartOpacity = 1f, EndOpacity = 0.5f,
            StartColor = Color.FromRgba(0, 100, 200, 255), EndColor = Color.FromRgba(100, 100, 0, 55),
        });
        const double age = 2.5;

        //Act
        DrawingParticle p = system.GetParticles(age)[0];

        //Assert
        double vx = (50.0 * Math.Cos(0.0)) + 1.0, vy = (50.0 * Math.Sin(0.0)) - 3.0;
        p.X.Should().Be((float)(10.0 + (0.0 * Math.Cos(0)) + (vx * age) + (0.5 * 2.0 * age * age)));
        p.Y.Should().Be((float)(20.0 + (vy * age) + (0.5 * 9.75 * age * age)));
        p.LifeFraction.Should().Be(0.25);
        p.Size.Should().Be(12.5f);
        p.Opacity.Should().Be(0.875f);
        p.Color.Should().Be(Color.FromRgba(25, 100, 150, 205));
    }

    [Fact]
    public void Direction_is_in_degrees_clockwise_on_screen()
    {
        //Arrange - one particle launched at 90 degrees: straight DOWN the screen (y points down)
        var system = new DrawingParticleSystem(5);
        system.AddEmitter(new DrawingParticleEmitter { X = 10f, Y = 20f, Burst = 1, Lifetime = 10.0, Direction = 90f, Speed = 40f });

        //Act
        DrawingParticle p = system.GetParticles(2.0)[0];

        //Assert - the documented rule: radians = degrees x (pi / 180), in double
        p.X.Should().Be((float)(10.0 + (40.0 * Math.Cos(90.0 * (Math.PI / 180.0)) * 2.0)));
        p.Y.Should().Be((float)(20.0 + (40.0 * Math.Sin(90.0 * (Math.PI / 180.0)) * 2.0)));
        p.X.Should().BeApproximately(10f, 1e-4f);
        p.Y.Should().BeApproximately(100f, 1e-4f);
    }

    [Fact]
    public void Lifetimes_are_double_seconds_and_end_time_stops_emission()
    {
        //Arrange - births at 0, 0.1, 0.2, ... until 0.3 (exclusive)
        var system = new DrawingParticleSystem(9);
        system.AddEmitter(new DrawingParticleEmitter { Rate = 10f, EndTime = 0.3, Lifetime = 0.35, LifetimeVariation = 0.0 });

        //Act
        IReadOnlyList<DrawingParticle> early = system.GetParticles(0.29);
        IReadOnlyList<DrawingParticle> late = system.GetParticles(0.5);

        //Assert
        early.Select(p => p.Index).Should().Equal(0L, 1L, 2L);
        early[0].Lifetime.Should().Be(0.35);
        late.Select(p => p.Index).Should().Equal(2L);
    }

    [Fact]
    public void Same_seed_gives_identical_particles()
    {
        //Arrange
        DrawingParticleSystem a = Fountain(77);
        DrawingParticleSystem b = Fountain(77);

        //Act
        IReadOnlyList<DrawingParticle> first = a.GetParticles(3.3);
        IReadOnlyList<DrawingParticle> second = b.GetParticles(3.3);

        //Assert
        first.Count.Should().BeGreaterThan(20);
        second.Should().Equal(first);
    }

    [Fact]
    public void Different_seeds_give_different_particles()
    {
        //Arrange + Act
        IReadOnlyList<DrawingParticle> first = Fountain(1).GetParticles(1.0);
        IReadOnlyList<DrawingParticle> second = Fountain(2).GetParticles(1.0);

        //Assert
        second.Select(p => p.X).Should().NotEqual(first.Select(p => p.X));
    }

    [Fact]
    public void A_query_is_a_pure_function_of_time()
    {
        //Arrange
        DrawingParticleSystem system = Fountain(9);
        IReadOnlyList<DrawingParticle> fresh = Fountain(9).GetParticles(2.0);

        //Act - query other times first, in a scrambled order
        system.GetParticles(5.0);
        system.GetParticles(0.1);
        system.GetParticles(2.7);
        IReadOnlyList<DrawingParticle> later = system.GetParticles(2.0);

        //Assert
        later.Should().Equal(fresh);
    }

    [Fact]
    public void Queries_from_many_threads_agree()
    {
        //Arrange
        DrawingParticleSystem system = Fountain(12);
        double[] times = Enumerable.Range(0, 64).Select(i => i * 0.05).ToArray();
        IReadOnlyList<DrawingParticle>[] expected = times.Select(t => system.GetParticles(t)).ToArray();
        var actual = new IReadOnlyList<DrawingParticle>[times.Length];

        //Act
        Parallel.For(0, times.Length, i => actual[i] = system.GetParticles(times[times.Length - 1 - i]));

        //Assert
        for (int i = 0; i < times.Length; i++) { actual[times.Length - 1 - i].Should().Equal(expected[i]); }
    }

    [Fact]
    public void Adding_an_emitter_never_changes_the_existing_emitters_particles()
    {
        //Arrange
        DrawingParticleSystem system = Fountain(4);
        IReadOnlyList<DrawingParticle> before = system.GetParticles(1.7);

        //Act
        system.AddEmitter(new DrawingParticleEmitter { Rate = 50f, Burst = 3, Lifetime = 4f, Speed = 10f, Spread = 344f });
        IReadOnlyList<DrawingParticle> after = system.GetParticles(1.7);

        //Assert
        after.Where(p => p.EmitterIndex == 0).Should().Equal(before);
        after.Should().Contain(p => p.EmitterIndex == 1);
    }

    [Fact]
    public void Random_choices_stay_within_their_ranges()
    {
        //Arrange
        DrawingParticleSystem system = Fountain(5);

        //Act
        IReadOnlyList<DrawingParticle> particles = system.GetParticles(2.2);

        //Assert
        particles.Should().OnlyContain(p => p.Lifetime >= 1.5 && p.Lifetime <= 2.5);
        particles.Should().OnlyContain(p => p.Age >= 0 && p.Age < p.Lifetime);
        particles.Should().OnlyContain(p => p.Opacity >= 0f && p.Opacity <= 1f);
        particles.Should().OnlyContain(p => p.Size >= 0.8f && p.Size <= 7.2f);
    }

    [Fact]
    public void Particle_equality_compares_every_field()
    {
        //Arrange
        var a = new DrawingParticle(0, 1, 0.5, 0.25, 2, 1f, 2f, 3f, 0.5f, Color.Red);
        var b = new DrawingParticle(0, 1, 0.5, 0.25, 2, 1f, 2f, 3f, 0.5f, Color.Red);
        var c = new DrawingParticle(0, 1, 0.5, 0.25, 2, 1f, 2f, 3f, 0.5f, Color.Blue);

        //Act + Assert
        (a == b).Should().BeTrue();
        (a != c).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Emitter_settings_validate_their_ranges()
    {
        //Arrange
        var emitter = new DrawingParticleEmitter();
        Action rate = () => emitter.Rate = -1f;
        Action burst = () => emitter.Burst = -1;
        Action lifetime = () => emitter.Lifetime = 0f;
        Action start = () => emitter.StartTime = double.NaN;
        Action stop = () => emitter.EndTime = double.NegativeInfinity;

        //Act + Assert
        rate.Should().Throw<ArgumentOutOfRangeException>();
        burst.Should().Throw<ArgumentOutOfRangeException>();
        lifetime.Should().Throw<ArgumentOutOfRangeException>();
        start.Should().Throw<ArgumentOutOfRangeException>();
        stop.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetParticles_rejects_nan_time_and_reuses_the_list()
    {
        //Arrange
        DrawingParticleSystem system = Fountain(2);
        var list = new List<DrawingParticle> { default };
        Action nan = () => system.GetParticles(double.NaN);

        //Act
        system.GetParticles(0.0, list);

        //Assert
        nan.Should().Throw<ArgumentOutOfRangeException>();
        list.Count.Should().Be(6);
    }
}
