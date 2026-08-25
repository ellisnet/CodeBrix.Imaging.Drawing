using System;
using System.Collections.Generic;

namespace CodeBrix.Imaging.Drawing.NoSkia.Raster;

/// <summary>
/// Rasterizes a picture-backed tile on demand and keeps the result, so that a pattern which
/// paints thousands of pixels draws its tile once. A tile is rasterized at the resolution
/// the transform in force asks for, and the cache is keyed by that pixel size: the same
/// pattern painted at two different scales keeps a tile for each, and painting it again at
/// a scale already seen reuses one.
/// </summary>
internal sealed class PictureTileCache
{
    /// <summary>
    /// The largest tile this cache rasterizes along either axis. A pattern under an extreme
    /// scale is sampled from a tile of this size rather than allocating without bound.
    /// </summary>
    public const int MaxTileExtent = 4096;

    //A tile per scale, bounded: a pattern under a continuously changing scale would
    //  otherwise keep every resolution it ever painted at
    private const int MaxCachedTiles = 4;

    private readonly Dictionary<(int Width, int Height), DrawingBitmap> _tiles =
        new Dictionary<(int Width, int Height), DrawingBitmap>();

    private readonly List<(int Width, int Height)> _order = new List<(int Width, int Height)>();

    /// <summary>
    /// Gets the tile rasterized at the given pixel size, rasterizing it on the first
    /// request for that size.
    /// </summary>
    /// <param name="picture">The picture the tile draws.</param>
    /// <param name="tileRect">The region of the picture's coordinate space one tile covers.</param>
    /// <param name="width">The tile's width in pixels.</param>
    /// <param name="height">The tile's height in pixels.</param>
    /// <returns>The rasterized tile.</returns>
    public DrawingBitmap GetTile(DrawingPicture picture, DrawingRect tileRect, int width, int height)
    {
        var key = (width, height);
        lock (_tiles)
        {
            if (_tiles.TryGetValue(key, out DrawingBitmap cached)) { return cached; }

            DrawingBitmap tile = Rasterize(picture, tileRect, width, height);

            if (_order.Count >= MaxCachedTiles)
            {
                (int Width, int Height) oldest = _order[0];
                _order.RemoveAt(0);
                if (_tiles.Remove(oldest, out DrawingBitmap evicted)) { evicted.Dispose(); }
            }

            _tiles[key] = tile;
            _order.Add(key);
            return tile;
        }
    }

    private static DrawingBitmap Rasterize(DrawingPicture picture, DrawingRect tileRect, int width, int height)
    {
        var tile = new DrawingBitmap(new DrawingImageInfo(
            width, height, DrawingColorType.Rgba8888, DrawingAlphaType.Premul));

        using var canvas = new DrawingCanvas(tile);
        canvas.Scale(width / tileRect.Width, height / tileRect.Height);
        canvas.Translate(-tileRect.Left, -tileRect.Top);
        canvas.DrawPicture(picture);
        return tile;
    }

    /// <summary>
    /// Chooses the pixel size to rasterize a tile at, from the tile's size in its own
    /// coordinate space and the scale the transform in force applies to it.
    /// </summary>
    /// <param name="tileRect">The region one tile covers.</param>
    /// <param name="scale">The scale the tile is painted at.</param>
    /// <param name="width">The chosen width in pixels.</param>
    /// <param name="height">The chosen height in pixels.</param>
    public static void ChooseTileSize(DrawingRect tileRect, float scale, out int width, out int height)
    {
        if (!(scale > 0) || float.IsInfinity(scale)) { scale = 1f; }

        width = Math.Clamp((int)MathF.Ceiling(tileRect.Width * scale), 1, MaxTileExtent);
        height = Math.Clamp((int)MathF.Ceiling(tileRect.Height * scale), 1, MaxTileExtent);
    }
}
