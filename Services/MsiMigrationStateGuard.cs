namespace WinThunar.Services;

/// <summary>Keep startup from creating competing settings after an interrupted MSI migration.</summary>
public static class MsiMigrationStateGuard
{
    public static string? GetStartupBlockReason(string? localAppData = null)
    {
        var holdingPath = Path.Combine(localAppData ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinThunar.MsiMigrationState");
        try
        {
            _ = File.GetAttributes(holdingPath);
            return "Windows Installer is migrating WinThunar, or a migration was interrupted. " +
                "Wait for the installer to finish before opening WinThunar. If it has stopped, " +
                $"your preserved state is at:\n\n{holdingPath}\n\n" +
                "Keep this folder. Follow the MSI migration recovery instructions before restarting.";
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return $"WinThunar cannot check its installer migration state at:\n\n{holdingPath}\n\n" +
                "Check access to this location before restarting WinThunar.";
        }
    }
}
