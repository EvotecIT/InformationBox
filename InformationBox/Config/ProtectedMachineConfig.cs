using System;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace InformationBox.Config;

/// <summary>
/// Determines whether machine configuration may set the app's elevation policy.
/// </summary>
internal static class ProtectedMachineConfig
{
    private const FileSystemRights MutableRights =
        FileSystemRights.WriteData | FileSystemRights.AppendData |
        FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    internal static bool HasProtectedAcl(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var expected = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "InformationBox", "config.json"));
            var actual = Path.GetFullPath(path);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(actual)!;
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(actual) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            return HasPrivilegedOwnerAndWriters(new DirectoryInfo(directory).GetAccessControl()) &&
                   HasPrivilegedOwnerAndWriters(new FileInfo(actual).GetAccessControl());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or
                                   ArgumentException or NotSupportedException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    internal static bool HasPrivilegedOwnerAndWriters(FileSystemSecurity security)
    {
        if (security.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !IsPrivileged(owner))
        {
            return false;
        }

        var privilegedWriterFound = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow || (rule.FileSystemRights & MutableRights) == 0)
            {
                continue;
            }

            if (rule.IdentityReference is not SecurityIdentifier sid || !IsPrivileged(sid))
            {
                return false;
            }

            privilegedWriterFound = true;
        }

        return privilegedWriterFound;
    }

    private static bool IsPrivileged(SecurityIdentifier sid) =>
        sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
        sid.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        sid.Value == "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464"; // TrustedInstaller
}
