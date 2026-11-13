namespace CodeBrix.Imaging.Drawing.Paths;

/// <summary>
/// How <see cref="PathGeometry.CombineWith(PathGeometry, PathBooleanOperation, float)"/> and
/// <see cref="PathGeometry.CombineAll(System.Collections.Generic.IEnumerable{PathGeometry}, PathBooleanOperation, float)"/>
/// combine the areas that geometries fill.
/// </summary>
public enum PathBooleanOperation
{
    /// <summary>Everything any of them fills.</summary>
    Union = 0,

    /// <summary>Only what all of them fill.</summary>
    Intersection = 1,

    /// <summary>What the first fills, less what the others fill.</summary>
    Difference = 2,

    /// <summary>What an odd number of them fill (for two: either one but not both).</summary>
    ExclusiveOr = 3,
}
