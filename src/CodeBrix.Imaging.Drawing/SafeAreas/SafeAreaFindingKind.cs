namespace CodeBrix.Imaging.Drawing.SafeAreas;

/// <summary>
/// What a <see cref="SafeAreaFinding"/> reports.
/// </summary>
public enum SafeAreaFindingKind
{
    /// <summary>The element reaches into an unsafe zone; the finding says where.</summary>
    Overlap = 0,

    /// <summary>
    /// The element's bounds are unknown, so it could not be checked (listed only when
    /// <see cref="SafeAreaCheckOptions.ReportUncheckedElements"/> is set).
    /// </summary>
    CouldNotCheck = 1,
}
