using System;
using System.Numerics;
using CodeBrix.Imaging.Drawing.NoSkia.Raster;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// A source of per-position paint colors - a solid color or a gradient - assigned to
/// <see cref="DrawingPaint.Shader"/>. Shader coordinates are the coordinate space the
/// painted geometry is expressed in (optionally adjusted by a local matrix).
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
    /// Creates a shader that paints a repeating tile drawn from a recorded picture - how a
    /// pattern fill paints. The tile is rasterized on demand, at the resolution the
    /// transform in force asks for, and re-rasterized when that resolution changes, so a
    /// pattern stays sharp at any scale.
    /// </summary>
    /// <param name="tile">The picture one tile draws.</param>
    /// <param name="tileRect">
    /// The region of the picture's coordinate space one tile covers - the tile's origin and
    /// its spacing in both directions. A rectangle with no area produces a shader that
    /// paints nothing.
    /// </param>
    /// <param name="tileModeX">How positions outside the tile are painted horizontally.</param>
    /// <param name="tileModeY">How positions outside the tile are painted vertically.</param>
    /// <param name="localMatrix">An optional extra transform applied to the tiling.</param>
    /// <returns>The shader.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tile"/> is null.</exception>
    public static DrawingShader CreatePicture(DrawingPicture tile, DrawingRect tileRect,
        DrawingShaderTileMode tileModeX, DrawingShaderTileMode tileModeY,
        Matrix3x2? localMatrix = null)
        => new PictureShaderImpl(tile ?? throw new ArgumentNullException(nameof(tile)),
            tileRect, tileModeX, tileModeY, localMatrix);

    /// <summary>
    /// What this shader paints - the discriminator that says which of the properties below
    /// carry meaningful values.
    /// </summary>
    public abstract DrawingShaderKind Kind { get; }

    /// <summary>
    /// The painted color, for <see cref="DrawingShaderKind.Color"/>; transparent black for
    /// every other kind.
    /// </summary>
    public virtual DrawingColor Color => default;

    /// <summary>
    /// The gradient's colors, in order, for the gradient kinds; <c>null</c> otherwise. The
    /// returned array is a copy, so changing it does not change the shader.
    /// </summary>
    public virtual DrawingColor[] Colors => null;

    /// <summary>
    /// The gradient's stop positions in 0..1, one per entry of <see cref="Colors"/>, for
    /// the gradient kinds; <c>null</c> when the gradient was built with evenly spaced stops
    /// (or the shader is not a gradient). The returned array is a copy.
    /// </summary>
    public virtual float[] Positions => null;

    /// <summary>
    /// Where the gradient begins - the axis start for
    /// <see cref="DrawingShaderKind.LinearGradient"/>, the focal circle's center for
    /// <see cref="DrawingShaderKind.TwoPointConicalGradient"/>.
    /// </summary>
    public virtual DrawingPoint Start => default;

    /// <summary>
    /// Where the gradient ends - the axis end for
    /// <see cref="DrawingShaderKind.LinearGradient"/>, the end circle's center for
    /// <see cref="DrawingShaderKind.TwoPointConicalGradient"/>.
    /// </summary>
    public virtual DrawingPoint End => default;

    /// <summary>
    /// The center of a <see cref="DrawingShaderKind.RadialGradient"/>; the origin for every
    /// other kind.
    /// </summary>
    public virtual DrawingPoint Center => default;

    /// <summary>
    /// The radius at which a <see cref="DrawingShaderKind.RadialGradient"/>'s last stop
    /// lands; zero for every other kind.
    /// </summary>
    public virtual float Radius => 0f;

    /// <summary>
    /// The focal circle's radius, for
    /// <see cref="DrawingShaderKind.TwoPointConicalGradient"/>; zero otherwise.
    /// </summary>
    public virtual float StartRadius => 0f;

    /// <summary>
    /// The end circle's radius, for the radial and two-point conical kinds; zero
    /// otherwise.
    /// </summary>
    public virtual float EndRadius => 0f;

    /// <summary>
    /// How positions outside the gradient (or outside the tile) are painted; only
    /// meaningful for the gradient and picture kinds.
    /// </summary>
    public virtual DrawingShaderTileMode TileMode => DrawingShaderTileMode.Clamp;

    /// <summary>
    /// The shader's optional extra transform, applied to the shader's geometry ahead of the
    /// canvas transform; <c>null</c> when the shader carries none.
    /// </summary>
    public virtual Matrix3x2? LocalMatrix => null;

    /// <summary>
    /// How positions outside the tile are painted horizontally, for
    /// <see cref="DrawingShaderKind.Picture"/>; the same as <see cref="TileMode"/> for
    /// every other kind, which tiles along one axis only.
    /// </summary>
    public virtual DrawingShaderTileMode TileModeX => TileMode;

    /// <summary>
    /// How positions outside the tile are painted vertically, for
    /// <see cref="DrawingShaderKind.Picture"/>; the same as <see cref="TileMode"/> for
    /// every other kind, which tiles along one axis only.
    /// </summary>
    public virtual DrawingShaderTileMode TileModeY => TileMode;

    /// <summary>
    /// The picture one tile draws, for <see cref="DrawingShaderKind.Picture"/>;
    /// <c>null</c> for every other kind.
    /// </summary>
    public virtual DrawingPicture Picture => null;

    /// <summary>
    /// The region of the picture's coordinate space one tile covers, for
    /// <see cref="DrawingShaderKind.Picture"/>; an empty rectangle for every other kind.
    /// </summary>
    public virtual DrawingRect TileRect => default;

    /// <summary>
    /// Resolves this shader's color at a position in shader-local coordinates - for the
    /// rendering internals.
    /// </summary>
    internal abstract void GetLocalColor(Vector2 local,
        out float red, out float green, out float blue, out float alpha);

    /// <summary>
    /// Returns the shader to sample with, given the transform from this shader's local
    /// space to device pixels - the hook a shader that must rasterize content uses to pick
    /// its resolution. Shaders that evaluate straight from their parameters return
    /// themselves; the returned shader has the same inspection values as this one either
    /// way, so nothing a consumer can see changes.
    /// </summary>
    /// <param name="localToDevice">The transform from shader-local space to device pixels.</param>
    /// <returns>The shader to sample with.</returns>
    internal virtual DrawingShader PrepareForDevice(Matrix3x2 localToDevice) => this;

    /// <summary>
    /// The largest scale factor a transform applies - its 2x2 linear part's largest
    /// singular value.
    /// </summary>
    /// <param name="matrix">The transform to measure.</param>
    /// <returns>The largest scale factor.</returns>
    private static float MaxScale(Matrix3x2 matrix)
    {
        float a = (matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12);
        float b = (matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22);
        float c = (matrix.M11 * matrix.M21) + (matrix.M12 * matrix.M22);
        float difference = MathF.Sqrt(((a - b) * (a - b)) + (4 * c * c));
        return MathF.Sqrt(Math.Max(0f, (a + b + difference) / 2f));
    }

    private sealed class ColorShaderImpl : DrawingShader
    {
        private readonly DrawingColor _color;
        private readonly float _red;
        private readonly float _green;
        private readonly float _blue;
        private readonly float _alpha;

        public ColorShaderImpl(DrawingColor color)
        {
            _color = color;
            _red = color.Red / 255f;
            _green = color.Green / 255f;
            _blue = color.Blue / 255f;
            _alpha = color.Alpha / 255f;
        }

        public override DrawingShaderKind Kind => DrawingShaderKind.Color;

        public override DrawingColor Color => _color;

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
        private readonly DrawingColor[] _colors; //The factory inputs, kept for inspection
        private readonly float[] _positions;
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
            _colors = (DrawingColor[])colors.Clone();
            _positions = positions != null ? (float[])positions.Clone() : null;
            _lut = BuildLut(colors, positions);
        }

        public override DrawingShaderKind Kind => _kind switch
        {
            GradientKind.Linear => DrawingShaderKind.LinearGradient,
            GradientKind.Radial => DrawingShaderKind.RadialGradient,
            _ => DrawingShaderKind.TwoPointConicalGradient,
        };

        public override DrawingColor[] Colors => (DrawingColor[])_colors.Clone();

        public override float[] Positions => _positions != null ? (float[])_positions.Clone() : null;

        public override DrawingPoint Start => new DrawingPoint(_start.X, _start.Y);

        public override DrawingPoint End => new DrawingPoint(_end.X, _end.Y);

        public override DrawingPoint Center
            => _kind == GradientKind.Radial ? new DrawingPoint(_end.X, _end.Y) : default;

        public override float Radius => _kind == GradientKind.Radial ? _endRadius : 0f;

        public override float StartRadius => _startRadius;

        public override float EndRadius => _endRadius;

        public override DrawingShaderTileMode TileMode => _tileMode;

        public override Matrix3x2? LocalMatrix => _localMatrix;

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

    private sealed class PictureShaderImpl : DrawingShader
    {
        private readonly DrawingPicture _picture;
        private readonly DrawingRect _tileRect;
        private readonly DrawingShaderTileMode _tileModeX;
        private readonly DrawingShaderTileMode _tileModeY;
        private readonly Matrix3x2? _localMatrix;
        private readonly PictureTileCache _tiles; //Shared with every prepared copy
        private readonly DrawingBitmap _tile; //Null until PrepareForDevice picks a resolution

        public PictureShaderImpl(DrawingPicture picture, DrawingRect tileRect,
            DrawingShaderTileMode tileModeX, DrawingShaderTileMode tileModeY, Matrix3x2? localMatrix)
            : this(picture, tileRect, tileModeX, tileModeY, localMatrix, new PictureTileCache(), null)
        {
        }

        private PictureShaderImpl(DrawingPicture picture, DrawingRect tileRect,
            DrawingShaderTileMode tileModeX, DrawingShaderTileMode tileModeY, Matrix3x2? localMatrix,
            PictureTileCache tiles, DrawingBitmap tile)
        {
            _picture = picture;
            _tileRect = tileRect;
            _tileModeX = tileModeX;
            _tileModeY = tileModeY;
            _localMatrix = localMatrix;
            _tiles = tiles;
            _tile = tile;
        }

        public override DrawingShaderKind Kind => DrawingShaderKind.Picture;

        public override DrawingShaderTileMode TileMode => _tileModeX;

        public override DrawingShaderTileMode TileModeX => _tileModeX;

        public override DrawingShaderTileMode TileModeY => _tileModeY;

        public override DrawingPicture Picture => _picture;

        public override DrawingRect TileRect => _tileRect;

        public override Matrix3x2? LocalMatrix => _localMatrix;

        internal override DrawingShader PrepareForDevice(Matrix3x2 localToDevice)
        {
            if (_tileRect.Width <= 0 || _tileRect.Height <= 0) { return this; }

            PictureTileCache.ChooseTileSize(_tileRect, MaxScale(localToDevice), out int width, out int height);
            if (_tile != null && _tile.Width == width && _tile.Height == height) { return this; }

            //A prepared copy rather than a field write: the shader a consumer holds stays
            //  immutable, and two canvases may paint with it at two scales at once
            return new PictureShaderImpl(_picture, _tileRect, _tileModeX, _tileModeY, _localMatrix,
                _tiles, _tiles.GetTile(_picture, _tileRect, width, height));
        }

        internal override void GetLocalColor(Vector2 local,
            out float red, out float green, out float blue, out float alpha)
        {
            if (_tile == null)
            {
                //Not prepared (or an empty tile rectangle): the pattern paints nothing
                red = green = blue = alpha = 0;
                return;
            }

            float x = (local.X - _tileRect.Left) / _tileRect.Width * _tile.Width;
            float y = (local.Y - _tileRect.Top) / _tileRect.Height * _tile.Height;
            TileSampler.Sample(_tile, x, y, _tileModeX, _tileModeY,
                out red, out green, out blue, out alpha);
        }
    }
}
