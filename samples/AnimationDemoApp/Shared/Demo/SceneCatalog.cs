using System;
using System.Collections.Generic;
using System.Linq;
using AnimationDemoApp.Demo.Scenes;
using CodeBrix.Imaging.Drawing.Animation;

namespace AnimationDemoApp.Demo;

/// <summary>
/// The demo's scenes, played one after another on one timeline (a library <see cref="AnimationTimeline" /> with
/// one cue per scene, named after it). ADD NEW SCENES HERE: one line in <see cref="Registrations" /> (a short name
/// and a factory); the order of the list is the order they play in.
/// </summary>
internal sealed class SceneCatalog : IDisposable
{
    /// <summary>Every scene: its short name and how to create it.</summary>
    public static readonly IReadOnlyList<(string Name, Func<DemoScene> Create)> Registrations = new (string, Func<DemoScene>)[]
    {
        ("easing", () => new EasingScene()),
        ("ramps", () => new ColorRampScene()),
        ("gradients", () => new GradientScene()),
        ("text", () => new TextScene()),
        ("safe-areas", () => new SafeAreaScene()),
        ("fade", () => new FadeScene()),
        ("procedural", () => new ProceduralScene()),
        ("light", () => new LightScene()),
        ("masks", () => new MaskScene()),
        ("noise", () => new NoiseScene()),
        ("transitions", () => new TransitionScene()),
        ("particles", () => new ParticleScene()),
        ("motion-blur", () => new MotionBlurScene()),
        ("timeline", () => new TimelineScene()),
        ("keyframes", () => new KeyframeScene()),
        ("transforms", () => new TransformScene()),
        ("time-keys", () => new TimeKeyScene()),
        ("layout", () => new LayoutScene()),
        ("camera", () => new CameraScene()),
        ("sequence", () => new SequenceScene()),
        ("playback", () => new PlaybackScene()),
        ("draw-on", () => new DrawOnScene()),
        ("motion-path", () => new MotionPathScene()),
        ("morph", () => new MorphScene()),
        ("sprites", () => new SpriteScene()),
        ("images", () => new ImageScene()),
        ("clip-mask", () => new ClipMaskScene()),
        ("effects", () => new EffectsScene()),
        ("effects-follow", () => new EffectsFollowScene()),
        ("path-combine", () => new PathCombineScene()),
        ("kerning", () => new KerningScene()),
        ("paragraph", () => new ParagraphScene()),
        ("typewriter", () => new TypewriterScene()),
        ("outlined-text", () => new OutlinedTextScene()),
        ("captions", () => new CaptionScene()),
        ("svg-parts", () => new SvgPartsScene()),
        ("hard-edges", () => new HardEdgeScene()),
        ("ownership", () => new OwnershipScene()),
        ("colour-pickup", () => new ColorPickupScene()),
        ("brush-texture", () => new BrushTextureScene()),
        ("keyframe-playback", () => new KeyframePlaybackScene()),
        ("formation-spiral", () => new FormationSpiralScene()),
        ("formation-heap", () => new FormationHeapScene()),
        ("rolling-text", () => new RollingTextScene()),
        ("additive", () => new AdditiveScene()),
        ("path-queries", () => new PathQueriesScene()),
        ("layer-visibility", () => new LayerVisibilityScene()),
        ("frame-cache", () => new FrameCacheScene()),
    };

    private readonly List<DemoScene> scenes = new List<DemoScene>();
    private readonly List<double> starts = new List<double>();

    /// <summary>Creates the scenes.</summary>
    /// <param name="only">The short names of the scenes to play (a comma-separated list); or null for all of them.</param>
    /// <exception cref="ArgumentException">A name is not a scene.</exception>
    public SceneCatalog(string only = null)
    {
        string[] names = String.IsNullOrWhiteSpace(only)
            ? Registrations.Select(r => r.Name).ToArray()
            : only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        double start = 0;
        foreach (string name in names)
        {
            (string Name, Func<DemoScene> Create) registration = Registrations.FirstOrDefault(r => String.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (registration.Create == null)
            {
                throw new ArgumentException("There is no scene '" + name + "'. The scenes are: " + String.Join(", ", Registrations.Select(r => r.Name)) + ".");
            }

            DemoScene scene = registration.Create();
            scenes.Add(scene);
            starts.Add(start);
            start += scene.DurationSeconds;
        }

        TotalSeconds = start;
        Timeline = new AnimationTimeline(TotalSeconds, 30);
        for (int i = 0; i < scenes.Count; i++)
        {
            Timeline.AddCue(scenes[i].Name, starts[i], starts[i] + scenes[i].DurationSeconds);
        }
    }

    /// <summary>The whole timeline: its length, 30 frames per second, and one cue per scene (named after it).</summary>
    public AnimationTimeline Timeline { get; }

    /// <summary>The scenes, in playing order.</summary>
    public IReadOnlyList<DemoScene> Scenes => scenes;

    /// <summary>The whole timeline's length, seconds.</summary>
    public double TotalSeconds { get; }

    /// <summary>Where a scene starts on the timeline.</summary>
    /// <param name="index">The scene's position in <see cref="Scenes" />.</param>
    /// <returns>Its start time, seconds.</returns>
    public double StartOf(int index) => starts[Math.Clamp(index, 0, starts.Count - 1)];

    /// <summary>Which scene plays at a time, and how far into it.</summary>
    /// <param name="seconds">The timeline time (clamped to the timeline).</param>
    /// <returns>The scene's index and the time within it.</returns>
    public (int Index, double Local) At(double seconds)
    {
        seconds = Math.Clamp(seconds, 0, TotalSeconds);
        int index = scenes.Count - 1;
        while (index > 0 && starts[index] > seconds) index--;
        return (index, Math.Min(seconds - starts[index], scenes[index].DurationSeconds));
    }

    /// <summary>Releases every scene.</summary>
    public void Dispose()
    {
        foreach (DemoScene scene in scenes) scene.Dispose();
        scenes.Clear();
    }
}
