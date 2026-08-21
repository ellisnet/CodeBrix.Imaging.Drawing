using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Imaging.Fonts;

namespace CodeBrix.Imaging.Drawing.NoSkia.Svg.Rendering;

/// <summary>
/// A registry of fonts, loaded from files, byte arrays, or streams, that supplies typefaces to
/// <see cref="ImagingSvgAssetLoader"/> for fully managed (no native code) SVG text rendering.
/// Mirrors the role that a custom typeface provider plays in the Skia-based stack: fonts are
/// registered explicitly for headless determinism instead of being discovered from the system.
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

        lock (_syncRoot)
        {
            var family = _fontCollection.Add(path);
            return AddEntry(family, familyNameOverride);
        }
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

        using (var stream = new MemoryStream(data, writable: false))
        {
            return RegisterFont(stream, familyNameOverride);
        }
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

        lock (_syncRoot)
        {
            var family = _fontCollection.Add(stream);
            return AddEntry(family, familyNameOverride);
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
        family = default;
        if (string.IsNullOrWhiteSpace(familyName))
        {
            return false;
        }

        foreach (var candidate in SplitFamilyCandidates(familyName))
        {
            lock (_syncRoot)
            {
                foreach (var entry in _entries)
                {
                    if (entry.OverrideFamilyName is { } overrideName &&
                        string.Equals(overrideName, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        family = entry.Family;
                        return true;
                    }
                }

                foreach (var entry in _entries)
                {
                    if (string.Equals(entry.Family.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        family = entry.Family;
                        return true;
                    }
                }
            }
        }

        return false;
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

    private string AddEntry(FontFamily family, string familyNameOverride)
    {
        var overrideName = string.IsNullOrWhiteSpace(familyNameOverride)
            ? null
            : familyNameOverride.Trim();
        _entries.Add(new RegistryEntry { OverrideFamilyName = overrideName, Family = family });
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
