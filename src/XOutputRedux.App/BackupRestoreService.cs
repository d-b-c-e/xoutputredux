using System.IO;
using System.IO.Compression;

namespace XOutputRedux.App;

/// <summary>
/// Service for backing up and restoring application settings.
/// </summary>
public static class BackupRestoreService
{
    private static string AppDataDir => AppPaths.BaseDirectory;

    private static readonly string[] SettingsFiles =
    {
        "app-settings.json",
        "device-settings.json",
        "games.json"
    };

    /// <summary>
    /// Gets the default backup filename with timestamp.
    /// </summary>
    public static string GetDefaultBackupFilename()
    {
        return $"XOutputRedux-Backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.xorbackup";
    }

    /// <summary>
    /// Creates a backup of all settings and profiles.
    /// </summary>
    /// <param name="outputPath">Path for the output .xorbackup file</param>
    /// <returns>Success status and message</returns>
    public static async Task<(bool Success, string Message)> CreateBackupAsync(string outputPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"XOutputRedux-Backup-{Guid.NewGuid():N}");

        try
        {
            AppLogger.Info($"Creating backup to: {outputPath}");

            // Create temp directory
            Directory.CreateDirectory(tempDir);

            int fileCount = 0;

            // Copy settings files
            foreach (var fileName in SettingsFiles)
            {
                var sourcePath = Path.Combine(AppDataDir, fileName);
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, Path.Combine(tempDir, fileName));
                    fileCount++;
                }
            }

            // Copy profiles directory
            var profilesSource = Path.Combine(AppDataDir, "Profiles");
            if (Directory.Exists(profilesSource))
            {
                var profilesDest = Path.Combine(tempDir, "Profiles");
                Directory.CreateDirectory(profilesDest);

                foreach (var file in Directory.GetFiles(profilesSource, "*.json"))
                {
                    File.Copy(file, Path.Combine(profilesDest, Path.GetFileName(file)));
                    fileCount++;
                }
            }

            if (fileCount == 0)
            {
                return (false, "No settings or profiles found to backup");
            }

            // Delete existing backup file if it exists
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            // Create ZIP file
            await Task.Run(() => ZipFile.CreateFromDirectory(tempDir, outputPath, CompressionLevel.Optimal, false));

            AppLogger.Info($"Backup created successfully: {fileCount} files");
            return (true, $"Backup created: {Path.GetFileName(outputPath)} ({fileCount} files)");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to create backup", ex);
            return (false, $"Backup failed: {ex.Message}");
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Restores settings and profiles from a backup file.
    /// </summary>
    /// <param name="zipPath">Path to the .xorbackup file</param>
    /// <returns>Success status and message</returns>
    public static async Task<(bool Success, string Message)> RestoreBackupAsync(string zipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"XOutputRedux-Restore-{Guid.NewGuid():N}");

        try
        {
            AppLogger.Info($"Restoring backup from: {zipPath}");

            if (!File.Exists(zipPath))
            {
                return (false, "Backup file not found");
            }

            // Clean and create temp directory
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }

            // Extract ZIP to temp
            await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir));

            // Find the backup root (handles both flat and nested structures)
            var backupDir = FindBackupRoot(tempDir);
            if (backupDir == null)
            {
                return (false, "Invalid backup file: no settings found");
            }

            // Ensure app data directory exists
            Directory.CreateDirectory(AppDataDir);

            int restoredCount = 0;

            // Restore settings files
            foreach (var fileName in SettingsFiles)
            {
                var sourcePath = Path.Combine(backupDir, fileName);
                if (File.Exists(sourcePath))
                {
                    var destPath = Path.Combine(AppDataDir, fileName);
                    File.Copy(sourcePath, destPath, overwrite: true);
                    restoredCount++;
                }
            }

            // Restore profiles
            var profilesSource = Path.Combine(backupDir, "Profiles");
            if (Directory.Exists(profilesSource))
            {
                var profilesDest = Path.Combine(AppDataDir, "Profiles");
                Directory.CreateDirectory(profilesDest);

                foreach (var file in Directory.GetFiles(profilesSource, "*.json"))
                {
                    File.Copy(file, Path.Combine(profilesDest, Path.GetFileName(file)), overwrite: true);
                    restoredCount++;
                }
            }

            if (restoredCount == 0)
            {
                return (false, "No valid settings found in backup");
            }

            AppLogger.Info($"Backup restored successfully: {restoredCount} files");
            return (true, $"Settings restored successfully ({restoredCount} files)");
        }
        catch (InvalidDataException)
        {
            AppLogger.Warning("Invalid backup file format");
            return (false, "Invalid backup file format");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to restore backup", ex);
            return (false, $"Restore failed: {ex.Message}");
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Validates a backup file and returns its contents.
    /// </summary>
    /// <param name="zipPath">Path to the .xorbackup file</param>
    /// <returns>Validation result and list of files in the backup</returns>
    public static (bool IsValid, string[] Contents) ValidateBackup(string zipPath)
    {
        try
        {
            if (!File.Exists(zipPath))
            {
                return (false, Array.Empty<string>());
            }

            using var archive = ZipFile.OpenRead(zipPath);
            var entries = archive.Entries.Select(e => e.FullName).ToArray();

            // Check if any settings files exist
            bool hasSettings = entries.Any(e =>
                SettingsFiles.Any(f => e.EndsWith(f, StringComparison.OrdinalIgnoreCase)) ||
                e.Contains("Profiles/", StringComparison.OrdinalIgnoreCase));

            return (hasSettings, entries);
        }
        catch
        {
            return (false, Array.Empty<string>());
        }
    }

    /// <summary>
    /// Finds the root directory containing backup files.
    /// Handles both flat structure and nested folder structure.
    /// </summary>
    private static string? FindBackupRoot(string extractedDir)
    {
        // Check if settings files are directly in the extracted directory
        if (SettingsFiles.Any(f => File.Exists(Path.Combine(extractedDir, f))) ||
            Directory.Exists(Path.Combine(extractedDir, "Profiles")))
        {
            return extractedDir;
        }

        // Check subdirectories (handles ZipFile including root folder)
        foreach (var subDir in Directory.GetDirectories(extractedDir))
        {
            if (SettingsFiles.Any(f => File.Exists(Path.Combine(subDir, f))) ||
                Directory.Exists(Path.Combine(subDir, "Profiles")))
            {
                return subDir;
            }
        }

        return null;
    }
}
