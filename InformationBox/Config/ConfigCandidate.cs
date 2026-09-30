namespace InformationBox.Config;

/// <summary>
/// Identifies an override file and whether it must be protected as machine-wide configuration.
/// </summary>
internal readonly record struct ConfigCandidate(string Path, bool RequiresProtectedAcl);
