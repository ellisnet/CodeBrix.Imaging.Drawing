using System.Numerics;

namespace CodeBrix.Imaging.Drawing.Rendering;

/// <summary>
/// What an element painter needs to know about the device it draws onto: its pixel size (for
/// offscreen effect bitmaps), the scale from calibrated units to device pixels, and how the
/// scene is turned on the device (the camera's roll as this layer feels it - the direction a
/// scene-fixed shadow turns with).
/// </summary>
internal sealed class PaintContext
{
    internal PaintContext(int width, int height, float scale)
        : this(width, height, scale, Matrix3x2.Identity)
    {
    }

    internal PaintContext(int width, int height, float scale, Matrix3x2 sceneRotation)
    {
        Width = width;
        Height = height;
        Scale = scale;
        SceneRotation = sceneRotation;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal float Scale { get; }

    //A pure rotation (row-vector convention): scene directions to device directions
    internal Matrix3x2 SceneRotation { get; }
}
