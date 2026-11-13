using System;
using System.Collections.Generic;
using System.Text;
using CodeBrix.Imaging.Drawing.Layout;
using CodeBrix.Imaging.Drawing.Paths;
using CodeBrix.Imaging.Drawing.Shapes;
using CodeBrix.Imaging.Drawing.Text;

namespace CodeBrix.Imaging.Drawing.Captions;

/// <summary>
/// Burns a <see cref="CaptionTrack"/> into frames: for any time it builds the shapes that show the
/// cues active then - each cue's text laid out (<see cref="TextLayoutEngine"/>) and wrapped to the
/// style's width, on an optional rounded box, placed with a <see cref="DrawingLayout"/> anchor
/// relative to the frame or its safe area. Cues showing at the same time and sharing a position
/// are stacked into one column, the earliest on top. Everything is a pure function of the time, in
/// fonts the caller registered, so frames render the same in any order and in both packages.
/// <para>
/// Use <see cref="CreateShape"/> (or <see cref="DrawingSession.DrawCaptions"/>) for an element that
/// follows the session's time by itself, or <see cref="GetShapeAt(double)"/> to build frames by hand.
/// Layouts and shapes are built once per cue and per set of active cues and then reused, so a
/// caption that stays up costs nothing per frame (and its layer is not redrawn).
/// </para>
/// </summary>
public sealed class CaptionPresenter
{
    private readonly object _locker = new object();
    private readonly CaptionStyle _style;
    private readonly Dictionary<int, CueBox> _cueBoxes = new Dictionary<int, CueBox>();
    private readonly Dictionary<string, DrawingGroup> _shapes = new Dictionary<string, DrawingGroup>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a presenter.
    /// </summary>
    /// <param name="track">The captions.</param>
    /// <param name="fonts">The registry the caption font comes from.</param>
    /// <param name="layout">The frame and safe area the captions are placed in (calibrated units).</param>
    /// <param name="style">How the captions look; or <c>null</c> for the defaults. It is copied.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="track"/>, <paramref name="fonts"/> or <paramref name="layout"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a style value is out of range.</exception>
    public CaptionPresenter(CaptionTrack track, FontRegistry fonts, DrawingLayout layout, CaptionStyle style = null)
    {
        Track = track ?? throw new ArgumentNullException(nameof(track));
        Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _style = (style ?? new CaptionStyle()).Clone();
        _style.Validate();
    }

    /// <summary>The captions.</summary>
    public CaptionTrack Track { get; }

    /// <summary>The registry the caption font comes from.</summary>
    public FontRegistry Fonts { get; }

    /// <summary>The frame and safe area the captions are placed in.</summary>
    public DrawingLayout Layout { get; }

    /// <summary>A copy of the style the captions are drawn in.</summary>
    public CaptionStyle Style => _style.Clone();

    /// <summary>
    /// The shape that shows the captions active at a time: a <see cref="DrawingGroup"/> of each
    /// active cue's box and text (an empty group when none is active). The same instance is
    /// returned for as long as the same cues are active.
    /// </summary>
    /// <param name="seconds">The time.</param>
    /// <returns>The shape.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a cue's own style has a value out of range.</exception>
    public DrawingShape GetShapeAt(double seconds)
    {
        IReadOnlyList<int> active = Track.GetActiveCueIndices(seconds);
        var key = new StringBuilder();
        foreach (int index in active) { key.Append(index).Append(','); }
        string name = key.ToString();
        lock (_locker)
        {
            if (_shapes.TryGetValue(name, out DrawingGroup cached)) { return cached; }
            DrawingGroup group = Build(active);
            if (_shapes.Count >= 256) { _shapes.Clear(); }
            _shapes[name] = group;
            return group;
        }
    }

    /// <summary>
    /// Creates an element that shows the captions at the session's time: an
    /// <see cref="AnimatedShape"/> over <see cref="GetShapeAt(double)"/> (the session updates it
    /// whenever its time is set).
    /// </summary>
    /// <param name="initialTime">The time it first shows, seconds.</param>
    /// <returns>The element.</returns>
    public AnimatedShape CreateShape(double initialTime = 0) => new AnimatedShape(GetShapeAt, initialTime);

    /// <summary>The laid-out text of one cue, as the presenter draws it.</summary>
    /// <param name="cueIndex">The cue's index in <see cref="CaptionTrack.Cues"/>.</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
    public TextLayoutResult GetCueLayout(int cueIndex)
    {
        if (cueIndex < 0 || cueIndex >= Track.Count) { throw new ArgumentOutOfRangeException(nameof(cueIndex)); }
        lock (_locker) { return GetCueBox(cueIndex).Text; }
    }

    private sealed class CueBox
    {
        public TextLayoutResult Text;
        public CaptionStyle Style;
        public float Size;
        public LayoutPosition Position;
        public SizeF BoxSize;
    }

    private CueBox GetCueBox(int index)
    {
        if (_cueBoxes.TryGetValue(index, out CueBox box)) { return box; }
        CaptionCue cue = Track.Cues[index];
        CaptionStyle style = cue.Style ?? _style;
        if (cue.Style != null) { style.Validate(); }
        float size = style.Size > 0f ? style.Size : Math.Max(1f, Layout.ShortSide * 0.045f);
        LayoutPosition position = cue.Position ?? style.Position;
        RectangleF reference = Layout.GetRectangle(position.Reference);
        float padX = style.PaddingX * size;
        float padY = style.PaddingY * size;
        var options = new TextLayoutOptions
        {
            MaxWidth = Math.Max(size, (reference.Width * style.MaxWidthFraction) - (2f * padX)),
            Align = cue.Align ?? style.Align,
            LineSpacing = style.LineSpacing,
            MaxLines = style.MaxLines,
            Trimming = style.MaxLines > 0 ? TextTrimming.WordEllipsis : TextTrimming.None,
        };
        TextLayoutResult text = TextLayoutEngine.Layout(Fonts, cue.Text, size, style.FamilyName, options);
        box = new CueBox
        {
            Text = text,
            Style = style,
            Size = size,
            Position = position,
            BoxSize = new SizeF(text.ContentBounds.Width + (2f * padX), text.ContentBounds.Height + (2f * padY)),
        };
        _cueBoxes[index] = box;
        return box;
    }

    private DrawingGroup Build(IReadOnlyList<int> active)
    {
        var group = new DrawingGroup();

        //Cues sharing a position stack into one column, in start order, the earliest on top
        var columns = new List<(LayoutPosition Position, List<CueBox> Boxes)>();
        foreach (int index in active)
        {
            CueBox box = GetCueBox(index);
            List<CueBox> column = null;
            foreach ((LayoutPosition position, List<CueBox> boxes) in columns)
            {
                if (position == box.Position) { column = boxes; break; }
            }
            if (column == null)
            {
                column = new List<CueBox>();
                columns.Add((box.Position, column));
            }
            column.Add(box);
        }

        foreach ((LayoutPosition position, List<CueBox> boxes) in columns)
        {
            float spacing = _style.CueSpacing * boxes[0].Size;
            float width = 0f, height = 0f;
            foreach (CueBox box in boxes)
            {
                width = Math.Max(width, box.BoxSize.Width);
                height += box.BoxSize.Height;
            }
            height += spacing * (boxes.Count - 1);
            RectangleF stack = Layout.PlaceBox(new SizeF(width, height), position);
            float y = stack.Y;
            foreach (CueBox box in boxes)
            {
                float x = stack.X + ((stack.Width - box.BoxSize.Width) * position.Anchor.X);
                AddCue(group, box, new RectangleF(x, y, box.BoxSize.Width, box.BoxSize.Height));
                y += box.BoxSize.Height + spacing;
            }
        }
        return group;
    }

    private static void AddCue(DrawingGroup group, CueBox box, RectangleF rect)
    {
        CaptionStyle style = box.Style;
        float size = box.Size;
        if (style.BackgroundColor.HasValue && rect.Width > 0f && rect.Height > 0f)
        {
            group.Add(new RectangleShape(rect.X, rect.Y, rect.Width, rect.Height, 1f, style.BackgroundColor.Value,
                isFilled: true, cornerRadius: style.CornerRadius * size));
        }
        RectangleF content = box.Text.ContentBounds;
        float x = rect.X + (style.PaddingX * size) - content.X;
        float y = rect.Y + (style.PaddingY * size) - content.Y;
        if (style.OutlineColor.HasValue && style.OutlineWidth > 0f)
        {
            //Stroked under the fill, so the outline shows only outside the glyphs
            group.Add(new TextBlockShape(box.Text, x, y, new PathStrokeStyle(2f * style.OutlineWidth * size), style.OutlineColor.Value));
        }
        group.Add(style.TextFill != null
            ? new TextBlockShape(box.Text, x, y, style.TextFill)
            : new TextBlockShape(box.Text, x, y, style.TextColor));
    }
}
