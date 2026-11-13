using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Effects;
using CodeBrix.Imaging.Drawing.Formations;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// The animated properties of a scene: keyframe tracks bound to properties of elements,
/// groups, layers and the camera, plus time callbacks of the caller's own. A session owns one
/// (<see cref="CodeBrix.Imaging.Drawing.DrawingSession.Animations"/>) and applies it whenever
/// its <see cref="CodeBrix.Imaging.Drawing.DrawingSession.Time"/> is set - "show the scene at
/// t" then sets every bound value. Bindings apply in the order they were added, so a later
/// binding of the same property wins - unless a binding is made with a <see cref="BindingMode"/>
/// of <see cref="BindingMode.Add"/> or <see cref="BindingMode.Multiply"/>, which combines with
/// the property instead (see <see cref="Apply(double)"/>). Adding and removing bindings is
/// thread-safe; applying them sets properties on the bound objects, so apply from the thread
/// that renders.
/// </summary>
public sealed class DrawingAnimations
{
    private readonly object _locker = new object();
    private readonly List<AnimationBinding> _bindings = new List<AnimationBinding>();
    private readonly Dictionary<object, RestEntry> _rests = new Dictionary<object, RestEntry>();
    private int _combinedCount;

    /// <summary>Creates an empty list (a session creates its own; use this one for a renderer of your own).</summary>
    public DrawingAnimations()
    {
    }

    /// <summary>The number of bindings.</summary>
    public int Count
    {
        get
        {
            lock (_locker) { return _bindings.Count; }
        }
    }

    /// <summary>Returns a snapshot copy of the bindings, in the order they apply.</summary>
    /// <returns>A new array holding the bindings.</returns>
    public AnimationBinding[] GetBindings()
    {
        lock (_locker) { return _bindings.ToArray(); }
    }

    /// <summary>
    /// Binds a callback that receives every applied time - for anything the typed bindings do
    /// not cover (the session's whole-frame opacity, a value computed from a cue, a shape's
    /// own property).
    /// </summary>
    /// <param name="applyAtTime">Called with the time on every <see cref="Apply(double)"/>.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="applyAtTime"/> is null.</exception>
    public AnimationBinding Bind(Action<double> applyAtTime)
    {
        if (applyAtTime == null) { throw new ArgumentNullException(nameof(applyAtTime)); }
        return Add(new AnimationBinding(null, null, applyAtTime));
    }

    /// <summary>
    /// Binds a track of any type to a setter of the caller's own: on every apply the setter
    /// receives the track's value at that time.
    /// </summary>
    /// <typeparam name="T">The track's value type.</typeparam>
    /// <param name="track">The track.</param>
    /// <param name="apply">Receives the value.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public AnimationBinding Bind<T>(KeyframeTrack<T> track, Action<T> apply)
    {
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        if (apply == null) { throw new ArgumentNullException(nameof(apply)); }
        return Add(new AnimationBinding(track, null, seconds => apply(track.Evaluate(seconds))));
    }

    /// <summary>
    /// Binds a float track to an element's <see cref="AnimatedProperty.Rotation"/>,
    /// <see cref="AnimatedProperty.Scale"/>, <see cref="AnimatedProperty.ScaleX"/>,
    /// <see cref="AnimatedProperty.ScaleY"/> or <see cref="AnimatedProperty.Opacity"/>; a path
    /// shape's <see cref="AnimatedProperty.TrimStart"/>, <see cref="AnimatedProperty.TrimEnd"/>,
    /// <see cref="AnimatedProperty.TrimOffset"/> or <see cref="AnimatedProperty.MorphFraction"/>
    /// (fractions clamped to 0..1); a sprite's <see cref="AnimatedProperty.FrameIndex"/> (rounded
    /// down); a text block's <see cref="AnimatedProperty.RevealFraction"/> (clamped to 0..1) or
    /// <see cref="AnimatedProperty.RevealCount"/> (negative counts as 0); or the element's effects'
    /// <see cref="AnimatedProperty.BlurRadius"/>, <see cref="AnimatedProperty.ShadowBlurRadius"/> or
    /// <see cref="AnimatedProperty.GlowRadius"/> (negative values count as 0; an element without
    /// effects gets them).
    /// </summary>
    /// <param name="element">The element (a shape, stroke or group).</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a float track.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<float> track)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property switch
        {
            AnimatedProperty.Opacity => t => element.Opacity = ClampOpacity(track.Evaluate(t)),
            AnimatedProperty.TrimStart => PathSetter(element, property, track, (shape, v) => shape.TrimStart = ClampOpacity(v)),
            AnimatedProperty.TrimEnd => PathSetter(element, property, track, (shape, v) => shape.TrimEnd = ClampOpacity(v)),
            AnimatedProperty.TrimOffset => PathSetter(element, property, track, (shape, v) => shape.TrimOffset = Single.IsFinite(v) ? v : 0f),
            AnimatedProperty.MorphFraction => element is PathShape { Morph: not null }
                ? PathSetter(element, property, track, (shape, v) => shape.MorphFraction = ClampOpacity(v))
                : throw WrongTarget(property, "a path shape made from a PathMorph"),
            AnimatedProperty.FrameIndex => element is SpriteShape sprite
                ? t => sprite.FrameIndex = FrameOf(track.Evaluate(t))
                : throw WrongTarget(property, "a sprite shape"),
            AnimatedProperty.BlurRadius or AnimatedProperty.ShadowBlurRadius or AnimatedProperty.GlowRadius
                => EffectFloatSetter(property, track, () => element.Effects, value => element.Effects = value),
            AnimatedProperty.RevealFraction => element is TextBlockShape fractionBlock
                ? t => fractionBlock.RevealFraction = ClampOpacity(track.Evaluate(t))
                : throw WrongTarget(property, "a text block shape"),
            AnimatedProperty.RevealCount => element is TextBlockShape countBlock
                ? t => countBlock.RevealCount = NonNegative(track.Evaluate(t))
                : throw WrongTarget(property, "a text block shape"),
            _ => FloatTransformSetter(property, track, () => element.Transform, value => element.Transform = value),
        };
        return Add(new AnimationBinding(element, property, apply));
    }

    /// <summary>
    /// Binds a point track to an element's <see cref="AnimatedProperty.Position"/>,
    /// <see cref="AnimatedProperty.Anchor"/> or <see cref="AnimatedProperty.Scale"/> (X and Y), or
    /// its effects' <see cref="AnimatedProperty.ShadowOffset"/>.
    /// </summary>
    /// <param name="element">The element (a shape, stroke or group).</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a point track.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<PointF> track)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property == AnimatedProperty.ShadowOffset
            ? t => element.Effects = (element.Effects ?? DrawingEffects.None).WithShadowOffset(FinitePoint(track.Evaluate(t)))
            : PointTransformSetter(property, track, () => element.Transform, value => element.Transform = value);
        return Add(new AnimationBinding(element, property, apply));
    }

    /// <summary>
    /// Binds a color track to an image's or sprite's <see cref="AnimatedProperty.Tint"/>, a group's
    /// <see cref="AnimatedProperty.Color"/> (its <see cref="DrawingGroup.ContentColor"/> - the same as
    /// <see cref="Bind(DrawingGroup, AnimatedProperty, KeyframeTrack{Color})"/>, for a group held as an
    /// element), or an element's effects' <see cref="AnimatedProperty.ShadowColor"/> or
    /// <see cref="AnimatedProperty.GlowColor"/>.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a color track on this element.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<Color> track)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property switch
        {
            AnimatedProperty.Tint => element switch
            {
                ImageShape image => t => image.Tint = track.Evaluate(t),
                SpriteShape sprite => t => sprite.Tint = track.Evaluate(t),
                _ => throw WrongTarget(property, "an image or sprite shape"),
            },
            AnimatedProperty.Color => element is DrawingGroup group
                ? t => group.ContentColor = track.Evaluate(t)
                : throw WrongTarget(property, "a group"),
            AnimatedProperty.ShadowColor or AnimatedProperty.GlowColor
                => EffectColorSetter(property, track, () => element.Effects, value => element.Effects = value),
            _ => throw WrongTrack(property, "color (on an element)"),
        };
        return Add(new AnimationBinding(element, property, apply));
    }

    /// <summary>
    /// Moves an element along a path: on every apply its <see cref="DrawingElement.Transform"/>
    /// is placed so that its anchor point (the transform's <see cref="DrawingTransform.Anchor"/>;
    /// draw the element around (0, 0) to use its origin) lies on <paramref name="path"/> at the
    /// fraction of the path's length the track gives (clamped to 0..1), and - when
    /// <paramref name="orientToPath"/> is set - turned to face along the path (its rotation the
    /// path's direction there, in degrees, plus <paramref name="rotationOffset"/>). The element's
    /// scale is kept. The point and direction come from <see cref="PathGeometry.GetPointAt(float)"/>,
    /// shared managed arithmetic identical in both packages.
    /// </summary>
    /// <param name="element">The element to move (a shape, stroke or group).</param>
    /// <param name="path">The path to follow, in the same coordinates as the element's.</param>
    /// <param name="progress">How far along the path, 0..1, over time.</param>
    /// <param name="orientToPath"><c>true</c> to turn the element to face along the path.</param>
    /// <param name="rotationOffset">Degrees added to the facing rotation (for content drawn facing another way than right).</param>
    /// <returns>The binding (its property is <see cref="AnimatedProperty.Position"/>).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rotationOffset"/> is not finite.</exception>
    public AnimationBinding BindAlongPath(DrawingElement element, PathGeometry path, KeyframeTrack<float> progress,
        bool orientToPath = false, float rotationOffset = 0f)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (progress == null) { throw new ArgumentNullException(nameof(progress)); }
        if (!Single.IsFinite(rotationOffset)) { throw new ArgumentOutOfRangeException(nameof(rotationOffset)); }
        return Add(new AnimationBinding(element, AnimatedProperty.Position, t =>
        {
            PathPoint point = path.GetPointAt(progress.Evaluate(t));
            DrawingTransform current = element.Transform;
            var translation = new PointF(point.Position.X - current.Anchor.X, point.Position.Y - current.Anchor.Y);
            element.Transform = orientToPath
                ? current.WithTranslation(translation).WithRotation(point.Angle + rotationOffset)
                : current.WithTranslation(translation);
        }));
    }

    /// <summary>
    /// Moves an element along a path with the travel settings of <paramref name="options"/>: as
    /// <see cref="BindAlongPath(DrawingElement, PathGeometry, KeyframeTrack{float}, bool, float)"/>
    /// (the element's transform anchor rides the path at the track's fraction of its length), plus
    /// ROLLING - the element turns like a wheel of <see cref="PathTravelOptions.RollRadius"/>, by the
    /// distance travelled, upright at <see cref="PathTravelOptions.UprightAtProgress"/> - which ADDS
    /// to the facing rotation when <see cref="PathTravelOptions.OrientToPath"/> is also set, and
    /// progress outside 0..1 continuing straight past the ends when
    /// <see cref="PathTravelOptions.ExtendBeyondEnds"/> is set (an overshoot-and-settle). The roll is
    /// <c>(progress - UprightAtProgress) x length / radius</c> radians, in degrees; for a
    /// <see cref="FloatTrack"/> it is evaluated key by key on the converted key values (exactly a
    /// rotation track mirrored from the progress track, each segment eased alike). Scale, opacity and
    /// everything else are left alone - bind them as usual, or combine more motion on top with a
    /// <see cref="BindingMode"/>.
    /// </summary>
    /// <param name="element">The element to move (a shape, stroke or group).</param>
    /// <param name="path">The path to follow, in the same coordinates as the element's.</param>
    /// <param name="progress">How far along the path, as a fraction of its length, over time.</param>
    /// <param name="options">The travel settings; or <c>null</c> for the defaults (the plain binding).</param>
    /// <returns>The binding (its property is <see cref="AnimatedProperty.Position"/>).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/>, <paramref name="path"/> or <paramref name="progress"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is not finite.</exception>
    public AnimationBinding BindAlongPath(DrawingElement element, PathGeometry path, KeyframeTrack<float> progress, PathTravelOptions options)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (progress == null) { throw new ArgumentNullException(nameof(progress)); }
        PathTravelOptions settings = options ?? new PathTravelOptions();
        settings.Validate();
        bool orient = settings.OrientToPath;
        bool extend = settings.ExtendBeyondEnds;
        float offset = settings.RotationOffset;
        float radius = settings.RollRadius;
        float upright = settings.UprightAtProgress;
        if (radius == 0f && !extend)
        {
            return BindAlongPath(element, path, progress, orient, offset);
        }

        Func<double, float> rollAt = null;
        if (radius != 0f)
        {
            float length = path.GetLength();
            double degreesPerFraction = length / radius * 180.0 / Math.PI;
            Func<float, float> roll = value => (float)((value - upright) * degreesPerFraction);
            FloatTrack floats = progress as FloatTrack;
            rollAt = t =>
            {
                float p = progress.Evaluate(t);
                if (Single.IsNaN(p)) { return roll(0f); }
                if (!extend && (p < 0f || p > 1f)) { return roll(Math.Clamp(p, 0f, 1f)); }
                return floats != null ? floats.EvaluateMapped(t, roll) : roll(p);
            };
        }

        return Add(new AnimationBinding(element, AnimatedProperty.Position, t =>
        {
            PathPoint point = path.GetPointAt(progress.Evaluate(t), extend);
            DrawingTransform current = element.Transform;
            var translation = new PointF(point.Position.X - current.Anchor.X, point.Position.Y - current.Anchor.Y);
            DrawingTransform next = current.WithTranslation(translation);
            if (orient)
            {
                float facing = point.Angle + offset;
                next = next.WithRotation(rollAt != null ? facing + rollAt(t) : facing);
            }
            else if (rollAt != null)
            {
                float rolled = rollAt(t);
                next = next.WithRotation(offset != 0f ? rolled + offset : rolled);
            }
            element.Transform = next;
        }));
    }

    /// <summary>
    /// Rolls an element along a path like a wheel: its transform anchor (set it to the wheel's hub
    /// first, for example with <c>element.Transform = element.Transform.WithAnchor(hub)</c>) rides
    /// the path at the track's fraction of the length, and it turns by the distance travelled over
    /// <paramref name="radius"/> - upright at <paramref name="uprightAtProgress"/>, and facing along
    /// the path as well when <paramref name="orientToPath"/> is set (the two add). The short form of
    /// <see cref="BindAlongPath(DrawingElement, PathGeometry, KeyframeTrack{float}, PathTravelOptions)"/>;
    /// use that for an overshoot past the path's ends.
    /// </summary>
    /// <param name="element">The element to roll (a shape, stroke or group).</param>
    /// <param name="path">The path the hub follows, in the same coordinates as the element's.</param>
    /// <param name="progress">How far along the path, as a fraction of its length, over time.</param>
    /// <param name="radius">The rolling radius (hub to ground), calibrated units; negative rolls the other way.</param>
    /// <param name="uprightAtProgress">The progress at which the element is upright; 1 (the path's end) by default.</param>
    /// <param name="orientToPath"><c>true</c> to turn with the path's direction as well.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/>, <paramref name="path"/> or <paramref name="progress"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is 0 or not finite, or <paramref name="uprightAtProgress"/> is not finite.</exception>
    public AnimationBinding BindRollAlongPath(DrawingElement element, PathGeometry path, KeyframeTrack<float> progress, float radius,
        float uprightAtProgress = 1f, bool orientToPath = false)
    {
        if (!Single.IsFinite(radius) || radius == 0f) { throw new ArgumentOutOfRangeException(nameof(radius)); }
        if (!Single.IsFinite(uprightAtProgress)) { throw new ArgumentOutOfRangeException(nameof(uprightAtProgress)); }
        return BindAlongPath(element, path, progress, new PathTravelOptions
        {
            RollRadius = radius,
            UprightAtProgress = uprightAtProgress,
            OrientToPath = orientToPath,
        });
    }

    /// <summary>
    /// Sends several elements along ONE path as a train - one behind the other, with gaps that may
    /// open and close over time, each following the progress track a little later than the one
    /// ahead, and each optionally facing along the path, rolling, scaling and fading as it goes
    /// (see <see cref="PathTrainOptions"/> for the rule). One binding places them all; pure
    /// functions of the time.
    /// </summary>
    /// <param name="elements">The elements, in their natural order (rank follows <see cref="PathTrainOptions.Order"/>).</param>
    /// <param name="path">The path, in the elements' coordinates.</param>
    /// <param name="progress">The lead's progress along the path, as a fraction of its length, over time.</param>
    /// <param name="options">The train's settings; or <c>null</c> for the defaults.</param>
    /// <returns>The train (its <see cref="PathTrain.Binding"/> is in this list).</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="options"/> is null, or an element is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is out of range.</exception>
    public PathTrain BindTrainAlongPath(IReadOnlyList<DrawingElement> elements, PathGeometry path, KeyframeTrack<float> progress,
        PathTrainOptions options = null)
    {
        if (elements == null) { throw new ArgumentNullException(nameof(elements)); }
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i] == null) { throw new ArgumentNullException(nameof(elements), "An element is null."); }
        }
        return BindTrain(elements, null, path, progress, options);
    }

    /// <summary>
    /// "These letters roll along this path and settle here": splits a text block into pieces
    /// (<see cref="TextBlockShape.CreatePieces"/>; pieces that are only white space are left out)
    /// and sends them along the path as a train in which EVERY PIECE ENDS EXACTLY WHERE THE BLOCK
    /// DRAWS IT - each travels a copy of the path moved so that the path's end lies on the piece's
    /// own centre (<see cref="PathTrainOptions.SettleInPlace"/> is always on). So draw the block
    /// where the text should end up, and give a path that ENDS where the route should end (its end
    /// point is the arrival point of every piece's centre, relative to the others). Spacing,
    /// stagger, rolling (<see cref="PathTrainOptions.RollOnBaseline"/> rolls each piece on its own
    /// line), facing, scaling and fading work as for any train; for text arriving from the left,
    /// <see cref="StaggerOrder.Reverse"/> lets the last letter lead. With a spacing, let
    /// <see cref="PathTrainOptions.SpacingScale"/> end at 0 so the gaps close at rest; with
    /// <see cref="PathTrainOptions.OrientToPath"/>, let the route end heading along the baseline
    /// (to the right) so the pieces arrive upright. Add the pieces
    /// to a layer with <see cref="PathTrain.AddTo"/> (the block itself is not needed on a layer).
    /// </summary>
    /// <param name="text">The laid-out text block, placed where the text should end up.</param>
    /// <param name="unit">The kind of piece: characters, words or lines.</param>
    /// <param name="path">The route (its end is where every piece arrives, relative to its own place).</param>
    /// <param name="progress">The lead's progress along the path, as a fraction of its length, over time.</param>
    /// <param name="options">The train's settings; or <c>null</c> for the defaults.</param>
    /// <returns>The train, whose elements are the pieces.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unit"/> is not defined or an option is out of range.</exception>
    public PathTrain BindTextAlongPath(TextBlockShape text, TextUnit unit, PathGeometry path, KeyframeTrack<float> progress,
        PathTrainOptions options = null)
    {
        if (text == null) { throw new ArgumentNullException(nameof(text)); }
        var pieces = new List<DrawingElement>();
        var baselines = new List<float>();
        IReadOnlyList<TextCluster> clusters = text.Layout.Clusters;
        foreach (TextBlockShape piece in text.CreatePieces(unit))
        {
            bool ink = false;
            for (int i = piece.FirstCluster; i < piece.FirstCluster + piece.ClusterCount; i++)
            {
                if (!clusters[i].IsWhiteSpace) { ink = true; break; }
            }
            if (!ink) { continue; }
            pieces.Add(piece);
            baselines.Add(text.Y + text.Layout.Lines[clusters[piece.FirstCluster].LineIndex].Baseline);
        }
        return BindTrain(pieces, baselines, path, progress, options);
    }

    private PathTrain BindTrain(IReadOnlyList<DrawingElement> elements, List<float> baselines, PathGeometry path,
        KeyframeTrack<float> progress, PathTrainOptions options)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (progress == null) { throw new ArgumentNullException(nameof(progress)); }
        PathTrainOptions settings = (options ?? new PathTrainOptions()).Copy();
        if (baselines != null) { settings.SettleInPlace = true; }
        settings.Validate();

        PointF end = path.PointAtDistance(path.LengthValue).Position;
        int count = elements.Count;
        var cars = new PathTrainCar[count];
        for (int i = 0; i < count; i++)
        {
            DrawingElement element = elements[i];
            if (settings.AnchorAtCenter)
            {
                RectangleF? bounds = element.GetLocalBounds();
                if (bounds.HasValue)
                {
                    RectangleF b = bounds.Value;
                    element.Transform = element.Transform.WithAnchor(new PointF(b.X + (b.Width / 2f), b.Y + (b.Height / 2f)));
                }
            }
            int rank = settings.Order switch
            {
                StaggerOrder.Reverse => count - 1 - i,
                StaggerOrder.CenterOut => (int)Math.Floor(Math.Abs(i - ((count - 1) / 2.0))),
                _ => i,
            };
            float radius = settings.RollRadius;
            PointF anchor = element.Transform.Anchor;
            if (baselines != null && settings.RollOnBaseline) { radius = Math.Max(1f, baselines[i] - anchor.Y); }
            cars[i] = new PathTrainCar(element, i, rank, rank * settings.Spacing, rank * settings.Stagger, radius,
                settings.SettleInPlace ? end : null);
        }

        var train = new PathTrain(path, progress, settings, cars);
        train.Binding = Add(new AnimationBinding(train, AnimatedProperty.Position, train.Apply));
        return train;
    }

    /// <summary>
    /// Turns a formation into time-keyed session elements: for each member (in ARRIVAL order - a
    /// stable sort by <see cref="FormationMember.ArrivalTime"/>), <paramref name="createResting"/>
    /// makes its resting element, which goes on <paramref name="restingLayer"/> existing from the
    /// arrival on (its <see cref="DrawingElement.StartTime"/> is set); and for a member that
    /// travels, <paramref name="createTravelling"/> makes the elements seen on the way - draw them
    /// CENTRED ON (0, 0) - which go on <paramref name="travellingLayer"/> existing only while it
    /// travels (start and end times set) and are posed every apply by one binding
    /// (<see cref="FormationPose.ToTransform"/> of the path's pose). Because the resting elements
    /// appear in the order they were added, the live render only draws new arrivals while playing
    /// forward; seeking anywhere needs no rebuilding; every frame is a function of its time alone.
    /// Keep the travelling elements on their own layer (it redraws while anything moves).
    /// </summary>
    /// <param name="members">The members.</param>
    /// <param name="restingLayer">The layer the resting elements go on.</param>
    /// <param name="travellingLayer">The layer the travelling elements go on (may be the same layer).</param>
    /// <param name="createResting">Makes member k's resting element, already placed where it rests (k = its index in <paramref name="members"/>).</param>
    /// <param name="createTravelling">Makes member k's travelling elements, drawn centred on (0, 0); or <c>null</c> for formations whose members just appear.</param>
    /// <returns>The formation's elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument other than <paramref name="createTravelling"/> is null, a member is null, or a factory returns null.</exception>
    public FormationElements BindFormation(IReadOnlyList<FormationMember> members, DrawingLayer restingLayer, DrawingLayer travellingLayer,
        Func<int, DrawingElement> createResting, Func<int, IReadOnlyList<DrawingElement>> createTravelling = null)
    {
        if (members == null) { throw new ArgumentNullException(nameof(members)); }
        if (restingLayer == null) { throw new ArgumentNullException(nameof(restingLayer)); }
        if (travellingLayer == null) { throw new ArgumentNullException(nameof(travellingLayer)); }
        if (createResting == null) { throw new ArgumentNullException(nameof(createResting)); }
        int count = members.Count;
        var list = new FormationMember[count];
        var order = new int[count];
        for (int i = 0; i < count; i++)
        {
            list[i] = members[i] ?? throw new ArgumentNullException(nameof(members), "A member is null.");
            order[i] = i;
        }
        //A stable sort by arrival time: members arriving together keep their given order
        Array.Sort(order, (a, b) =>
        {
            int byTime = list[a].ArrivalTime.CompareTo(list[b].ArrivalTime);
            return byTime != 0 ? byTime : a.CompareTo(b);
        });

        var resting = new DrawingElement[count];
        var travelling = new DrawingElement[count][];
        foreach (int i in order)
        {
            FormationMember member = list[i];
            DrawingElement rest = createResting(i) ?? throw new ArgumentNullException(nameof(createResting), "The resting factory returned null.");
            rest.StartTime = member.ArrivalTime;
            resting[i] = rest;
            AddElement(restingLayer, rest);

            IReadOnlyList<DrawingElement> parts = member.Path != null && createTravelling != null ? createTravelling(i) : null;
            var copy = new DrawingElement[parts?.Count ?? 0];
            for (int p = 0; p < copy.Length; p++)
            {
                DrawingElement part = parts[p] ?? throw new ArgumentNullException(nameof(createTravelling), "A travelling element is null.");
                part.StartTime = member.Path.StartTime;
                part.EndTime = member.ArrivalTime;
                copy[p] = part;
                AddElement(travellingLayer, part);
            }
            travelling[i] = copy;
        }

        var formation = new FormationElements(list, resting, travelling, order);
        formation.Binding = Add(new AnimationBinding(formation, null, formation.Apply));
        return formation;
    }

    private static void AddElement(DrawingLayer layer, DrawingElement element)
    {
        if (element is Stroke stroke) { layer.AddStroke(stroke); }
        else { layer.AddShape((DrawingShape)element); }
    }

    /// <summary>
    /// Binds a staggered entrance to a list of elements - typically the characters, words or lines
    /// of a text block (<see cref="TextBlockShape.CreatePieces"/>), but any elements: element k (in
    /// <see cref="StaggerOptions.Order"/>) starts at StartTime + k x Interval and takes Duration to go
    /// from the options' "from" state (opacity, offset, scale, rotation) to opacity
    /// <see cref="StaggerOptions.ToOpacity"/> with no offset, scale 1 and rotation 0. Before its start
    /// an element holds its "from" state; after its end, its final state. Only the properties the
    /// options change are bound (opacity always is), as ordinary keyframe tracks - each element's
    /// <see cref="AnimatedProperty.Opacity"/>, and when needed its <see cref="AnimatedProperty.Position"/>
    /// (the stagger then owns its translation), <see cref="AnimatedProperty.Scale"/> and
    /// <see cref="AnimatedProperty.Rotation"/>. With <see cref="StaggerOptions.AnchorAtCenter"/> each
    /// element's transform anchor is moved to the centre of its bounds first. Pure functions of time:
    /// any frame renders the same in any order.
    /// </summary>
    /// <param name="elements">The elements, in their natural order.</param>
    /// <param name="options">The stagger's settings; or <c>null</c> for the defaults.</param>
    /// <returns>The bindings made, element by element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null or holds a null element.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option is out of range.</exception>
    public IReadOnlyList<AnimationBinding> BindStagger(IReadOnlyList<DrawingElement> elements, StaggerOptions options = null)
    {
        if (elements == null) { throw new ArgumentNullException(nameof(elements)); }
        StaggerOptions settings = options ?? new StaggerOptions();
        settings.Validate();
        for (int i = 0; i < elements.Count; i++)
        {
            if (elements[i] == null) { throw new ArgumentNullException(nameof(elements), "An element is null."); }
        }

        var bindings = new List<AnimationBinding>();
        int count = elements.Count;
        for (int i = 0; i < count; i++)
        {
            DrawingElement element = elements[i];
            int rank = settings.Order switch
            {
                StaggerOrder.Reverse => count - 1 - i,
                StaggerOrder.CenterOut => (int)Math.Floor(Math.Abs(i - ((count - 1) / 2.0))),
                _ => i,
            };
            double start = settings.StartTime + (rank * settings.Interval);
            double end = start + settings.Duration;
            EasingKind easing = settings.Easing;

            if (settings.AnchorAtCenter)
            {
                RectangleF? bounds = element.GetLocalBounds();
                if (bounds.HasValue)
                {
                    RectangleF b = bounds.Value;
                    element.Transform = element.Transform.WithAnchor(new PointF(b.X + (b.Width / 2f), b.Y + (b.Height / 2f)));
                }
            }

            var opacity = new FloatTrack();
            opacity.Add(start, settings.FromOpacity).Add(end, settings.ToOpacity, easing);
            bindings.Add(Bind(element, AnimatedProperty.Opacity, opacity));
            if (settings.FromOffset.X != 0f || settings.FromOffset.Y != 0f)
            {
                var position = new PointTrack();
                position.Add(start, settings.FromOffset).Add(end, new PointF(0f, 0f), easing);
                bindings.Add(Bind(element, AnimatedProperty.Position, position));
            }
            if (settings.FromScale != 1f)
            {
                var scale = new FloatTrack();
                scale.Add(start, settings.FromScale).Add(end, 1f, easing);
                bindings.Add(Bind(element, AnimatedProperty.Scale, scale));
            }
            if (settings.FromRotation != 0f)
            {
                var rotation = new FloatTrack();
                rotation.Add(start, settings.FromRotation).Add(end, 0f, easing);
                bindings.Add(Bind(element, AnimatedProperty.Rotation, rotation));
            }
        }
        return bindings;
    }

    /// <summary>
    /// Binds a color track to a group's <see cref="AnimatedProperty.Color"/>
    /// (<see cref="DrawingGroup.ContentColor"/>: the color its children without one of their
    /// own are drawn in), or to its effects' <see cref="AnimatedProperty.ShadowColor"/> or
    /// <see cref="AnimatedProperty.GlowColor"/>. For a single shape's color, draw it with an
    /// <see cref="AnimatedShape"/> whose factory reads the track.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="property">The property: <see cref="AnimatedProperty.Color"/>.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property is not <see cref="AnimatedProperty.Color"/>.</exception>
    public AnimationBinding Bind(DrawingGroup group, AnimatedProperty property, KeyframeTrack<Color> track)
    {
        if (group == null) { throw new ArgumentNullException(nameof(group)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        if (property == AnimatedProperty.ShadowColor || property == AnimatedProperty.GlowColor)
        {
            return Add(new AnimationBinding(group, property,
                EffectColorSetter(property, track, () => group.Effects, value => group.Effects = value)));
        }
        if (property != AnimatedProperty.Color) { throw WrongTrack(property, "color"); }
        return Add(new AnimationBinding(group, property, t => group.ContentColor = track.Evaluate(t)));
    }

    /// <summary>
    /// Binds a float track to a layer's <see cref="AnimatedProperty.Rotation"/>,
    /// <see cref="AnimatedProperty.Scale"/>, <see cref="AnimatedProperty.ScaleX"/>,
    /// <see cref="AnimatedProperty.ScaleY"/> (its <see cref="DrawingLayer.Transform"/>),
    /// <see cref="AnimatedProperty.Opacity"/> (its <see cref="DrawingLayer.Opacity"/>), or its
    /// effects' <see cref="AnimatedProperty.BlurRadius"/>, <see cref="AnimatedProperty.ShadowBlurRadius"/>
    /// or <see cref="AnimatedProperty.GlowRadius"/>.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a float track.</exception>
    public AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<float> track)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property switch
        {
            AnimatedProperty.Opacity => t => layer.Opacity = ClampOpacity(track.Evaluate(t)),
            AnimatedProperty.BlurRadius or AnimatedProperty.ShadowBlurRadius or AnimatedProperty.GlowRadius
                => EffectFloatSetter(property, track, () => layer.Effects, value => layer.Effects = value),
            _ => FloatTransformSetter(property, track, () => layer.Transform, value => layer.Transform = value),
        };
        return Add(new AnimationBinding(layer, property, apply));
    }

    /// <summary>
    /// Binds a point track to a layer's <see cref="AnimatedProperty.Position"/>,
    /// <see cref="AnimatedProperty.Anchor"/> or <see cref="AnimatedProperty.Scale"/> (its
    /// <see cref="DrawingLayer.Transform"/>), or its effects' <see cref="AnimatedProperty.ShadowOffset"/>.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a point track.</exception>
    public AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<PointF> track)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property == AnimatedProperty.ShadowOffset
            ? t => layer.Effects = (layer.Effects ?? DrawingEffects.None).WithShadowOffset(FinitePoint(track.Evaluate(t)))
            : PointTransformSetter(property, track, () => layer.Transform, value => layer.Transform = value);
        return Add(new AnimationBinding(layer, property, apply));
    }

    /// <summary>
    /// Binds a color track to a layer's <see cref="AnimatedProperty.Color"/>
    /// (<see cref="DrawingLayer.Color"/>; each change redraws the layer), or to its effects'
    /// <see cref="AnimatedProperty.ShadowColor"/> or <see cref="AnimatedProperty.GlowColor"/>.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="property">The property: <see cref="AnimatedProperty.Color"/>.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property is not <see cref="AnimatedProperty.Color"/>.</exception>
    public AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<Color> track)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        if (property == AnimatedProperty.ShadowColor || property == AnimatedProperty.GlowColor)
        {
            return Add(new AnimationBinding(layer, property,
                EffectColorSetter(property, track, () => layer.Effects, value => layer.Effects = value)));
        }
        if (property != AnimatedProperty.Color) { throw WrongTrack(property, "color"); }
        return Add(new AnimationBinding(layer, property, t => layer.Color = track.Evaluate(t)));
    }

    /// <summary>
    /// Binds an on/off track to a layer's <see cref="AnimatedProperty.IsVisible"/>
    /// (<see cref="DrawingLayer.IsVisible"/>): the layer is shown while the track is <c>true</c>
    /// and skipped entirely - not drawn, not composited, not exported - while it is <c>false</c>.
    /// A <see cref="BoolTrack"/> steps exactly at its keys' times.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="property">The property: <see cref="AnimatedProperty.IsVisible"/>.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property is not <see cref="AnimatedProperty.IsVisible"/>.</exception>
    public AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<bool> track)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        if (property != AnimatedProperty.IsVisible) { throw WrongTrack(property, "bool"); }
        return Add(new AnimationBinding(layer, property, t => layer.IsVisible = track.Evaluate(t)));
    }

    /// <summary>
    /// Binds a float track to the camera's <see cref="AnimatedProperty.Rotation"/> (its roll) or
    /// <see cref="AnimatedProperty.Scale"/> (its zoom; the track's values must stay positive).
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a float track on a camera.</exception>
    public AnimationBinding Bind(DrawingCamera camera, AnimatedProperty property, KeyframeTrack<float> track)
    {
        if (camera == null) { throw new ArgumentNullException(nameof(camera)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property switch
        {
            AnimatedProperty.Rotation => t => camera.Rotation = track.Evaluate(t),
            AnimatedProperty.Scale => t => camera.Zoom = track.Evaluate(t),
            _ => throw WrongTrack(property, "float (on a camera)"),
        };
        return Add(new AnimationBinding(camera, property, apply));
    }

    /// <summary>
    /// Binds a point track to the camera's <see cref="AnimatedProperty.Position"/> (its pan) or
    /// <see cref="AnimatedProperty.Anchor"/> (its pivot).
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a point track on a camera.</exception>
    public AnimationBinding Bind(DrawingCamera camera, AnimatedProperty property, KeyframeTrack<PointF> track)
    {
        if (camera == null) { throw new ArgumentNullException(nameof(camera)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        Action<double> apply = property switch
        {
            AnimatedProperty.Position => t => camera.Pan = track.Evaluate(t),
            AnimatedProperty.Anchor => t => camera.Pivot = track.Evaluate(t),
            _ => throw WrongTrack(property, "point (on a camera)"),
        };
        return Add(new AnimationBinding(camera, property, apply));
    }

    /// <summary>
    /// Binds a float track to an element's <see cref="AnimatedProperty.Rotation"/>,
    /// <see cref="AnimatedProperty.Scale"/> (both ways), <see cref="AnimatedProperty.ScaleX"/>,
    /// <see cref="AnimatedProperty.ScaleY"/> or <see cref="AnimatedProperty.Opacity"/> with a
    /// <see cref="BindingMode"/>: <see cref="BindingMode.Add"/> adds the track's value to whatever
    /// the other bindings set (a sway on top of a turn), <see cref="BindingMode.Multiply"/>
    /// multiplies by it (a pulse on top of a scale, a flicker on top of a fade), and
    /// <see cref="BindingMode.Replace"/> is the plain binding. The result does not depend on the
    /// order the bindings were made in (see <see cref="Apply(double)"/>); opacity is clamped to
    /// 0..1 after combining.
    /// </summary>
    /// <param name="element">The element (a shape, stroke or group).</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <param name="mode">How the value combines with the property.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be combined (for Add or Multiply) or does not take a float track.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<float> track, BindingMode mode)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        CheckMode(mode);
        if (mode == BindingMode.Replace) { return Bind(element, property, track); }
        return BindSlots(element, property, mode, ElementSlots(element, property, false), track.Evaluate, null);
    }

    /// <summary>
    /// Binds a point track to an element's <see cref="AnimatedProperty.Position"/> (its
    /// translation) or <see cref="AnimatedProperty.Scale"/> (X and Y) with a
    /// <see cref="BindingMode"/>: <see cref="BindingMode.Add"/> adds the point (an offset on top of
    /// a path or a position track - a shake, a bob), <see cref="BindingMode.Multiply"/> multiplies
    /// component by component.
    /// </summary>
    /// <param name="element">The element (a shape, stroke or group).</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <param name="mode">How the value combines with the property.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be combined (for Add or Multiply) or does not take a point track.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, KeyframeTrack<PointF> track, BindingMode mode)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        CheckMode(mode);
        if (mode == BindingMode.Replace) { return Bind(element, property, track); }
        return BindSlots(element, property, mode, ElementSlots(element, property, true), null, track.Evaluate);
    }

    /// <summary>
    /// Binds a value computed by a function of the time - a wave, a value read from a cue, anything
    /// a keyframe track does not express - to an element's <see cref="AnimatedProperty.Rotation"/>,
    /// <see cref="AnimatedProperty.Scale"/>, <see cref="AnimatedProperty.ScaleX"/>,
    /// <see cref="AnimatedProperty.ScaleY"/> or <see cref="AnimatedProperty.Opacity"/>, replacing
    /// or combining as <paramref name="mode"/> says. Write the function as a pure function of the
    /// time, so any frame renders the same in any order.
    /// </summary>
    /// <param name="element">The element (a shape, stroke or group).</param>
    /// <param name="property">The property.</param>
    /// <param name="valueAt">Returns the value at a time.</param>
    /// <param name="mode">How the value combines with the property; <see cref="BindingMode.Replace"/> by default.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property is not one of those listed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, Func<double, float> valueAt, BindingMode mode = BindingMode.Replace)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (valueAt == null) { throw new ArgumentNullException(nameof(valueAt)); }
        CheckMode(mode);
        return BindSlots(element, property, mode, ElementSlots(element, property, false), valueAt, null);
    }

    /// <summary>
    /// Binds a point computed by a function of the time to an element's
    /// <see cref="AnimatedProperty.Position"/> or <see cref="AnimatedProperty.Scale"/> (X and Y),
    /// replacing or combining as <paramref name="mode"/> says.
    /// </summary>
    /// <param name="element">The element (a shape, stroke or group).</param>
    /// <param name="property">The property.</param>
    /// <param name="valueAt">Returns the point at a time.</param>
    /// <param name="mode">How the value combines with the property; <see cref="BindingMode.Replace"/> by default.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property is not one of those listed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingElement element, AnimatedProperty property, Func<double, PointF> valueAt, BindingMode mode = BindingMode.Replace)
    {
        if (element == null) { throw new ArgumentNullException(nameof(element)); }
        if (valueAt == null) { throw new ArgumentNullException(nameof(valueAt)); }
        CheckMode(mode);
        return BindSlots(element, property, mode, ElementSlots(element, property, true), null, valueAt);
    }

    /// <summary>
    /// Binds a float track to a layer's <see cref="AnimatedProperty.Rotation"/>,
    /// <see cref="AnimatedProperty.Scale"/>, <see cref="AnimatedProperty.ScaleX"/>,
    /// <see cref="AnimatedProperty.ScaleY"/> (its <see cref="DrawingLayer.Transform"/>) or
    /// <see cref="AnimatedProperty.Opacity"/>, combining as <paramref name="mode"/> says (see
    /// <see cref="Bind(DrawingElement, AnimatedProperty, KeyframeTrack{float}, BindingMode)"/>).
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <param name="mode">How the value combines with the property.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be combined (for Add or Multiply) or does not take a float track.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<float> track, BindingMode mode)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        CheckMode(mode);
        if (mode == BindingMode.Replace) { return Bind(layer, property, track); }
        Slot[] slots = TransformSlots(property, false, () => layer.Transform, value => layer.Transform = value,
            () => layer.Opacity, value => layer.Opacity = ClampOpacity(value));
        return BindSlots(layer, property, mode, slots, track.Evaluate, null);
    }

    /// <summary>
    /// Binds a point track to a layer's <see cref="AnimatedProperty.Position"/> or
    /// <see cref="AnimatedProperty.Scale"/> (its <see cref="DrawingLayer.Transform"/>), combining
    /// as <paramref name="mode"/> says.
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <param name="mode">How the value combines with the property.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be combined (for Add or Multiply) or does not take a point track.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingLayer layer, AnimatedProperty property, KeyframeTrack<PointF> track, BindingMode mode)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        CheckMode(mode);
        if (mode == BindingMode.Replace) { return Bind(layer, property, track); }
        Slot[] slots = TransformSlots(property, true, () => layer.Transform, value => layer.Transform = value, null, null);
        return BindSlots(layer, property, mode, slots, null, track.Evaluate);
    }

    /// <summary>
    /// Binds a float track to the camera's <see cref="AnimatedProperty.Rotation"/> (roll) or
    /// <see cref="AnimatedProperty.Scale"/> (zoom), combining as <paramref name="mode"/> says - a
    /// handheld wobble on top of a move, a breathing zoom on top of a push-in. Keep the combined
    /// zoom positive.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="property">The property.</param>
    /// <param name="track">The track.</param>
    /// <param name="mode">How the value combines with the property.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property does not take a float track on a camera.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingCamera camera, AnimatedProperty property, KeyframeTrack<float> track, BindingMode mode)
    {
        if (camera == null) { throw new ArgumentNullException(nameof(camera)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        CheckMode(mode);
        if (mode == BindingMode.Replace) { return Bind(camera, property, track); }
        Slot[] slots = property switch
        {
            AnimatedProperty.Rotation => new[] { new Slot("Rotation", () => camera.Rotation, value => camera.Rotation = value) },
            AnimatedProperty.Scale => new[] { new Slot("Zoom", () => camera.Zoom, value => camera.Zoom = value) },
            _ => throw WrongTrack(property, "float (on a camera)"),
        };
        return BindSlots(camera, property, mode, slots, track.Evaluate, null);
    }

    /// <summary>
    /// Binds a point track to the camera's <see cref="AnimatedProperty.Position"/> (its pan),
    /// combining as <paramref name="mode"/> says - a shake on top of a pan.
    /// </summary>
    /// <param name="camera">The camera.</param>
    /// <param name="property">The property: <see cref="AnimatedProperty.Position"/> (or <see cref="AnimatedProperty.Anchor"/> with <see cref="BindingMode.Replace"/>).</param>
    /// <param name="track">The track.</param>
    /// <param name="mode">How the value combines with the property.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the property cannot be combined on a camera.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is not defined.</exception>
    public AnimationBinding Bind(DrawingCamera camera, AnimatedProperty property, KeyframeTrack<PointF> track, BindingMode mode)
    {
        if (camera == null) { throw new ArgumentNullException(nameof(camera)); }
        if (track == null) { throw new ArgumentNullException(nameof(track)); }
        CheckMode(mode);
        if (mode == BindingMode.Replace) { return Bind(camera, property, track); }
        if (property != AnimatedProperty.Position) { throw WrongTrack(property, "point (combined, on a camera)"); }
        Slot[] slots =
        {
            new Slot("PanX", () => camera.Pan.X, value => camera.Pan = new PointF(value, camera.Pan.Y)),
            new Slot("PanY", () => camera.Pan.Y, value => camera.Pan = new PointF(camera.Pan.X, value)),
        };
        return BindSlots(camera, property, mode, slots, null, track.Evaluate);
    }

    /// <summary>Removes a binding.</summary>
    /// <param name="binding">The binding to remove.</param>
    /// <returns><c>true</c> when it was in the list.</returns>
    public bool Remove(AnimationBinding binding)
    {
        lock (_locker)
        {
            if (!_bindings.Remove(binding)) { return false; }
            if (binding.RestKeys != null)
            {
                _combinedCount--;
                foreach (object key in binding.RestKeys)
                {
                    if (_rests.TryGetValue(key, out RestEntry entry) && --entry.Count == 0) { _rests.Remove(key); }
                }
            }
            return true;
        }
    }

    /// <summary>Removes every binding.</summary>
    public void Clear()
    {
        lock (_locker)
        {
            _bindings.Clear();
            _rests.Clear();
            _combinedCount = 0;
        }
    }

    /// <summary>
    /// Applies every binding at a time. With only replace bindings (every binding made without a
    /// <see cref="BindingMode"/>), they run in the order they were added, so a later binding of the
    /// same property wins. When <see cref="BindingMode.Add"/> or <see cref="BindingMode.Multiply"/>
    /// bindings exist: each property they combine into is first put back to its resting value (its
    /// value when it was first bound that way), then every replace and callback binding runs in
    /// order, then every multiply binding, then every add binding - so the result never depends on
    /// the order the bindings were made in, and showing a time twice gives the same values.
    /// </summary>
    /// <param name="seconds">The time.</param>
    public void Apply(double seconds)
    {
        AnimationBinding[] bindings;
        Action[] restores = null;
        lock (_locker)
        {
            bindings = _bindings.ToArray();
            if (_combinedCount > 0)
            {
                restores = new Action[_rests.Count];
                int i = 0;
                foreach (RestEntry entry in _rests.Values) { restores[i++] = entry.Restore; }
            }
        }

        if (restores == null)
        {
            foreach (AnimationBinding binding in bindings)
            {
                binding.Apply(seconds);
            }
            return;
        }

        foreach (Action restore in restores) { restore(); }
        foreach (AnimationBinding binding in bindings)
        {
            if (binding.Mode == BindingMode.Replace) { binding.Apply(seconds); }
        }
        foreach (AnimationBinding binding in bindings)
        {
            if (binding.Mode == BindingMode.Multiply) { binding.Apply(seconds); }
        }
        foreach (AnimationBinding binding in bindings)
        {
            if (binding.Mode == BindingMode.Add) { binding.Apply(seconds); }
        }
    }

    private AnimationBinding Add(AnimationBinding binding)
    {
        lock (_locker) { _bindings.Add(binding); }
        return binding;
    }

    private static float ClampOpacity(float value) => Single.IsNaN(value) ? 0f : Math.Clamp(value, 0f, 1f);

    private static Action<double> FloatTransformSetter(AnimatedProperty property, KeyframeTrack<float> track,
        Func<DrawingTransform> get, Action<DrawingTransform> set)
        => property switch
        {
            AnimatedProperty.Rotation => t => set(get().WithRotation(track.Evaluate(t))),
            AnimatedProperty.Scale => t => set(get().WithScale(track.Evaluate(t))),
            AnimatedProperty.ScaleX => t => { DrawingTransform current = get(); set(current.WithScale(track.Evaluate(t), current.ScaleY)); },
            AnimatedProperty.ScaleY => t => { DrawingTransform current = get(); set(current.WithScale(current.ScaleX, track.Evaluate(t))); },
            _ => throw WrongTrack(property, "float"),
        };

    private static Action<double> PointTransformSetter(AnimatedProperty property, KeyframeTrack<PointF> track,
        Func<DrawingTransform> get, Action<DrawingTransform> set)
        => property switch
        {
            AnimatedProperty.Position => t => set(get().WithTranslation(track.Evaluate(t))),
            AnimatedProperty.Anchor => t => set(get().WithAnchor(track.Evaluate(t))),
            AnimatedProperty.Scale => t => { PointF scale = track.Evaluate(t); set(get().WithScale(scale.X, scale.Y)); },
            _ => throw WrongTrack(property, "point"),
        };

    private static Action<double> PathSetter(DrawingElement element, AnimatedProperty property, KeyframeTrack<float> track,
        Action<PathShape, float> set)
    {
        if (element is not PathShape shape) { throw WrongTarget(property, "a path shape"); }
        return t => set(shape, track.Evaluate(t));
    }

    private static int FrameOf(float value)
    {
        if (!Single.IsFinite(value)) { return 0; }
        double floor = Math.Floor((double)value);
        return floor <= Int32.MinValue ? Int32.MinValue : floor >= Int32.MaxValue ? Int32.MaxValue : (int)floor;
    }

    private static float Radius(float value) => Single.IsFinite(value) && value > 0f ? value : 0f;

    private static float NonNegative(float value) => Single.IsNaN(value) || value < 0f ? 0f : value;

    private static PointF FinitePoint(PointF point)
        => new PointF(Single.IsFinite(point.X) ? point.X : 0f, Single.IsFinite(point.Y) ? point.Y : 0f);

    private static Action<double> EffectFloatSetter(AnimatedProperty property, KeyframeTrack<float> track,
        Func<DrawingEffects> get, Action<DrawingEffects> set)
        => property switch
        {
            AnimatedProperty.BlurRadius => t => set((get() ?? DrawingEffects.None).WithBlurRadius(Radius(track.Evaluate(t)))),
            AnimatedProperty.ShadowBlurRadius => t => set((get() ?? DrawingEffects.None).WithShadowBlurRadius(Radius(track.Evaluate(t)))),
            _ => t => set((get() ?? DrawingEffects.None).WithGlowRadius(Radius(track.Evaluate(t)))),
        };

    private static Action<double> EffectColorSetter(AnimatedProperty property, KeyframeTrack<Color> track,
        Func<DrawingEffects> get, Action<DrawingEffects> set)
        => property == AnimatedProperty.ShadowColor
            ? t => set((get() ?? DrawingEffects.None).WithShadowColor(track.Evaluate(t)))
            : t => set((get() ?? DrawingEffects.None).WithGlowColor(track.Evaluate(t)));

    private static ArgumentException WrongTarget(AnimatedProperty property, string target)
        => new ArgumentException($"{property} can only be bound on {target}.", nameof(property));

    private static ArgumentException WrongTrack(AnimatedProperty property, string trackKind)
        => new ArgumentException($"{property} cannot be driven by a {trackKind} track here.", nameof(property));

    private static void CheckMode(BindingMode mode)
    {
        if (!Enum.IsDefined(mode)) { throw new ArgumentOutOfRangeException(nameof(mode)); }
    }

    private static Slot[] ElementSlots(DrawingElement element, AnimatedProperty property, bool point)
        => TransformSlots(property, point, () => element.Transform, value => element.Transform = value,
            () => element.Opacity, value => element.Opacity = ClampOpacity(value));

    //The numeric parts of a transform (and opacity) a property stands for, in value order: a
    //  point fills two slots with X and Y, a float scale both scale slots with the same value
    private static Slot[] TransformSlots(AnimatedProperty property, bool point, Func<DrawingTransform> get,
        Action<DrawingTransform> set, Func<float> getOpacity, Action<float> setOpacity)
    {
        var translationX = new Slot("TranslationX", () => get().Translation.X, value => set(get().WithTranslation(new PointF(value, get().Translation.Y))));
        var translationY = new Slot("TranslationY", () => get().Translation.Y, value => set(get().WithTranslation(new PointF(get().Translation.X, value))));
        var rotation = new Slot("Rotation", () => get().Rotation, value => set(get().WithRotation(value)));
        var scaleX = new Slot("ScaleX", () => get().ScaleX, value => { DrawingTransform current = get(); set(current.WithScale(value, current.ScaleY)); });
        var scaleY = new Slot("ScaleY", () => get().ScaleY, value => { DrawingTransform current = get(); set(current.WithScale(current.ScaleX, value)); });
        if (point)
        {
            return property switch
            {
                AnimatedProperty.Position => new[] { translationX, translationY },
                AnimatedProperty.Scale => new[] { scaleX, scaleY },
                _ => throw WrongTrack(property, "point (combined)"),
            };
        }
        return property switch
        {
            AnimatedProperty.Rotation => new[] { rotation },
            AnimatedProperty.Scale => new[] { scaleX, scaleY },
            AnimatedProperty.ScaleX => new[] { scaleX },
            AnimatedProperty.ScaleY => new[] { scaleY },
            AnimatedProperty.Opacity when getOpacity != null => new[] { new Slot("Opacity", getOpacity, setOpacity) },
            _ => throw WrongTrack(property, "float (combined)"),
        };
    }

    //One binding over numeric slots: Replace sets each slot, Add and Multiply combine with it.
    //  A combining binding registers each slot's resting value (taken at the FIRST combining
    //  bind of that slot) so Apply can put it back before the replace bindings run
    private AnimationBinding BindSlots(object target, AnimatedProperty property, BindingMode mode, Slot[] slots,
        Func<double, float> floatAt, Func<double, PointF> pointAt)
    {
        Action<double> apply = t =>
        {
            if (pointAt != null)
            {
                PointF value = pointAt(t);
                Combine(slots[0], value.X, mode);
                Combine(slots[1], value.Y, mode);
            }
            else
            {
                float value = floatAt(t);
                foreach (Slot slot in slots) { Combine(slot, value, mode); }
            }
        };
        var binding = new AnimationBinding(target, property, apply, mode);
        if (mode == BindingMode.Replace) { return Add(binding); }

        var keys = new object[slots.Length];
        lock (_locker)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                var key = new RestKey(target, slot.Name);
                keys[i] = key;
                if (_rests.TryGetValue(key, out RestEntry entry))
                {
                    entry.Count++;
                }
                else
                {
                    float rest = slot.Get();
                    _rests.Add(key, new RestEntry(() => slot.Set(rest)));
                }
            }
            binding.RestKeys = keys;
            _combinedCount++;
            _bindings.Add(binding);
        }
        return binding;
    }

    private static void Combine(Slot slot, float value, BindingMode mode)
    {
        switch (mode)
        {
            case BindingMode.Add:
                slot.Set(slot.Get() + value);
                break;
            case BindingMode.Multiply:
                slot.Set(slot.Get() * value);
                break;
            default:
                slot.Set(value);
                break;
        }
    }

    private sealed class Slot
    {
        public Slot(string name, Func<float> get, Action<float> set)
        {
            Name = name;
            Get = get;
            Set = set;
        }

        public string Name { get; }

        public Func<float> Get { get; }

        public Action<float> Set { get; }
    }

    private readonly record struct RestKey(object Target, string Slot);

    private sealed class RestEntry
    {
        public RestEntry(Action restore)
        {
            Restore = restore;
            Count = 1;
        }

        public Action Restore { get; }

        public int Count { get; set; }
    }
}
