using System;
using System.Numerics;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A source of per-position paint colors - a solid color or a gradient - assigned to
/// <see cref="DrawingPaint.Shader"/>, API-compatible with the SkiaSharp <c>SKShader</c>
/// factory surface this managed implementation supports. Shader coordinates are the
/// coordinate space the painted geometry is expressed in (optionally adjusted by a local
/// matrix), exactly as in Skia.
/// </summary>
public abstract class DrawingShader
{
    private protected DrawingShader()
    {
    }

    /// <summary>
    /// Creates a shader that paints one solid color.
    /// </summary>
    /// <param name="color">The color to paint.</param>
    /// <returns>The shader.</returns>
    public static DrawingShader CreateColor(DrawingColor color) => new ColorShaderImpl(color);

    /// <summary>
    /// Creates a linear gradient between two points.
    /// </summary>
    /// <param name="start">The position where the gradient begins.</param>
    /// <param name="end">The position where the gradient ends.</param>
    /// <param name="colors">The gradient's colors, in order.</param>
    /// <param name="positions">
    /// The stop positions in 0..1, one per color; or <c>null</c> for evenly spaced stops.
    /// </param>
    /// <param name="tileMode">How positions outside the gradient are painted.</param>
    /// <param name="localMatrix">An optional extra transform applied to the gradient's geometry.</param>
    /// <returns>The shader.</returns>
    public static DrawingShader CreateLinearGradient(DrawingPoint start, DrawingPoint end,
        DrawingColor[] colors, float[] positions, DrawingShaderTileMode tileMode,
        Matrix3x2? localMatrix = null)
        => new GradientShaderImpl(GradientShaderImpl.GradientKind.Linear,
            new Vector2(start.X, start.Y), 0f, new Vector2(end.X, end.Y), 0f,
            colors, positions, tileMode, localMatrix);

    /// <summary>
    /// Creates a radial gradient emanating from a center point.
    /// </summary>
    /// <param name="center">The gradient's center.</param>
    /// <param name="radius">The radius at which the last color stop lands.</param>
    /// <param name="colors">The gradient's colors, in order.</param>
    /// <param name="positions">
    /// The stop positions in 0..1, one per color; or <c>null</c> for evenly spaced stops.
    /// </param>
    /// <param name="tileMode">How positions outside the gradient are painted.</param>
    /// <param name="localMatrix">An optional extra transform applied to the gradient's geometry.</param>
    /// <returns>The shader.</returns>
    public static DrawingShader CreateRadialGradient(DrawingPoint center, float radius,
        DrawingColor[] colors, float[] positions, DrawingShaderTileMode tileMode,
        Matrix3x2? localMatrix = null)
        => new GradientShaderImpl(GradientShaderImpl.GradientKind.Radial,
            new Vector2(center.X, center.Y), 0f, new Vector2(center.X, center.Y), radius,
            colors, positions, tileMode, localMatrix);

    /// <summary>
    /// Creates a two-point conical gradient (the SVG focal radial gradient model).
    /// </summary>
    /// <param name="start">The center of the start circle (the focal point).</param>
    /// <param name="startRadius">The radius of the start circle.</param>
    /// <param name="end">The center of the end circle.</param>
    /// <param name="endRadius">The radius of the end circle.</param>
    /// <param name="colors">The gradient's colors, in order.</param>
    /// <param name="positions">
    /// The stop positions in 0..1, one per color; or <c>null</c> for evenly spaced stops.
    /// </param>
    /// <param name="tileMode">How positions outside the gradient are painted.</param>
    /// <param name="localMatrix">An optional extra transform applied to the gradient's geometry.</param>
    /// <returns>The shader.</returns>
    public static DrawingShader CreateTwoPointConicalGradient(DrawingPoint start, float startRadius,
        DrawingPoint end, float endRadius, DrawingColor[] colors, float[] positions,
        DrawingShaderTileMode tileMode, Matrix3x2? localMatrix = null)
        => new GradientShaderImpl(GradientShaderImpl.GradientKind.TwoPointConical,
            new Vector2(start.X, start.Y), startRadius, new Vector2(end.X, end.Y), endRadius,
            colors, positions, tileMode, localMatrix);

    /// <summary>
    /// Resolves this shader's color at a position in shader-local coordinates - for the
    /// rendering internals.
    /// </summary>
    internal abstract void GetLocalColor(Vector2 local,
        out float red, out float green, out float blue, out float alpha);

    /// <summary>The shader's optional local matrix.</summary>
    internal virtual Matrix3x2? LocalMatrix => null;

    private sealed class ColorShaderImpl : DrawingShader
    {
        private readonly float _red;
        private readonly float _green;
        private readonly float _blue;
        private readonly float _alpha;

        public ColorShaderImpl(DrawingColor color)
        {
            _red = color.Red / 255f;
            _green = color.Green / 255f;
            _blue = color.Blue / 255f;
            _alpha = color.Alpha / 255f;
        }

        internal override void GetLocalColor(Vector2 local,
            out float red, out float green, out float blue, out float alpha)
        {
            red = _red;
            green = _green;
            blue = _blue;
            alpha = _alpha;
        }
    }

    private sealed class GradientShaderImpl : DrawingShader
    {
        internal enum GradientKind
        {
            Linear,
            Radial,
            TwoPointConical,
        }

        private const int LutSize = 256;

        private readonly GradientKind _kind;
        private readonly Vector2 _start;
        private readonly float _startRadius;
        private readonly Vector2 _end;
        private readonly float _endRadius;
        private readonly DrawingShaderTileMode _tileMode;
        private readonly Matrix3x2? _localMatrix;
        private readonly float[] _lut; //LutSize x (r, g, b, a), straight alpha

        public GradientShaderImpl(GradientKind kind, Vector2 start, float startRadius,
            Vector2 end, float endRadius, DrawingColor[] colors, float[] positions,
            DrawingShaderTileMode tileMode, Matrix3x2? localMatrix)
        {
            if (colors == null || colors.Length == 0)
            {
                throw new ArgumentException("A gradient requires at least one color.", nameof(colors));
            }
            if (positions != null && positions.Length != colors.Length)
            {
                throw new ArgumentException("When positions are provided, there must be one per color.", nameof(positions));
            }

            _kind = kind;
            _start = start;
            _startRadius = startRadius;
            _end = end;
            _endRadius = endRadius;
            _tileMode = tileMode;
            _localMatrix = localMatrix;
            _lut = BuildLut(colors, positions);
        }

        internal override Matrix3x2? LocalMatrix => _localMatrix;

        internal override void GetLocalColor(Vector2 local,
            out float red, out float green, out float blue, out float alpha)
        {
            float t;
            var defined = true;
            switch (_kind)
            {
                case GradientKind.Linear:
                {
                    Vector2 axis = _end - _start;
                    float lengthSquared = axis.LengthSquared();
                    t = lengthSquared > 0 ? Vector2.Dot(local - _start, axis) / lengthSquared : 0f;
                    break;
                }
                case GradientKind.Radial:
                {
                    t = _endRadius > 0 ? Vector2.Distance(local, _end) / _endRadius : 0f;
                    break;
                }
                default:
                {
                    defined = SolveConical(local, out t);
                    break;
                }
            }

            if (!defined || (_tileMode == DrawingShaderTileMode.Decal && (t < 0 || t > 1)))
            {
                red = green = blue = alpha = 0;
                return;
            }

            switch (_tileMode)
            {
                case DrawingShaderTileMode.Repeat:
                    t -= MathF.Floor(t);
                    break;
                case DrawingShaderTileMode.Mirror:
                {
                    t = MathF.Abs(t);
                    float period = t - (2f * MathF.Floor(t / 2f));
                    t = period > 1f ? 2f - period : period;
                    break;
                }
                default:
                    t = Math.Clamp(t, 0f, 1f);
                    break;
            }

            int index = Math.Clamp((int)((t * (LutSize - 1)) + 0.5f), 0, LutSize - 1) * 4;
            red = _lut[index];
            green = _lut[index + 1];
            blue = _lut[index + 2];
            alpha = _lut[index + 3];
        }

        //Two-point conical: find the largest t with |local - lerp(start, end, t)| = lerp(r0, r1, t)
        private bool SolveConical(Vector2 local, out float t)
        {
            Vector2 centerDelta = _end - _start;
            float radiusDelta = _endRadius - _startRadius;
            Vector2 fromStart = local - _start;

            float a = centerDelta.LengthSquared() - (radiusDelta * radiusDelta);
            float b = -2f * (Vector2.Dot(fromStart, centerDelta) + (_startRadius * radiusDelta));
            float c = fromStart.LengthSquared() - (_startRadius * _startRadius);

            if (MathF.Abs(a) < 1e-6f)
            {
                if (MathF.Abs(b) < 1e-6f) { t = 0; return false; }
                t = -c / b;
            }
            else
            {
                float discriminant = (b * b) - (4f * a * c);
                if (discriminant < 0) { t = 0; return false; }
                float root = MathF.Sqrt(discriminant);
                float t1 = (-b + root) / (2f * a);
                float t2 = (-b - root) / (2f * a);
                t = MathF.Max(t1, t2);
                if (_startRadius + (t * radiusDelta) < 0)
                {
                    t = MathF.Min(t1, t2);
                    if (_startRadius + (t * radiusDelta) < 0) { return false; }
                }
            }
            return true;
        }

        private static float[] BuildLut(DrawingColor[] colors, float[] positions)
        {
            var stops = new (float Position, DrawingColor Color)[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float position = positions != null
                    ? Math.Clamp(positions[i], 0f, 1f)
                    : (colors.Length == 1 ? 0f : i / (float)(colors.Length - 1));
                if (i > 0 && position < stops[i - 1].Position)
                {
                    position = stops[i - 1].Position; //Stops must not move backwards
                }
                stops[i] = (position, colors[i]);
            }

            var lut = new float[LutSize * 4];
            var stopIndex = 0;
            for (int i = 0; i < LutSize; i++)
            {
                float t = i / (float)(LutSize - 1);
                while (stopIndex < stops.Length - 1 && t > stops[stopIndex + 1].Position)
                {
                    stopIndex++;
                }

                DrawingColor color;
                if (stopIndex >= stops.Length - 1 || t <= stops[0].Position)
                {
                    color = t <= stops[0].Position ? stops[0].Color : stops[stops.Length - 1].Color;
                    WriteLut(lut, i, color, color, 0f);
                    continue;
                }

                (float fromPosition, DrawingColor fromColor) = stops[stopIndex];
                (float toPosition, DrawingColor toColor) = stops[stopIndex + 1];
                float segment = toPosition - fromPosition;
                float fraction = segment > 0 ? Math.Clamp((t - fromPosition) / segment, 0f, 1f) : 0f;
                WriteLut(lut, i, fromColor, toColor, fraction);
            }
            return lut;
        }

        private static void WriteLut(float[] lut, int index, DrawingColor from, DrawingColor to, float fraction)
        {
            int offset = index * 4;
            lut[offset] = ((from.Red / 255f) * (1 - fraction)) + ((to.Red / 255f) * fraction);
            lut[offset + 1] = ((from.Green / 255f) * (1 - fraction)) + ((to.Green / 255f) * fraction);
            lut[offset + 2] = ((from.Blue / 255f) * (1 - fraction)) + ((to.Blue / 255f) * fraction);
            lut[offset + 3] = ((from.Alpha / 255f) * (1 - fraction)) + ((to.Alpha / 255f) * fraction);
        }
    }
}
