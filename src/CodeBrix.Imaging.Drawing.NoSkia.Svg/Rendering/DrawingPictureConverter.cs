using System;
using System.Collections.Generic;
using System.Numerics;
using CodeBrix.Imaging;
using CodeBrix.Imaging.Drawing.NoSkia;
using CodeBrix.Imaging.Drawing.NoSkia.Svg.ShimSkiaSharp;
using CodeBrix.Imaging.Formats;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

// <summary>
// Converts a compiled SVG display list - the shim <see cref="SKPicture"/> the scene
// compiler produces - into the Drawing-named <see cref="DrawingPicture"/> that consumers
// see. The conversion is one command in, one command out, so the picture replays through
// <see cref="DrawingCanvas.DrawPicture"/> to exactly the pixels the shim replay produced;
// the two places it cannot be 1:1 are a clip path holding nested intersect-clips (which
// flattens to one canvas clip per nesting level, as the shim replay also applies them) and
// a glyph-identifier text run (which carries no characters, so nothing can be recorded for
// it - a <see cref="DrawingSvgWarningKind.GlyphIdTextRunUnsupported"/> warning is raised
// instead).
// <para>
// Nested pictures are converted once per instance and cached by identity, so the
// sub-pictures an SVG <c>use</c> element shares stay shared in the converted picture too.
// </para>
// <para>
// A text command's style carries the family the compiler RESOLVED the run to, and no
// requested family: the compiler overwrites a paint's typeface with the one it matched, so
// by the time a command reaches the display list the family the document asked for is gone.
// <c>DrawingTextStyle.RequestedFamilyName</c> is therefore always <c>null</c> here.
// </para>
// </summary>
internal sealed class DrawingPictureConverter
{
    private readonly NoSkiaModel _model;
    private readonly ImagingTextOutliner _outliner;

    //Keyed by reference: SKPicture is a record, so the default comparer would fold two
    //  structurally identical but independently recorded pictures into one
    private readonly Dictionary<SKPicture, DrawingPicture> _converted =
        new Dictionary<SKPicture, DrawingPicture>(System.Collections.Generic.ReferenceEqualityComparer.Instance);

    private readonly HashSet<SKPicture> _converting =
        new HashSet<SKPicture>(System.Collections.Generic.ReferenceEqualityComparer.Instance);

    // <summary>
    // Creates a converter.
    // </summary>
    // <param name="model">
    // The model that maps shim paints, paths, colors, and matrices onto their drawing
    // counterparts - the same instance the SVG renderer uses, so conversions match.
    // </param>
    // <param name="outliner">
    // The outliner recorded into every text command; or <c>null</c> when text should
    // carry no outliner (it is then recorded but cannot be drawn).
    // </param>
    // <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    public DrawingPictureConverter(NoSkiaModel model, ImagingTextOutliner outliner)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _outliner = outliner;
    }

    // <summary>
    // How text runs are recorded. Set before converting; the picture is walked once.
    // </summary>
    public DrawingSvgTextEmission TextEmission { get; set; } = DrawingSvgTextEmission.Runs;

    // <summary>
    // An optional callback invoked with each degradation the conversion runs into.
    // </summary>
    public Action<DrawingSvgWarningKind, string> Warning { get; set; }

    // <summary>
    // Converts a shim picture, and everything nested inside it, into a drawing picture.
    // </summary>
    // <param name="picture">The shim picture to convert.</param>
    // <returns>
    // The converted picture; or <c>null</c> when <paramref name="picture"/> is null (or is
    // already being converted further up the recursion, which a well-formed display list
    // never does).
    // </returns>
    public DrawingPicture Convert(SKPicture picture)
    {
        if (picture == null) { return null; }
        if (_converted.TryGetValue(picture, out DrawingPicture cached)) { return cached; }
        if (!_converting.Add(picture)) { return null; }

        try
        {
            var converted = new DrawingPicture(_model.ToDrawingRect(picture.CullRect), ConvertCommands(picture));
            _converted[picture] = converted;
            return converted;
        }
        finally
        {
            _converting.Remove(picture);
        }
    }

    private List<DrawingCommand> ConvertCommands(SKPicture picture)
    {
        var commands = new List<DrawingCommand>();
        if (picture.Commands == null) { return commands; }

        var stack = new List<StackFrame>();
        var openLayers = new List<LayerFrame>();
        Matrix3x2 matrix = Matrix3x2.Identity;

        foreach (CanvasCommand canvasCommand in picture.Commands)
        {
            switch (canvasCommand)
            {
                case SaveCanvasCommand saveCanvasCommand:
                    stack.Add(new StackFrame { Matrix = matrix, Layer = null });
                    commands.Add(new SaveCommand(saveCanvasCommand.Count));
                    break;

                case SaveLayerCanvasCommand saveLayerCanvasCommand:
                {
                    var layer = new LayerFrame
                    {
                        CommandIndex = commands.Count,
                        HasInverse = Matrix3x2.Invert(matrix, out Matrix3x2 pictureToLayer),
                    };
                    layer.PictureToLayer = pictureToLayer;
                    commands.Add(new SaveLayerCommand(
                        saveLayerCanvasCommand.Count, _model.ToDrawingPaint(saveLayerCanvasCommand.Paint), null));
                    stack.Add(new StackFrame { Matrix = matrix, Layer = layer });
                    openLayers.Add(layer);
                    break;
                }

                case RestoreCanvasCommand restoreCanvasCommand:
                {
                    commands.Add(new RestoreCommand(restoreCanvasCommand.Count));
                    if (stack.Count > 0)
                    {
                        StackFrame frame = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        matrix = frame.Matrix;
                        if (frame.Layer != null)
                        {
                            openLayers.Remove(frame.Layer);
                            var opened = (SaveLayerCommand)commands[frame.Layer.CommandIndex];
                            commands[frame.Layer.CommandIndex] = opened with { Bounds = frame.Layer.GetBounds() };
                        }
                    }
                    break;
                }

                case SetMatrixCanvasCommand setMatrixCanvasCommand:
                {
                    Matrix3x2 delta = _model.ToMatrix(setMatrixCanvasCommand.DeltaMatrix);
                    matrix = delta * matrix;
                    commands.Add(new SetMatrixCommand(delta, _model.ToMatrix(setMatrixCanvasCommand.TotalMatrix)));
                    break;
                }

                case ClipRectCanvasCommand clipRectCanvasCommand:
                    commands.Add(new ClipRectCommand(
                        _model.ToDrawingRect(clipRectCanvasCommand.Rect),
                        _model.ToDrawingClipOperation(clipRectCanvasCommand.Operation),
                        clipRectCanvasCommand.Antialias));
                    break;

                case ClipPathCanvasCommand clipPathCanvasCommand:
                {
                    if (clipPathCanvasCommand.ClipPath == null) { break; }

                    var clips = new List<(DrawingPath Path, DrawingClipOperation Operation)>();
                    _model.CollectClipPaths(
                        clipPathCanvasCommand.ClipPath,
                        Matrix3x2.Identity,
                        _model.ToDrawingClipOperation(clipPathCanvasCommand.Operation),
                        clips);
                    foreach ((DrawingPath clipPath, DrawingClipOperation clipOperation) in clips)
                    {
                        commands.Add(new ClipPathCommand(clipPath, clipOperation, clipPathCanvasCommand.Antialias));
                    }
                    break;
                }

                case DrawPathCanvasCommand drawPathCanvasCommand:
                {
                    if (drawPathCanvasCommand.Path == null) { break; }

                    DrawingPath path = _model.ToDrawingPath(drawPathCanvasCommand.Path);
                    commands.Add(new DrawPathCommand(path, _model.ToDrawingPaint(drawPathCanvasCommand.Paint)));
                    if (path != null && !path.IsEmpty) { Accumulate(openLayers, path.Bounds, matrix); }
                    break;
                }

                case DrawImageCanvasCommand drawImageCanvasCommand:
                {
                    if (drawImageCanvasCommand.Image == null) { break; }

                    DrawingRect dest = _model.ToDrawingRect(drawImageCanvasCommand.Dest);
                    byte[] encoded = drawImageCanvasCommand.Image.Data;
                    commands.Add(new DrawImageCommand(
                        _model.ToDrawingBitmap(drawImageCanvasCommand.Image),
                        _model.ToDrawingRect(drawImageCanvasCommand.Source),
                        dest,
                        _model.ToDrawingPaint(drawImageCanvasCommand.Paint),
                        _model.ToDrawingSamplingOptions(
                            drawImageCanvasCommand.Paint?.FilterQuality ?? SKFilterQuality.None),
                        encoded,
                        IdentifyFormat(encoded)));
                    Accumulate(openLayers, dest, matrix);
                    break;
                }

                case DrawPictureCanvasCommand drawPictureCanvasCommand:
                {
                    DrawingPicture nested = Convert(drawPictureCanvasCommand.Picture);
                    if (nested == null) { break; }

                    commands.Add(new DrawPictureCommand(nested));
                    Accumulate(openLayers, nested.CullRect, matrix);
                    break;
                }

                case DrawTextCanvasCommand drawTextCanvasCommand:
                {
                    DrawingCommand command = ConvertText(drawTextCanvasCommand);
                    if (command == null) { break; }

                    commands.Add(command);
                    AccumulateText(openLayers, command, matrix);
                    break;
                }

                case DrawTextBlobCanvasCommand drawTextBlobCanvasCommand:
                {
                    DrawingCommand command = ConvertTextBlob(drawTextBlobCanvasCommand);
                    if (command == null) { break; }

                    commands.Add(command);
                    AccumulateText(openLayers, command, matrix);
                    break;
                }

                case DrawTextOnPathCanvasCommand drawTextOnPathCanvasCommand:
                {
                    Warning?.Invoke(DrawingSvgWarningKind.TextOnPathUnsupported,
                        "A textPath run was recorded but not drawn: text along a path is not implemented.");
                    commands.Add(new DrawTextOnPathCommand(
                        drawTextOnPathCanvasCommand.Text,
                        drawTextOnPathCanvasCommand.Path == null
                            ? null
                            : _model.ToDrawingPath(drawTextOnPathCanvasCommand.Path),
                        drawTextOnPathCanvasCommand.HOffset,
                        drawTextOnPathCanvasCommand.VOffset,
                        _model.ToDrawingPaint(drawTextOnPathCanvasCommand.Paint),
                        ToTextStyle(drawTextOnPathCanvasCommand.Paint, null),
                        _outliner));
                    break;
                }
            }
        }

        //An unbalanced display list (more SaveLayers than Restores) still yields a usable
        //  picture: the layers that never closed simply report no bounds
        return commands;
    }

    private DrawingCommand ConvertText(DrawTextCanvasCommand command)
    {
        DrawingTextStyle style = ToTextStyle(command.Paint, null);
        DrawingPaint paint = _model.ToDrawingPaint(command.Paint);

        if (TextEmission != DrawingSvgTextEmission.PositionedGlyphs || _outliner == null
            || String.IsNullOrEmpty(command.Text))
        {
            return new DrawTextCommand(command.Text, command.X, command.Y, paint, style, _outliner);
        }

        //Per-code-point placement: the run's own alignment is resolved into the origin, and
        //  each position is the advance of everything before it - measured as a prefix, so
        //  the kerning inside the run survives
        string text = command.Text;
        float advance = _outliner.MeasureAdvance(text, style);
        float originX = style.Align switch
        {
            DrawingTextAlign.Center => command.X - (advance / 2f),
            DrawingTextAlign.Right => command.X - advance,
            _ => command.X,
        };

        var positions = new List<DrawingPoint>();
        for (int i = 0; i < text.Length;)
        {
            positions.Add(new DrawingPoint(_outliner.MeasureAdvance(text.Substring(0, i), style), 0f));
            i += Char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
        }

        return new DrawPositionedTextCommand(text, positions.ToArray(), originX, command.Y, paint,
            style with { Align = DrawingTextAlign.Left }, _outliner);
    }

    private DrawingCommand ConvertTextBlob(DrawTextBlobCanvasCommand command)
    {
        SKTextBlob blob = command.TextBlob;
        if (blob?.Text == null || blob.Points == null)
        {
            Warning?.Invoke(DrawingSvgWarningKind.GlyphIdTextRunUnsupported,
                "A text run arrived as glyph identifiers with no characters, so it was skipped.");
            return null;
        }

        var positions = new DrawingPoint[blob.Points.Length];
        for (int i = 0; i < blob.Points.Length; i++)
        {
            positions[i] = _model.ToDrawingPoint(blob.Points[i]);
        }

        //The compiler has already resolved the run's anchoring into the positions, so every
        //  code point draws left-aligned at its own position - the same thing the shim
        //  replay does by cloning the paint with TextAlign.Left
        return new DrawPositionedTextCommand(
            blob.Text,
            positions,
            command.X,
            command.Y,
            _model.ToDrawingPaint(command.Paint),
            ToTextStyle(command.Paint, DrawingTextAlign.Left),
            _outliner);
    }

    // <summary>
    // Builds the style a text command records. The requested family name is not carried:
    // the compiler overwrites a paint's typeface with the one it resolved, so by the time
    // a command reaches the display list the family the document asked for is gone. A
    // paint with no typeface at all is one the compiler could not resolve, and is recorded
    // with no family name so the outliner falls back exactly as rendering does.
    // </summary>
    // <param name="paint">The shim paint the run was recorded with.</param>
    // <param name="alignOverride">The alignment to record instead of the paint's own; or <c>null</c>.</param>
    // <returns>The recorded style.</returns>
    private static DrawingTextStyle ToTextStyle(SKPaint paint, DrawingTextAlign? alignOverride)
    {
        SKTypeface typeface = paint?.Typeface;
        return new DrawingTextStyle(
            typeface?.FamilyName,
            null,
            (DrawingFontWeight)(int)(typeface?.FontWeight ?? SKFontStyleWeight.Normal),
            (DrawingFontWidth)(int)(typeface?.FontWidth ?? SKFontStyleWidth.Normal),
            (DrawingFontSlant)(int)(typeface?.FontSlant ?? SKFontStyleSlant.Upright),
            paint?.TextSize ?? 0f,
            alignOverride ?? (paint?.TextAlign ?? SKTextAlign.Left) switch
            {
                SKTextAlign.Center => DrawingTextAlign.Center,
                SKTextAlign.Right => DrawingTextAlign.Right,
                _ => DrawingTextAlign.Left,
            });
    }

    private static DrawingEncodedImageFormat? IdentifyFormat(byte[] data)
    {
        if (data == null || data.Length == 0) { return null; }

        try
        {
            Image.Identify(data, out IImageFormat format);
            if (format?.Name is not { } name) { return null; }

            if (String.Equals(name, "PNG", StringComparison.OrdinalIgnoreCase)) { return DrawingEncodedImageFormat.Png; }
            if (String.Equals(name, "JPEG", StringComparison.OrdinalIgnoreCase)) { return DrawingEncodedImageFormat.Jpeg; }
            if (String.Equals(name, "GIF", StringComparison.OrdinalIgnoreCase)) { return DrawingEncodedImageFormat.Gif; }
            if (String.Equals(name, "BMP", StringComparison.OrdinalIgnoreCase)) { return DrawingEncodedImageFormat.Bmp; }
            if (String.Equals(name, "WEBP", StringComparison.OrdinalIgnoreCase)) { return DrawingEncodedImageFormat.Webp; }
            return null;
        }
        catch (Exception)
        {
            //Undecodable bytes keep their place in the command; only the format is unknown
            return null;
        }
    }

    private static void AccumulateText(List<LayerFrame> openLayers, DrawingCommand command, Matrix3x2 matrix)
    {
        if (openLayers.Count == 0) { return; }

        //Only worth outlining when a layer is actually open: a run's contribution to a
        //  layer's bounds is the area its glyphs cover, which only the outline knows
        DrawingPath outline = command switch
        {
            DrawTextCommand drawTextCommand => drawTextCommand.GetOutline(),
            DrawPositionedTextCommand drawPositionedTextCommand => drawPositionedTextCommand.GetOutline(),
            _ => null,
        };
        if (outline == null || outline.IsEmpty) { return; }

        Accumulate(openLayers, outline.Bounds, matrix);
    }

    private static void Accumulate(List<LayerFrame> openLayers, DrawingRect local, Matrix3x2 matrix)
    {
        if (openLayers.Count == 0) { return; }

        foreach (LayerFrame layer in openLayers)
        {
            if (!layer.HasInverse) { continue; }

            Matrix3x2 localToLayer = matrix * layer.PictureToLayer;
            layer.Add(Vector2.Transform(new Vector2(local.Left, local.Top), localToLayer));
            layer.Add(Vector2.Transform(new Vector2(local.Right, local.Top), localToLayer));
            layer.Add(Vector2.Transform(new Vector2(local.Right, local.Bottom), localToLayer));
            layer.Add(Vector2.Transform(new Vector2(local.Left, local.Bottom), localToLayer));
        }
    }

    private sealed class StackFrame
    {
        public Matrix3x2 Matrix;
        public LayerFrame Layer;
    }

    private sealed class LayerFrame
    {
        private float _left;
        private float _top;
        private float _right;
        private float _bottom;
        private bool _any;

        public int CommandIndex;
        public bool HasInverse;
        public Matrix3x2 PictureToLayer;

        public void Add(Vector2 point)
        {
            if (!_any)
            {
                _left = _right = point.X;
                _top = _bottom = point.Y;
                _any = true;
                return;
            }

            _left = MathF.Min(_left, point.X);
            _top = MathF.Min(_top, point.Y);
            _right = MathF.Max(_right, point.X);
            _bottom = MathF.Max(_bottom, point.Y);
        }

        public DrawingRect? GetBounds()
            => _any ? new DrawingRect(_left, _top, _right, _bottom) : (DrawingRect?)null;
    }
}
