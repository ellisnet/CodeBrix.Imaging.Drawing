using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// A safe-area guide: the parts of a video frame that a platform's interface (buttons,
/// captions, headers), a crop or a broadcast standard may hide, so content that matters
/// should stay out of them. A guide is DATA - a name, edge <see cref="Margins"/> as fractions
/// of the frame, optional <see cref="Crops"/>, an optional <see cref="TimeRule"/>, and a
/// <see cref="Source"/> stating where its numbers came from - so it scales to any frame size.
/// <see cref="SafeAreaGuides"/> holds built-in presets (all ADVISORY: platforms change their
/// interfaces without notice); create your own with the constructor and merge several with
/// <see cref="Combine(string, SafeAreaGuide[])"/>. Guides are immutable.
/// <para>
/// Guides are used two ways, both opt-in: <see cref="DrawingSession.CheckSafeArea(SafeAreaGuide, Size, SafeAreaCheckOptions)"/>
/// reports marked elements that reach into an unsafe zone, and a <see cref="SafeAreaOverlay"/>
/// draws the unsafe zones as a translucent mask on a preview.
/// </para>
/// </summary>
public sealed class SafeAreaGuide
{
    /// <summary>The zone name of a margin that covers the whole frame (see <see cref="Margins"/>).</summary>
    public const string WholeFrameZoneName = "whole frame";

    private readonly SafeAreaCrop[] _crops;
    private readonly SafeAreaGuide[] _parts;

    /// <summary>The guide's name, reported with every zone it produces.</summary>
    public string Name { get; }

    /// <summary>
    /// Where the guide's numbers came from and how reliable they are - for the built-in
    /// presets, the published standard or the third-party reports they were taken from.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The unsafe edge bands. Margins whose opposite edges together reach the whole frame
    /// (top + bottom = 1, or left + right = 1) make the entire frame one
    /// <see cref="WholeFrameZoneName"/> zone - the way to say "nothing should be here".
    /// </summary>
    public SafeAreaMargins Margins { get; }

    /// <summary>The crops whose cut-away areas are unsafe; empty when there are none.</summary>
    public IReadOnlyList<SafeAreaCrop> Crops => _crops;

    /// <summary>When the guide applies; or <c>null</c> when it applies at every moment.</summary>
    public SafeAreaTimeRule TimeRule { get; }

    /// <summary>
    /// For a guide made by <see cref="Combine(string, SafeAreaGuide[])"/>, the guides it
    /// combines (each keeps its own margins, crops and time rule); empty for a plain guide.
    /// </summary>
    public IReadOnlyList<SafeAreaGuide> Parts => _parts;

    /// <summary>
    /// Creates a guide.
    /// </summary>
    /// <param name="name">The guide's name.</param>
    /// <param name="margins">The unsafe edge bands, as fractions of the frame.</param>
    /// <param name="source">Where the numbers came from; or <c>null</c> for a guide of the caller's own.</param>
    /// <param name="crops">Crops whose cut-away areas are unsafe; or <c>null</c> for none.</param>
    /// <param name="timeRule">When the guide applies; or <c>null</c> for always.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace, or <paramref name="crops"/> contains null.</exception>
    public SafeAreaGuide(string name, SafeAreaMargins margins, string source = null,
        IReadOnlyList<SafeAreaCrop> crops = null, SafeAreaTimeRule timeRule = null)
    {
        if (String.IsNullOrWhiteSpace(name)) { throw new ArgumentException("A guide name is required.", nameof(name)); }
        Name = name.Trim();
        Margins = margins;
        Source = source ?? String.Empty;
        TimeRule = timeRule;
        _parts = Array.Empty<SafeAreaGuide>();

        if (crops == null)
        {
            _crops = Array.Empty<SafeAreaCrop>();
        }
        else
        {
            _crops = new SafeAreaCrop[crops.Count];
            for (int i = 0; i < crops.Count; i++)
            {
                _crops[i] = crops[i] ?? throw new ArgumentException("The crops must not contain null.", nameof(crops));
            }
        }
    }

    private SafeAreaGuide(string name, SafeAreaGuide[] parts)
    {
        Name = name;
        _parts = parts;
        _crops = Array.Empty<SafeAreaCrop>();
        var sources = new List<string>();
        foreach (SafeAreaGuide part in parts)
        {
            if (part.Source.Length > 0) { sources.Add(part.Name + ": " + part.Source); }
        }
        Source = String.Join(Environment.NewLine, sources);
    }

    /// <summary>
    /// Combines guides into one whose unsafe area is the UNION of theirs - for a video that
    /// must work on several platforms at once. Each part keeps its own time rule, and every
    /// zone it produces is reported under the part's own name.
    /// </summary>
    /// <param name="name">The combined guide's name.</param>
    /// <param name="guides">The guides to combine; at least one.</param>
    /// <returns>The combined guide.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace, or no guides (or a null guide) are given.</exception>
    public static SafeAreaGuide Combine(string name, params SafeAreaGuide[] guides)
    {
        if (String.IsNullOrWhiteSpace(name)) { throw new ArgumentException("A guide name is required.", nameof(name)); }
        if (guides == null || guides.Length < 1) { throw new ArgumentException("At least one guide is required.", nameof(guides)); }

        var parts = new List<SafeAreaGuide>();
        foreach (SafeAreaGuide guide in guides)
        {
            if (guide == null) { throw new ArgumentException("The guides must not contain null.", nameof(guides)); }
            //A combined guide contributes its own parts, so combining never nests
            if (guide._parts.Length > 0) { parts.AddRange(guide._parts); } else { parts.Add(guide); }
        }
        return new SafeAreaGuide(name.Trim(), parts.ToArray());
    }

    /// <summary>
    /// Lays the guide out on a frame: every unsafe zone that applies at the given moment.
    /// </summary>
    /// <param name="frameSize">The frame size, in pixels.</param>
    /// <param name="seconds">The moment of the video; or <c>null</c> when unknown (every time rule then applies).</param>
    /// <param name="duration">The video's length; or <c>null</c> when unknown.</param>
    /// <returns>The zones, in frame pixels; empty when nothing is unsafe at that moment.</returns>
    public IReadOnlyList<SafeAreaZone> GetUnsafeZones(SizeF frameSize, double? seconds = null, double? duration = null)
    {
        var zones = new List<SafeAreaZone>();
        if (frameSize.Width <= 0 || frameSize.Height <= 0) { return zones; }

        if (_parts.Length > 0)
        {
            foreach (SafeAreaGuide part in _parts)
            {
                zones.AddRange(part.GetUnsafeZones(frameSize, seconds, duration));
            }
            return zones;
        }

        if (TimeRule != null && !TimeRule.AppliesAt(seconds, duration)) { return zones; }

        float width = frameSize.Width;
        float height = frameSize.Height;
        SafeAreaMargins m = Margins;
        if (m.Top + m.Bottom >= 1f || m.Left + m.Right >= 1f)
        {
            zones.Add(new SafeAreaZone(Name, WholeFrameZoneName, new RectangleF(0, 0, width, height)));
            return zones;
        }

        AddZone(zones, "top margin", 0, 0, width, m.Top * height);
        AddZone(zones, "bottom margin", 0, height - (m.Bottom * height), width, m.Bottom * height);
        AddZone(zones, "left margin", 0, 0, m.Left * width, height);
        AddZone(zones, "right margin", width - (m.Right * width), 0, m.Right * width, height);

        foreach (SafeAreaCrop crop in _crops)
        {
            RectangleF kept = crop.GetKeptRectangle(frameSize);
            AddZone(zones, crop.Name, 0, 0, width, kept.Top);
            AddZone(zones, crop.Name, 0, kept.Bottom, width, height - kept.Bottom);
            AddZone(zones, crop.Name, 0, kept.Top, kept.Left, kept.Height);
            AddZone(zones, crop.Name, kept.Right, kept.Top, width - kept.Right, kept.Height);
        }
        return zones;
    }

    /// <summary>
    /// The SAFE rectangle of a frame: the largest axis-aligned area outside every unsafe zone
    /// that applies at the given moment - the frame inset by the margins, cut to each crop's
    /// kept rectangle, and (for a combined guide) the intersection of every part's safe
    /// rectangle. The whole frame when nothing applies; <see cref="RectangleF.Empty"/> when a
    /// zone covers the whole frame. Use it to place content (see
    /// <see cref="CodeBrix.Imaging.Drawing.Layout.DrawingLayout"/>).
    /// </summary>
    /// <param name="frameSize">The frame size, in pixels.</param>
    /// <param name="seconds">The moment of the video; or <c>null</c> when unknown (every time rule then applies).</param>
    /// <param name="duration">The video's length; or <c>null</c> when unknown.</param>
    /// <returns>The safe rectangle, in frame pixels.</returns>
    public RectangleF GetSafeRectangle(SizeF frameSize, double? seconds = null, double? duration = null)
    {
        if (frameSize.Width <= 0 || frameSize.Height <= 0) { return RectangleF.Empty; }
        var safe = new RectangleF(0, 0, frameSize.Width, frameSize.Height);

        if (_parts.Length > 0)
        {
            foreach (SafeAreaGuide part in _parts)
            {
                safe = Intersect(safe, part.GetSafeRectangle(frameSize, seconds, duration));
            }
            return safe;
        }

        if (TimeRule != null && !TimeRule.AppliesAt(seconds, duration)) { return safe; }

        float width = frameSize.Width;
        float height = frameSize.Height;
        SafeAreaMargins m = Margins;
        if (m.Top + m.Bottom >= 1f || m.Left + m.Right >= 1f) { return RectangleF.Empty; }

        float left = m.Left * width;
        float top = m.Top * height;
        safe = new RectangleF(left, top, (width - (m.Right * width)) - left, (height - (m.Bottom * height)) - top);
        foreach (SafeAreaCrop crop in _crops)
        {
            safe = Intersect(safe, crop.GetKeptRectangle(frameSize));
        }
        return safe;
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    private static RectangleF Intersect(RectangleF a, RectangleF b)
    {
        float left = Math.Max(a.Left, b.Left);
        float top = Math.Max(a.Top, b.Top);
        float right = Math.Min(a.Right, b.Right);
        float bottom = Math.Min(a.Bottom, b.Bottom);
        return right > left && bottom > top ? new RectangleF(left, top, right - left, bottom - top) : RectangleF.Empty;
    }

    private void AddZone(List<SafeAreaZone> zones, string zoneName, float x, float y, float width, float height)
    {
        //A sliver below a hundredth of a pixel is rounding noise from a crop that removes nothing
        if (width < 0.01f || height < 0.01f) { return; }
        zones.Add(new SafeAreaZone(Name, zoneName, new RectangleF(x, y, width, height)));
    }
}
