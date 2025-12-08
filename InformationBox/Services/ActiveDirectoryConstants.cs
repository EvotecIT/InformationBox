namespace InformationBox.Services;

/// <summary>
/// Well-known Active Directory userAccountControl flag constants.
/// </summary>
public static class ActiveDirectoryConstants
{
    /// <summary>
    /// Password never expires flag (0x10000) on userAccountControl.
    /// </summary>
    public const int DontExpirePassword = 0x10000;
}
