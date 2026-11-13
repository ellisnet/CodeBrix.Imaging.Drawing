namespace AnimationDemoApp.Demo;

/// <summary>
/// Which drawing package a scene can be drawn with. Most features exist in both packages; a scene that shows a
/// feature of only one (for example SVG, which only the NoSkia package has) is shown there and replaced by a
/// plain placeholder frame in the other.
/// </summary>
internal enum SceneAvailability
{
    /// <summary>The scene is drawn by both packages.</summary>
    BothPackages = 0,

    /// <summary>The scene is drawn only by CodeBrix.Imaging.Drawing (SkiaSharp).</summary>
    SkiaOnly = 1,

    /// <summary>The scene is drawn only by CodeBrix.Imaging.Drawing.NoSkia.</summary>
    NoSkiaOnly = 2,
}
