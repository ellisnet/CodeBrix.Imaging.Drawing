using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Fonts;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

/// <summary>
/// A registry of fonts, loaded from files, byte arrays, or streams, that supplies typefaces to
/// <see cref="ImagingSvgAssetLoader"/> for fully managed (no native code) SVG text rendering.
/// Fonts are registered explicitly, for headless determinism, instead of being discovered
/// from the system.
/// </summary>
public sealed class NoSkiaFontRegistry
{
    private readonly object _syncRoot = new object();
    private readonly FontCollection _fontCollection = new FontCollection();
    private readonly List<RegistryEntry> _entries = new List<RegistryEntry>();

    private sealed class RegistryEntry
    {
        public string OverrideFamilyName;
        public FontFamily Family;
        public byte[] Data;
    }

    /// <summary>Gets the number of fonts that have been registered.</summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// Registers a font from a file path (.ttf/.otf), keyed by the family name embedded in the font.
    /// </summary>
    /// <param name="path">The path to the font file.</param>
    /// <returns>The family name the font was registered under.</returns>
    public string RegisterFont(string path) => RegisterFont(path, null);

    /// <summary>
    /// Registers a font from a file path (.ttf/.otf) with an optional explicit family name override.
    /// </summary>
    /// <param name="path">The path to the font file.</param>
    /// <param name="familyNameOverride">
    /// An explicit family name to register the font under, or <c>null</c> to use the family name
    /// embedded in the font. The font's own family name always remains matchable as well.
    /// </param>
    /// <returns>The family name the font was registered under.</returns>
    public string RegisterFont(string path, string familyNameOverride)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        return RegisterFontData(File.ReadAllBytes(path), familyNameOverride);
    }

    /// <summary>
    /// Registers a font from raw font-file bytes, keyed by the family name embedded in the font.
    /// </summary>
    /// <param name="data">The raw font file data.</param>
    /// <returns>The family name the font was registered under.</returns>
    public string RegisterFont(byte[] data) => RegisterFont(data, null);

    /// <summary>
    /// Registers a font from raw font-file bytes with an optional explicit family name override.
    /// </summary>
    /// <param name="data">The raw font file data.</param>
    /// <param name="familyNameOverride">
    /// An explicit family name to register the font under, or <c>null</c> to use the family name
    /// embedded in the font. The font's own family name always remains matchable as well.
    /// </param>
    /// <returns>The family name the font was registered under.</returns>
    public string RegisterFont(byte[] data, string familyNameOverride)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        return RegisterFontData((byte[])data.Clone(), familyNameOverride);
    }

    /// <summary>
    /// Registers a font from a stream of font-file data, keyed by the family name embedded in the font.
    /// </summary>
    /// <param name="stream">The stream containing the font file data.</param>
    /// <returns>The family name the font was registered under.</returns>
    public string RegisterFont(Stream stream) => RegisterFont(stream, null);

    /// <summary>
    /// Registers a font from a stream of font-file data with an optional explicit family name override.
    /// </summary>
    /// <param name="stream">The stream containing the font file data.</param>
    /// <param name="familyNameOverride">
    /// An explicit family name to register the font under, or <c>null</c> to use the family name
    /// embedded in the font. The font's own family name always remains matchable as well.
    /// </param>
    /// <returns>The family name the font was registered under.</returns>
    public string RegisterFont(Stream stream, string familyNameOverride)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            return RegisterFontData(buffer.ToArray(), familyNameOverride);
        }
    }

    /// <summary>
    /// Gets the family names (override names where supplied, otherwise embedded names) of all
    /// registered fonts, in registration order.
    /// </summary>
    /// <returns>The list of registered family names.</returns>
    public IReadOnlyList<string> GetRegisteredFamilyNames()
    {
        lock (_syncRoot)
        {
            var names = new List<string>(_entries.Count);
            foreach (var entry in _entries)
            {
                names.Add(entry.OverrideFamilyName ?? entry.Family.Name);
            }

            return names;
        }
    }

    /// <summary>
    /// Attempts to find a registered font family matching the specified family name. The name is
    /// trimmed and compared case-insensitively against override names first, then against the
    /// family names embedded in the registered fonts. Comma-separated CSS-style family lists
    /// (for example <c>"Arial, sans-serif"</c>) are searched candidate by candidate.
    /// </summary>
    /// <param name="familyName">The family name (or comma-separated family list) to match.</param>
    /// <param name="family">When this method returns <c>true</c>, receives the matched family.</param>
    /// <returns><c>true</c> if a registered family matched; otherwise, <c>false</c>.</returns>
    public bool TryFindFamily(string familyName, out FontFamily family)
    {
        lock (_syncRoot)
        {
            var entry = FindEntry(familyName);
            if (entry is null)
            {
                family = default;
                return false;
            }

            family = entry.Family;
            return true;
        }
    }

    /// <summary>
    /// Gets the font file a family name resolves to, following exactly the rules rendering
    /// follows: the requested name (or comma-separated family list) is matched against the
    /// override names first and the embedded family names second, and when nothing matches
    /// the first registered font is used as the fallback. A consumer that re-emits a
    /// picture into a container with its own font support - a PDF writer embedding the
    /// face, for example - gets back the very bytes the outlines were measured from.
    /// </summary>
    /// <param name="familyName">
    /// The family name (or comma-separated family list) to resolve; <c>null</c> or empty
    /// resolves straight to the fallback.
    /// </param>
    /// <param name="data">
    /// When this method returns <c>true</c>, receives a copy of the registered font file's
    /// bytes.
    /// </param>
    /// <param name="resolvedFamilyName">
    /// When this method returns <c>true</c>, receives the family name embedded in the font
    /// that was resolved - the same name the display list records in a text command's style.
    /// </param>
    /// <returns><c>true</c> when a font was resolved; <c>false</c> when nothing is registered.</returns>
    public bool TryGetFontData(string familyName, out byte[] data, out string resolvedFamilyName)
    {
        lock (_syncRoot)
        {
            var entry = FindEntry(familyName) ?? (_entries.Count > 0 ? _entries[0] : null);
            if (entry is null)
            {
                data = null;
                resolvedFamilyName = null;
                return false;
            }

            data = (byte[])entry.Data.Clone();
            resolvedFamilyName = entry.Family.Name;
            return true;
        }
    }

    private RegistryEntry FindEntry(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return null;
        }

        foreach (var candidate in SplitFamilyCandidates(familyName))
        {
            foreach (var entry in _entries)
            {
                if (entry.OverrideFamilyName is { } overrideName &&
                    string.Equals(overrideName, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            foreach (var entry in _entries)
            {
                if (string.Equals(entry.Family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to get the first registered font family, used as the fallback when no registered
    /// family matches a requested name.
    /// </summary>
    /// <param name="family">When this method returns <c>true</c>, receives the first registered family.</param>
    /// <returns><c>true</c> if at least one font is registered; otherwise, <c>false</c>.</returns>
    public bool TryGetFirstFamily(out FontFamily family)
    {
        lock (_syncRoot)
        {
            if (_entries.Count > 0)
            {
                family = _entries[0].Family;
                return true;
            }
        }

        family = default;
        return false;
    }

    /// <summary>
    /// Gets a snapshot of all registered font families, in registration order.
    /// </summary>
    /// <returns>The list of registered families.</returns>
    public IReadOnlyList<FontFamily> GetFamilies()
    {
        lock (_syncRoot)
        {
            var families = new List<FontFamily>(_entries.Count);
            foreach (var entry in _entries)
            {
                families.Add(entry.Family);
            }

            return families;
        }
    }

    private string RegisterFontData(byte[] data, string familyNameOverride)
    {
        lock (_syncRoot)
        {
            //Every entry point funnels through one buffer, so the registry can hand the
            //  original font file back later (TryGetFontData) as well as render from it
            using var stream = new MemoryStream(data, writable: false);
            var family = _fontCollection.Add(stream);
            return AddEntry(family, familyNameOverride, data);
        }
    }

    private string AddEntry(FontFamily family, string familyNameOverride, byte[] data)
    {
        var overrideName = string.IsNullOrWhiteSpace(familyNameOverride)
            ? null
            : familyNameOverride.Trim();
        _entries.Add(new RegistryEntry { OverrideFamilyName = overrideName, Family = family, Data = data });
        return overrideName ?? family.Name;
    }

    private static IEnumerable<string> SplitFamilyCandidates(string familyName)
    {
        foreach (var part in familyName.Split(','))
        {
            var candidate = part.Trim().Trim('\'', '"').Trim();
            if (candidate.Length > 0)
            {
                yield return candidate;
            }
        }
    }
}
