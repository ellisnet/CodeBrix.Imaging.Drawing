using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.Images;

/// <summary>
/// The frames of a sprite animation, in order, for a
/// <see cref="CodeBrix.Imaging.Drawing.Shapes.SpriteShape"/>: cut from one sheet image as a
/// grid (<see cref="FromGrid(ImageSource, int, int, int)"/>) or as listed rectangles
/// (<see cref="FromRectangles(ImageSource, IReadOnlyList{Rectangle})"/>), or given as separate
/// pictures. Cutting COPIES each cell into a frame of its own, so neighbouring cells can never
/// bleed into each other however a frame is scaled or sampled.
/// <para>
/// OWNERSHIP. A sheet made by cutting owns the frames it cut and disposes them with itself (the
/// sheet image you cut from stays yours - dispose it when you no longer need it). A sheet made
/// from a list of frames only refers to them: they stay yours. Keep a sheet alive while any
/// sprite using it may still be rendered.
/// </para>
/// </summary>
public sealed class SpriteSheet : IDisposable
{
    private readonly ImageSource[] _frames;
    private readonly bool _ownsFrames;
    private bool _disposed;

    /// <summary>Makes a sheet of separate pictures (not owned: they stay the caller's to dispose).</summary>
    /// <param name="frames">The frames, in order; at least one.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="frames"/> or an entry is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there are no frames.</exception>
    public SpriteSheet(IReadOnlyList<ImageSource> frames)
        : this(Copy(frames), false)
    {
    }

    private SpriteSheet(ImageSource[] frames, bool ownsFrames)
    {
        _frames = frames;
        _ownsFrames = ownsFrames;
    }

    /// <summary>The number of frames.</summary>
    public int FrameCount => _frames.Length;

    /// <summary>Indicates whether the sheet disposes its frames (true for a sheet that cut them).</summary>
    public bool OwnsFrames => _ownsFrames;

    /// <summary>
    /// Indicates whether <see cref="Dispose"/> has been called. A disposed sheet that owned its
    /// frames draws nothing (its frames are disposed); a sheet of caller frames keeps drawing them.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>Gets a frame.</summary>
    /// <param name="index">The frame's index, 0 to <see cref="FrameCount"/> - 1.</param>
    /// <returns>The frame's picture.</returns>
    public ImageSource GetFrame(int index) => _frames[index];

    /// <summary>
    /// Cuts a sheet image into a grid of equal cells, read left to right then top to bottom.
    /// The cell size is the image size divided by the columns and rows (any remainder pixels at
    /// the right and bottom are not used).
    /// </summary>
    /// <param name="image">The sheet image (copied from; it stays the caller's).</param>
    /// <param name="columns">The number of cells across.</param>
    /// <param name="rows">The number of cells down.</param>
    /// <param name="frameCount">How many cells are frames (the last row may be partly empty); 0 (the default) for all of them.</param>
    /// <returns>The sheet, owning its frames.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the grid does not fit the image or the frame count is out of range.</exception>
    public static SpriteSheet FromGrid(ImageSource image, int columns, int rows, int frameCount = 0)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        if (columns < 1 || columns > image.Width) { throw new ArgumentOutOfRangeException(nameof(columns)); }
        if (rows < 1 || rows > image.Height) { throw new ArgumentOutOfRangeException(nameof(rows)); }
        int cells = columns * rows;
        if (frameCount < 0 || frameCount > cells) { throw new ArgumentOutOfRangeException(nameof(frameCount)); }
        int count = frameCount == 0 ? cells : frameCount;
        int cellWidth = image.Width / columns, cellHeight = image.Height / rows;
        var rectangles = new Rectangle[count];
        for (int i = 0; i < count; i++)
        {
            rectangles[i] = new Rectangle((i % columns) * cellWidth, (i / columns) * cellHeight, cellWidth, cellHeight);
        }
        return FromRectangles(image, rectangles);
    }

    /// <summary>Cuts listed rectangles out of a sheet image, in order.</summary>
    /// <param name="image">The sheet image (copied from; it stays the caller's).</param>
    /// <param name="cells">The rectangles, in pixels; each must lie inside the image.</param>
    /// <returns>The sheet, owning its frames.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when there are no rectangles.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a rectangle is empty or reaches outside the image.</exception>
    public static SpriteSheet FromRectangles(ImageSource image, IReadOnlyList<Rectangle> cells)
    {
        if (image == null) { throw new ArgumentNullException(nameof(image)); }
        if (cells == null) { throw new ArgumentNullException(nameof(cells)); }
        if (cells.Count < 1) { throw new ArgumentException("A sprite sheet needs at least one frame.", nameof(cells)); }
        var frames = new ImageSource[cells.Count];
        try
        {
            for (int i = 0; i < frames.Length; i++) { frames[i] = image.Crop(cells[i]); }
        }
        catch
        {
            foreach (ImageSource frame in frames) { frame?.Dispose(); }
            throw;
        }
        return new SpriteSheet(frames, true);
    }

    /// <summary>Disposes the frames the sheet owns (a sheet of caller frames releases nothing).</summary>
    public void Dispose()
    {
        _disposed = true;
        if (!_ownsFrames) { return; }
        foreach (ImageSource frame in _frames) { frame.Dispose(); }
    }

    private static ImageSource[] Copy(IReadOnlyList<ImageSource> frames)
    {
        if (frames == null) { throw new ArgumentNullException(nameof(frames)); }
        if (frames.Count < 1) { throw new ArgumentException("A sprite sheet needs at least one frame.", nameof(frames)); }
        var copy = new ImageSource[frames.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = frames[i] ?? throw new ArgumentNullException(nameof(frames), "A frame is null.");
        }
        return copy;
    }
}
