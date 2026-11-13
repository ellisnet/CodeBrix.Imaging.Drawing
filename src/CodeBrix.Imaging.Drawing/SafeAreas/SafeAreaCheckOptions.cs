namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// Optional settings for <see cref="DrawingSession.CheckSafeArea(SafeAreaGuide, Size, SafeAreaCheckOptions)"/>.
/// Every setting has a default, so an instance is only needed to change one. There is no time
/// setting: a check always looks at the session AS IT STANDS at its <see cref="DrawingSession.Time"/>
/// - the moment that decides which elements exist, where animated ones are, and which of the
/// guide's time rules apply. To check another moment, set the session's Time first.
/// </summary>
public sealed class SafeAreaCheckOptions
{
    /// <summary>
    /// The video's total length, in seconds - needed for "final N seconds" rules such as
    /// <see cref="SafeAreaGuides.YouTubeEndScreen"/>; or <c>null</c> (the default) when
    /// unknown, in which case such rules count as applying.
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// When <c>true</c>, marked elements whose bounds are unknown (custom shapes that do not
    /// override <c>GetBounds</c>) are listed as <see cref="SafeAreaFindingKind.CouldNotCheck"/>
    /// findings. The default, <c>false</c>, skips them silently.
    /// </summary>
    public bool ReportUncheckedElements { get; set; }

    /// <summary>
    /// When <c>true</c>, every element on every layer is checked, whether or not it or its
    /// layer is marked. The default, <c>false</c>, checks only elements whose own
    /// <c>IsSafeAreaChecked</c> flag or whose layer's flag is set.
    /// </summary>
    public bool CheckAllElements { get; set; }
}
