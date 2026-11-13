using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Fonts;

namespace CodeBrix.Imaging.Drawing.Text;

/// <summary>
/// The fonts text shapes may use: a small registry of font files the CALLER supplies (by
/// path, bytes or stream), parsed by CodeBrix.Imaging's font support. System fonts are never
/// consulted and there is no system font fallback, so text renders the same on every
/// machine and in both packages. Every <see cref="DrawingSession"/> owns one
/// (<see cref="DrawingSession.Fonts"/>), or several sessions can share one through
/// <see cref="DrawingSessionOptions.Fonts"/>; a <see cref="CodeBrix.Imaging.Drawing.Shapes.TextShape"/>
/// can also be given any registry directly. Thread-safe.
/// <para>
/// Family resolution: a null family name means the FIRST registered font; a named family
/// must be registered (by its own name or the override it was registered under), matched
/// without regard to case. One font file is one face: register each face you need
/// (for example a bold file) under a family name of its own.
/// </para>
/// </summary>
public sealed class FontRegistry
{
    private sealed class Entry
    {
        public string Name;
        public FontFamily Family;
        public FontStyle Style;
    }

    private readonly object _locker = new object();
    private readonly FontCollection _collection = new FontCollection();
    private readonly List<Entry> _entries = new List<Entry>();

    /// <summary>The number of registered fonts.</summary>
    public int Count
    {
        get
        {
            lock (_locker) { return _entries.Count; }
        }
    }

    /// <summary>
    /// Registers a font file (TrueType or OpenType) under the family name it declares.
    /// </summary>
    /// <param name="path">The font file's path. The file is read once and not kept open.</param>
    /// <returns>The family name the font was registered under.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty, or the file is not a readable font.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    public string RegisterFont(string path) => RegisterFont(path, null);

    /// <summary>
    /// Registers a font file (TrueType or OpenType) under a family name of the caller's choosing.
    /// </summary>
    /// <param name="path">The font file's path. The file is read once and not kept open.</param>
    /// <param name="familyNameOverride">The name text shapes use to ask for this font; or <c>null</c> for the name the font declares.</param>
    /// <returns>The family name the font was registered under.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty, or the file is not a readable font.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
    public string RegisterFont(string path, string familyNameOverride)
    {
        if (String.IsNullOrEmpty(path)) { throw new ArgumentException("A font file path is required.", nameof(path)); }
        return RegisterFont(File.ReadAllBytes(path), familyNameOverride);
    }

    /// <summary>
    /// Registers a font (TrueType or OpenType) from its bytes, under the family name it declares.
    /// </summary>
    /// <param name="data">The font file's bytes; copied, so the caller may reuse the array.</param>
    /// <returns>The family name the font was registered under.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bytes are not a readable font.</exception>
    public string RegisterFont(byte[] data) => RegisterFont(data, null);

    /// <summary>
    /// Registers a font (TrueType or OpenType) from its bytes, under a family name of the caller's choosing.
    /// </summary>
    /// <param name="data">The font file's bytes; copied, so the caller may reuse the array.</param>
    /// <param name="familyNameOverride">The name text shapes use to ask for this font; or <c>null</c> for the name the font declares.</param>
    /// <returns>The family name the font was registered under.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the bytes are not a readable font.</exception>
    public string RegisterFont(byte[] data, string familyNameOverride)
    {
        if (data == null) { throw new ArgumentNullException(nameof(data)); }

        lock (_locker)
        {
            FontFamily family;
            FontDescription description;
            try
            {
                using var stream = new MemoryStream((byte[])data.Clone(), writable: false);
                family = _collection.Add(stream, out description);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                throw new ArgumentException("The data could not be read as a font.", nameof(data), e);
            }

            string name = String.IsNullOrWhiteSpace(familyNameOverride) ? family.Name : familyNameOverride.Trim();
            _entries.Add(new Entry { Name = name, Family = family, Style = description.Style });
            return name;
        }
    }

    /// <summary>
    /// Registers a font (TrueType or OpenType) read from a stream, under the family name it declares.
    /// </summary>
    /// <param name="stream">The stream to read the font from (read to its end; not disposed).</param>
    /// <returns>The family name the font was registered under.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the data is not a readable font.</exception>
    public string RegisterFont(Stream stream) => RegisterFont(stream, null);

    /// <summary>
    /// Registers a font (TrueType or OpenType) read from a stream, under a family name of the caller's choosing.
    /// </summary>
    /// <param name="stream">The stream to read the font from (read to its end; not disposed).</param>
    /// <param name="familyNameOverride">The name text shapes use to ask for this font; or <c>null</c> for the name the font declares.</param>
    /// <returns>The family name the font was registered under.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the data is not a readable font.</exception>
    public string RegisterFont(Stream stream, string familyNameOverride)
    {
        if (stream == null) { throw new ArgumentNullException(nameof(stream)); }
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return RegisterFont(copy.ToArray(), familyNameOverride);
    }

    /// <summary>
    /// Lists the family names fonts were registered under, in registration order.
    /// </summary>
    /// <returns>A new list of names (the first one is the default family).</returns>
    public IReadOnlyList<string> GetRegisteredFamilyNames()
    {
        lock (_locker)
        {
            var names = new List<string>(_entries.Count);
            foreach (Entry entry in _entries) { names.Add(entry.Name); }
            return names;
        }
    }

    /// <summary>
    /// Indicates whether a family name resolves to a registered font.
    /// </summary>
    /// <param name="familyName">The family name (case-insensitive); or <c>null</c> for "the default font".</param>
    /// <returns><c>true</c> when text in that family can be drawn.</returns>
    public bool Contains(string familyName) => TryResolve(familyName, out _);

    /// <summary>
    /// Measures the advance width of a line of text - the width a <see cref="CodeBrix.Imaging.Drawing.Shapes.TextShape"/>
    /// with the same values would report as its <c>Width</c> - without creating a shape.
    /// </summary>
    /// <param name="text">The text; null or empty measures zero.</param>
    /// <param name="size">The font size (the em height), in calibrated drawing units; must be positive.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>.</param>
    /// <param name="familyName">The family; or <c>null</c> for the first registered font.</param>
    /// <returns>The advance width, in calibrated drawing units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive or <paramref name="tracking"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no font is registered at all.</exception>
    public float MeasureText(string text, float size, float tracking = 0f, string familyName = null)
    {
        TextOutline outline = TextOutline.Build(this, text, size, familyName, tracking);
        return outline.Advance;
    }

    /// <summary>
    /// Measures the advance width of a line of text with the font's kerning switched on or off -
    /// the width a <see cref="CodeBrix.Imaging.Drawing.Shapes.TextShape"/> created with the same
    /// values (and the same <paramref name="kerning"/>) would report.
    /// </summary>
    /// <param name="text">The text; null or empty measures zero.</param>
    /// <param name="size">The font size (the em height), in calibrated drawing units; must be positive.</param>
    /// <param name="tracking">Extra space after every character but the last, as a fraction of <paramref name="size"/>.</param>
    /// <param name="familyName">The family; or <c>null</c> for the first registered font.</param>
    /// <param name="kerning"><c>true</c> to apply the font's kerning (what the other overload does).</param>
    /// <returns>The advance width, in calibrated drawing units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> is not positive or <paramref name="tracking"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="familyName"/> is not registered.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no font is registered at all.</exception>
    public float MeasureText(string text, float size, float tracking, string familyName, bool kerning)
    {
        TextOutline outline = TextOutline.Build(this, text, size, familyName, tracking, kerning);
        return outline.Advance;
    }

    //Resolves a family name to a font of the given size, by the documented rule
    internal Font CreateFont(string familyName, float size, out string resolvedName)
    {
        lock (_locker)
        {
            if (_entries.Count < 1)
            {
                throw new InvalidOperationException("No font is registered: register a font file before drawing text (system fonts are never used).");
            }
            if (!TryResolve(familyName, out Entry entry))
            {
                throw new ArgumentException($"No font is registered under the family name '{familyName}'.", nameof(familyName));
            }
            resolvedName = entry.Name;
            return entry.Family.CreateFont(size, entry.Style);
        }
    }

    private bool TryResolve(string familyName, out Entry entry)
    {
        lock (_locker)
        {
            entry = null;
            if (_entries.Count < 1) { return false; }
            if (familyName == null)
            {
                entry = _entries[0];
                return true;
            }

            string trimmed = familyName.Trim();
            foreach (Entry candidate in _entries)
            {
                if (String.Equals(candidate.Name, trimmed, StringComparison.OrdinalIgnoreCase)
                    || String.Equals(candidate.Family.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
