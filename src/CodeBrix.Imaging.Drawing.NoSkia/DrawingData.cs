using System;
using System.IO;

namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// An immutable byte buffer, API-compatible with the SkiaSharp <c>SKData</c> type as used
/// for encoded image bytes.
/// </summary>
public sealed class DrawingData : IDisposable
{
    private readonly byte[] _bytes;

    internal DrawingData(byte[] bytes)
    {
        _bytes = bytes ?? Array.Empty<byte>();
    }

    /// <summary>The number of bytes in the buffer.</summary>
    public long Size => _bytes.Length;

    /// <summary>
    /// Returns a copy of the buffer's bytes.
    /// </summary>
    /// <returns>A new array holding the bytes.</returns>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    /// <summary>
    /// Returns a read-only stream over the buffer's bytes.
    /// </summary>
    /// <returns>A new stream positioned at the start of the buffer.</returns>
    public Stream AsStream() => new MemoryStream(_bytes, writable: false);

    /// <summary>
    /// Returns a read-only span over the buffer's bytes.
    /// </summary>
    /// <returns>A span over the bytes.</returns>
    public ReadOnlySpan<byte> AsSpan() => _bytes;

    /// <summary>
    /// Writes the buffer's bytes to a stream.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
    public void SaveTo(Stream destination)
    {
        if (destination == null) { throw new ArgumentNullException(nameof(destination)); }
        destination.Write(_bytes, 0, _bytes.Length);
    }

    /// <summary>
    /// Releases the buffer. The managed implementation holds no unmanaged resources; this
    /// exists for API compatibility with SkiaSharp's disposable data buffers.
    /// </summary>
    public void Dispose()
    {
    }
}
