namespace CodeBrix.Imaging.Drawing.NoSkia;

/// <summary>
/// Converts a run of text into the filled outline that draws it. This is the only seam
/// between the core drawing assembly - which owns the display list and knows nothing about
/// fonts - and whatever component supplies typefaces (for SVG, the font registry in the
/// SVG assembly). A producer that records text commands supplies the outliner those
/// commands carry, so <c>GetOutline</c> keeps working for as long as the picture is alive.
/// </summary>
public interface IDrawingTextOutliner
{
    /// <summary>
    /// Builds the filled outline of a run of text placed at an origin.
    /// </summary>
    /// <param name="text">The text to outline.</param>
    /// <param name="style">The typeface selection the run was recorded with.</param>
    /// <param name="origin">
    /// The run's origin - the baseline position that <paramref name="style"/>'s alignment
    /// is measured from.
    /// </param>
    /// <returns>
    /// The outline, in the same coordinate space as <paramref name="origin"/>; or
    /// <c>null</c> when no outline could be produced (no font, or empty text).
    /// </returns>
    DrawingPath GetOutline(string text, DrawingTextStyle style, DrawingPoint origin);
}
