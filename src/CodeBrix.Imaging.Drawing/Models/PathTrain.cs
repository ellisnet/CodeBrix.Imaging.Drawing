using System;
using System.Collections.Generic;
using CodeBrix.Imaging.Drawing.Animation;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// Several elements travelling ONE path, one behind the other - made by
/// <see cref="DrawingAnimations.BindTrainAlongPath"/> (any elements) or
/// <see cref="DrawingAnimations.BindTextAlongPath"/> (the characters, words or lines of a text
/// block). One binding places every element on each apply: its position on the path, its facing
/// and roll, and its scale and opacity along the path when the options ask for them (see
/// <see cref="PathTrainOptions"/> for the rule). Everything is computed from the time alone, so any
/// frame renders the same in any order. Read where an element is with
/// <see cref="GetPoint(int, double)"/>.
/// </summary>
public sealed class PathTrain
{
    private readonly PathTrainCar[] _cars;
    private readonly DrawingElement[] _elements;
    private readonly double _length;
    private readonly float _restScale;

    internal PathTrain(PathGeometry path, KeyframeTrack<float> progress, PathTrainOptions options, PathTrainCar[] cars)
    {
        Path = path;
        Progress = progress;
        Options = options;
        _cars = cars;
        _elements = new DrawingElement[cars.Length];
        for (int i = 0; i < cars.Length; i++) { _elements[i] = cars[i].Element; }
        _length = path.LengthValue;
        _restScale = options.SpacingScale != null && options.SpacingScale.Count > 0
            ? options.SpacingScale.GetKeyframes()[options.SpacingScale.Count - 1].Value
            : 1f;
    }

    /// <summary>The path every element travels.</summary>
    public PathGeometry Path { get; }

    /// <summary>The lead's progress along the path (a fraction of its length) over time.</summary>
    public KeyframeTrack<float> Progress { get; }

    /// <summary>A copy of the options the train was made with.</summary>
    public PathTrainOptions Options { get; }

    /// <summary>The elements and their places in the train, in the order they were given.</summary>
    public IReadOnlyList<PathTrainCar> Cars => _cars;

    /// <summary>The elements, in the order they were given.</summary>
    public IReadOnlyList<DrawingElement> Elements => _elements;

    /// <summary>The binding that places the elements (remove it to stop the train).</summary>
    public AnimationBinding Binding { get; internal set; }

    /// <summary>
    /// Adds every element to a layer, in order (a convenience: the elements are ordinary elements
    /// and may go anywhere).
    /// </summary>
    /// <param name="layer">The layer.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="layer"/> is null.</exception>
    public void AddTo(DrawingLayer layer)
    {
        if (layer == null) { throw new ArgumentNullException(nameof(layer)); }
        foreach (DrawingElement element in _elements)
        {
            if (element is Stroke stroke) { layer.AddStroke(stroke); }
            else { layer.AddShape((DrawingShape)element); }
        }
    }

    /// <summary>How far along the path an element is at a time, calibrated units (clamped to the path unless the train extends beyond its ends).</summary>
    /// <param name="index">The element's index (in <see cref="Cars"/>).</param>
    /// <param name="seconds">The time.</param>
    /// <returns>The distance along the path.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public float GetDistance(int index, double seconds)
    {
        if (index < 0 || index >= _cars.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
        return (float)Distance(_cars[index], seconds);
    }

    /// <summary>
    /// Where an element's path point is at a time - on the path, or for a train that settles in
    /// place on the element's own copy of it - with its direction.
    /// </summary>
    /// <param name="index">The element's index (in <see cref="Cars"/>).</param>
    /// <param name="seconds">The time.</param>
    /// <returns>The point (in the path's coordinates).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public PathPoint GetPoint(int index, double seconds)
    {
        if (index < 0 || index >= _cars.Length) { throw new ArgumentOutOfRangeException(nameof(index)); }
        return Path.PointAtDistanceExtended(Distance(_cars[index], seconds));
    }

    internal void Apply(double seconds)
    {
        float scaleNow = SpacingScaleAt(seconds);
        foreach (PathTrainCar car in _cars)
        {
            double distance = Distance(car, seconds, scaleNow);
            PathPoint point = Path.PointAtDistanceExtended(distance);
            DrawingElement element = car.Element;
            DrawingTransform current = element.Transform;
            PointF reference = car.Reference ?? current.Anchor;
            var translation = new PointF(point.Position.X - reference.X, point.Position.Y - reference.Y);
            DrawingTransform next = current.WithTranslation(translation);

            bool rolls = car.RollRadius != 0f;
            if (Options.OrientToPath || rolls || Options.RotationOffset != 0f)
            {
                double rotation = Options.RotationOffset;
                if (Options.OrientToPath) { rotation += point.Angle; }
                if (rolls)
                {
                    double upright = (Options.UprightAtProgress * _length) - (car.Gap * (double)_restScale);
                    rotation += (distance - upright) / car.RollRadius * (180.0 / Math.PI);
                }
                next = next.WithRotation((float)rotation);
            }

            double fraction = _length > 0.0 ? distance / _length : 0.0;
            if (Options.ScaleAlongPath != null && Options.ScaleAlongPath.Count > 0)
            {
                next = next.WithScale(Options.ScaleAlongPath.Evaluate(fraction));
            }
            element.Transform = next;
            if (Options.OpacityAlongPath != null && Options.OpacityAlongPath.Count > 0)
            {
                float opacity = Options.OpacityAlongPath.Evaluate(fraction);
                element.Opacity = Single.IsNaN(opacity) ? 0f : Math.Clamp(opacity, 0f, 1f);
            }
        }
    }

    private double Distance(PathTrainCar car, double seconds) => Distance(car, seconds, SpacingScaleAt(seconds));

    private double Distance(PathTrainCar car, double seconds, float spacingScale)
    {
        float progress = Progress.Evaluate(seconds - car.Delay);
        if (Single.IsNaN(progress)) { progress = 0f; }
        double distance = (progress * _length) - (car.Gap * (double)spacingScale);
        return Options.ExtendBeyondEnds ? distance : Math.Clamp(distance, 0.0, _length);
    }

    private float SpacingScaleAt(double seconds)
    {
        KeyframeTrack<float> track = Options.SpacingScale;
        if (track == null || track.Count == 0) { return 1f; }
        float value = track.Evaluate(seconds);
        return Single.IsFinite(value) ? value : 1f;
    }
}
