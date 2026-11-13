namespace CodeBrix.Imaging.Drawing.Layout;

/// <summary>The rectangle a <see cref="LayoutPosition"/> is measured against.</summary>
public enum LayoutReference
{
    /// <summary>The whole frame (the calibrated drawing space).</summary>
    Frame = 0,

    /// <summary>The safe rectangle of the layout's safe-area guide (the whole frame when it has none).</summary>
    SafeArea = 1,
}
