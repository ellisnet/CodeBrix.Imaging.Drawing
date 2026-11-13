namespace AnimationDemoApp.Demo;

/// <summary>The demo's frame: every scene fills one landscape frame of this size.</summary>
internal static class DemoFrame
{
    /// <summary>The frame's width, pixels.</summary>
    public const int Width = 1280;

    /// <summary>The frame's height, pixels.</summary>
    public const int Height = 720;

    /// <summary>The number of bytes in one raw RGBA frame.</summary>
    public const int Bytes = Width * Height * 4;
}
