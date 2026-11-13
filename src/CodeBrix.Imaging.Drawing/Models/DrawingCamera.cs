using System;
using System.Numerics;
#if NOSKIA
using CodeBrix.Imaging.Drawing.NoSkia;
#else
using SkiaSharp;
#endif

namespace CodeBrix.Imaging.Drawing.Models;

/// <summary>
/// A view over the whole scene - pan, zoom and roll - set on
/// <see cref="CodeBrix.Imaging.Drawing.DrawingSession.Camera"/>. The values describe the
/// CAMERA, so the picture moves the opposite way: panning right slides the scene left, a zoom
/// of 2 shows it twice as large, rolling the camera clockwise turns the scene
/// counter-clockwise. Zoom and roll happen about <see cref="Pivot"/> (by default the centre of
/// the calibrated drawing space). Each layer follows the camera by its
/// <see cref="DrawingLayer.Parallax"/> (0 = fixed to the frame). All values are in calibrated
/// drawing units and degrees; the camera is applied while drawing, so content stays sharp at
/// any zoom. Mutable (animate it through <see cref="DrawingAnimations"/> or by setting its
/// properties); a change takes effect on the next render.
/// <para>
/// A scene point p of a layer with parallax f appears at
/// <c>P + Rotate(-f * Rotation, Zoom^f * (p - P - f * Pan))</c>, P being the pivot.
/// </para>
/// </summary>
public sealed class DrawingCamera
{
    private PointF _pan;
    private float _zoom = 1f;
    private float _rotation;
    private PointF? _pivot;

    /// <summary>Creates a camera that changes nothing (no pan, zoom 1, no roll, pivot at the centre).</summary>
    public DrawingCamera()
    {
    }

    /// <summary>How far the camera has moved, calibrated units (the scene slides the other way).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a point that is not finite.</exception>
    public PointF Pan
    {
        get => _pan;
        set
        {
            if (!Single.IsFinite(value.X) || !Single.IsFinite(value.Y)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _pan = value;
        }
    }

    /// <summary>The magnification, positive (1 = none, 2 = twice as large).</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite and positive.</exception>
    public float Zoom
    {
        get => _zoom;
        set
        {
            if (!Single.IsFinite(value) || value <= 0f) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _zoom = value;
        }
    }

    /// <summary>The camera's roll, degrees; positive rolls the camera clockwise, so the scene turns counter-clockwise.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value that is not finite.</exception>
    public float Rotation
    {
        get => _rotation;
        set
        {
            if (!Single.IsFinite(value)) { throw new ArgumentOutOfRangeException(nameof(value)); }
            _rotation = value;
        }
    }

    /// <summary>
    /// The point, in calibrated units, that zoom and roll happen about; or <c>null</c> (the
    /// default) for the centre of the calibrated drawing space.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a point that is not finite.</exception>
    public PointF? Pivot
    {
        get => _pivot;
        set
        {
            if (value.HasValue && (!Single.IsFinite(value.Value.X) || !Single.IsFinite(value.Value.Y)))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _pivot = value;
        }
    }

    /// <summary>Indicates whether the camera changes nothing (no pan, zoom 1, no roll).</summary>
    public bool IsIdentity => _pan.X == 0f && _pan.Y == 0f && _zoom == 1f && _rotation == 0f;

    /// <summary>
    /// The camera's view transform for a layer, as a matrix (System.Numerics row-vector
    /// convention) from scene to view, both in calibrated units.
    /// </summary>
    /// <param name="calibrationSize">The calibrated drawing space (for the default pivot).</param>
    /// <param name="parallax">The layer's parallax factor; 1 by default.</param>
    /// <returns>The matrix.</returns>
    public Matrix3x2 GetMatrix(Size calibrationSize, float parallax = 1f)
    {
        if (!TryGetEffective(calibrationSize.Width, calibrationSize.Height, parallax,
                out PointF pivot, out PointF pan, out float zoom, out float roll))
        {
            return Matrix3x2.Identity;
        }
        return Matrix3x2.CreateTranslation(-pivot.X - pan.X, -pivot.Y - pan.Y)
            * Matrix3x2.CreateScale(zoom)
            * Matrix3x2.CreateRotation(-roll * MathF.PI / 180f)
            * Matrix3x2.CreateTranslation(pivot.X, pivot.Y);
    }

    /// <summary>Where a scene point appears in the view.</summary>
    /// <param name="scenePoint">The point, calibrated scene units.</param>
    /// <param name="calibrationSize">The calibrated drawing space.</param>
    /// <param name="parallax">The parallax factor of the point's layer; 1 by default.</param>
    /// <returns>The point in the view, calibrated units.</returns>
    public PointF ToView(PointF scenePoint, Size calibrationSize, float parallax = 1f)
    {
        Vector2 v = Vector2.Transform(new Vector2(scenePoint.X, scenePoint.Y), GetMatrix(calibrationSize, parallax));
        return new PointF(v.X, v.Y);
    }

    /// <summary>Which scene point appears at a point of the view (the inverse of <see cref="ToView"/>).</summary>
    /// <param name="viewPoint">The point in the view, calibrated units.</param>
    /// <param name="calibrationSize">The calibrated drawing space.</param>
    /// <param name="parallax">The parallax factor of the layer; 1 by default.</param>
    /// <returns>The scene point, calibrated units.</returns>
    public PointF ToScene(PointF viewPoint, Size calibrationSize, float parallax = 1f)
    {
        Matrix3x2.Invert(GetMatrix(calibrationSize, parallax), out Matrix3x2 inverse);
        Vector2 v = Vector2.Transform(new Vector2(viewPoint.X, viewPoint.Y), inverse);
        return new PointF(v.X, v.Y);
    }

    //The camera as felt by a layer with the given parallax; false when it changes nothing there
    internal bool TryGetEffective(int calibrationWidth, int calibrationHeight, float parallax,
        out PointF pivot, out PointF pan, out float zoom, out float roll)
    {
        pivot = _pivot ?? new PointF(calibrationWidth / 2f, calibrationHeight / 2f);
        if (parallax == 1f)
        {
            pan = _pan;
            zoom = _zoom;
            roll = _rotation;
        }
        else
        {
            pan = new PointF(_pan.X * parallax, _pan.Y * parallax);
            zoom = parallax == 0f ? 1f : (float)Math.Pow(_zoom, parallax);
            roll = _rotation * parallax;
        }
        return pan.X != 0f || pan.Y != 0f || zoom != 1f || roll != 0f;
    }

    //Applies the camera to a canvas that draws in calibrated scene units, as the same
    //  sequence the matrix describes
    internal bool ApplyTo(SKCanvas canvas, int calibrationWidth, int calibrationHeight, float parallax)
    {
        if (!TryGetEffective(calibrationWidth, calibrationHeight, parallax, out PointF pivot, out PointF pan, out float zoom, out float roll))
        {
            return false;
        }
        canvas.Translate(pivot.X, pivot.Y);
        if (roll != 0f) { canvas.RotateDegrees(-roll); }
        if (zoom != 1f) { canvas.Scale(zoom); }
        canvas.Translate(-pivot.X - pan.X, -pivot.Y - pan.Y);
        return true;
    }
}
