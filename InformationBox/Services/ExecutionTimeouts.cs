using System;

namespace InformationBox.Services;

/// <summary>
/// Central place to tune execution timeouts used across the app.
/// </summary>
public static class ExecutionTimeouts
{
    /// <summary>Default PowerShell command timeout.</summary>
    public static readonly TimeSpan CommandDefault = TimeSpan.FromMinutes(5);

    /// <summary>Grace period to finish reading stdout/stderr after exit.</summary>
    public static readonly TimeSpan StreamRead = TimeSpan.FromSeconds(5);

    /// <summary>LDAP client timeout for AD queries.</summary>
    public static readonly TimeSpan LdapClient = TimeSpan.FromSeconds(5);

    /// <summary>Delay to allow elevated temp files to flush before readback.</summary>
    public static readonly TimeSpan TempFileFlushDelay = TimeSpan.FromMilliseconds(100);
}
