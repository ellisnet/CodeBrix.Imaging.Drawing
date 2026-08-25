namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// The typeface selection a text command was recorded with: the family that was actually
/// resolved, the family that was asked for (when the producer still knows it), and the
/// style axes and size that go with it. A style is a value: two styles with the same
/// values are equal.
/// </summary>
/// <param name="FamilyName">
/// The family name the producer resolved the run to - the font the outline is measured and
/// drawn from. May be <c>null</c> when no font was resolved.
/// </param>
/// <param name="RequestedFamilyName">
/// The family name the document asked for, when the producer still knows it; <c>null</c>
/// when that information is not available, in which case only
/// <paramref name="FamilyName"/> is meaningful.
/// </param>
/// <param name="Weight">The requested weight.</param>
/// <param name="Width">The requested width (stretch).</param>
/// <param name="Slant">The requested slant.</param>
/// <param name="Size">The em size, in the coordinate space the command's origin is expressed in.</param>
/// <param name="Align">How the run is positioned horizontally relative to its origin.</param>
public sealed record DrawingTextStyle(
    string FamilyName,
    string RequestedFamilyName,
    DrawingFontWeight Weight,
    DrawingFontWidth Width,
    DrawingFontSlant Slant,
    float Size,
    DrawingTextAlign Align);
