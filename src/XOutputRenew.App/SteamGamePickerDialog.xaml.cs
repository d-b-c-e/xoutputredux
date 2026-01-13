using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace XOutputRenew.App;

/// <summary>
/// Dialog for selecting an installed Steam game.
/// </summary>
public partial class SteamGamePickerDialog : Window
{
    private readonly List<SteamGame> _allGames;

    /// <summary>
    /// Gets the selected Steam game.
    /// </summary>
    public SteamGame? SelectedGame { get; private set; }

    /// <summary>
    /// Known launcher/utility executables to filter out or mark as unlikely.
    /// </summary>
    private static readonly string[] LauncherPatterns =
    {
        // Epic Online Services
        "eosbootstrapper",
        "eosoverlay",
        "epiconlineservices",

        // Anti-cheat
        "easyanticheat",
        "eac_launcher",
        "battleye",
        "beclient",
        "beservice",
        "vanguard",

        // Crash handlers
        "crashhandler",
        "crashreporter",
        "crashpad",
        "crashsender",
        "unitycrashhandler",
        "uecrashreporter",

        // Launchers/Updaters
        "launcher",
        "updater",
        "patcher",
        "bootstrapper",

        // Redistributables
        "vcredist",
        "dxsetup",
        "dotnetfx",
        "physx",
        "directx",
        "oalinst",
        "ue4prereq",
        "ue5prereq",

        // Uninstallers
        "unins",
        "uninstall",

        // Utilities
        "cleanup",
        "verify",
        "repair",
        "config",
        "settings",
        "editor",
        "modkit",
        "devtools",
        "dedicated", // dedicated server
        "server",    // server executable

        // Common subdirectory patterns (check path)
        "_commonredist",
        "redist",
        "prerequisites",
        "installers",
        "__installer",
        "support",
        "tools"
    };

    public SteamGamePickerDialog()
    {
        InitializeComponent();
        DarkModeHelper.EnableDarkTitleBar(this);

        _allGames = DiscoverSteamGames();

        if (_allGames.Count == 0)
        {
            StatusText.Text = "No Steam games found. Make sure Steam is installed.";
        }
        else
        {
            StatusText.Text = $"Found {_allGames.Count} installed game(s)";
        }

        GamesListBox.ItemsSource = _allGames;
        FilterTextBox.Focus();
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = FilterTextBox.Text.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            GamesListBox.ItemsSource = _allGames;
        }
        else
        {
            GamesListBox.ItemsSource = _allGames
                .Where(g => g.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void GamesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GamesListBox.SelectedItem != null)
        {
            OK_Click(sender, e);
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (GamesListBox.SelectedItem is not SteamGame game)
            return;

        // If there's only one executable, use it directly
        if (game.Executables.Count == 1)
        {
            game.ExecutablePath = game.Executables[0].FullPath;
            SelectedGame = game;
            DialogResult = true;
            Close();
            return;
        }

        // Show executable picker dialog
        var exePicker = new ExecutablePickerDialog(game.Name, game.InstallPath, game.Executables)
        {
            Owner = this
        };

        if (exePicker.ShowDialog() == true && exePicker.SelectedExecutable != null)
        {
            game.ExecutablePath = exePicker.SelectedExecutable;
            SelectedGame = game;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static List<SteamGame> DiscoverSteamGames()
    {
        var games = new List<SteamGame>();

        try
        {
            // Find Steam installation path from registry
            var steamPath = GetSteamInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return games;

            // Get all library folders
            var libraryFolders = GetSteamLibraryFolders(steamPath);

            // Scan each library for installed games
            foreach (var libraryPath in libraryFolders)
            {
                var steamAppsPath = Path.Combine(libraryPath, "steamapps");
                if (!Directory.Exists(steamAppsPath))
                    continue;

                // Find all appmanifest files
                var manifestFiles = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf");
                foreach (var manifestFile in manifestFiles)
                {
                    var game = ParseAppManifest(manifestFile, steamAppsPath);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Error discovering Steam games: {ex.Message}");
        }

        return games.OrderBy(g => g.Name).ToList();
    }

    private static string? GetSteamInstallPath()
    {
        try
        {
            // Try 64-bit registry first
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            if (key?.GetValue("InstallPath") is string path)
                return path;

            // Try 32-bit registry
            using var key32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            if (key32?.GetValue("InstallPath") is string path32)
                return path32;

            // Try common paths
            var commonPaths = new[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam")
            };

            foreach (var p in commonPaths)
            {
                if (Directory.Exists(p))
                    return p;
            }
        }
        catch { }

        return null;
    }

    private static List<string> GetSteamLibraryFolders(string steamPath)
    {
        var folders = new List<string> { steamPath };

        try
        {
            var libraryFoldersVdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersVdf))
                return folders;

            var content = File.ReadAllText(libraryFoldersVdf);

            // Parse VDF to find "path" entries
            // Format: "path"		"D:\\SteamLibrary"
            var pathRegex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            var matches = pathRegex.Matches(content);

            foreach (Match match in matches)
            {
                var path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(path) && !folders.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    folders.Add(path);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Error parsing libraryfolders.vdf: {ex.Message}");
        }

        return folders;
    }

    private static SteamGame? ParseAppManifest(string manifestPath, string steamAppsPath)
    {
        try
        {
            var content = File.ReadAllText(manifestPath);

            // Parse name
            var nameMatch = Regex.Match(content, @"""name""\s+""([^""]+)""");
            if (!nameMatch.Success)
                return null;

            var name = nameMatch.Groups[1].Value;

            // Skip tools, demos, etc.
            if (name.Contains("Steamworks", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Proton", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase))
                return null;

            // Parse installdir
            var installdirMatch = Regex.Match(content, @"""installdir""\s+""([^""]+)""");
            if (!installdirMatch.Success)
                return null;

            var installDir = installdirMatch.Groups[1].Value;
            var gamePath = Path.Combine(steamAppsPath, "common", installDir);

            if (!Directory.Exists(gamePath))
                return null;

            // Find all executables (not just one)
            var executables = FindGameExecutables(gamePath, name);
            if (executables.Count == 0)
                return null;

            // Parse appid
            var appidMatch = Regex.Match(content, @"""appid""\s+""(\d+)""");
            var appId = appidMatch.Success ? appidMatch.Groups[1].Value : "0";

            return new SteamGame
            {
                Name = name,
                AppId = appId,
                InstallPath = gamePath,
                Executables = executables,
                // Pre-select the most likely executable
                ExecutablePath = executables.FirstOrDefault(e => e.IsLikelyGame)?.FullPath
                    ?? executables.First().FullPath
            };
        }
        catch
        {
            return null;
        }
    }

    private static List<ExecutablePickerDialog.ExecutableInfo> FindGameExecutables(string gamePath, string gameName)
    {
        var executables = new List<ExecutablePickerDialog.ExecutableInfo>();

        try
        {
            var exeFiles = Directory.GetFiles(gamePath, "*.exe", SearchOption.AllDirectories);

            // Get words from game name for matching
            var nameWords = gameName.Split(' ', '-', '_', ':', '\'', '.')
                .Where(w => w.Length > 2)
                .Select(w => w.ToLower())
                .ToList();

            foreach (var exePath in exeFiles)
            {
                var fileName = Path.GetFileName(exePath);
                var fileNameLower = fileName.ToLower();
                var pathLower = exePath.ToLower();
                var relativePath = Path.GetRelativePath(gamePath, Path.GetDirectoryName(exePath) ?? gamePath);
                if (relativePath == ".") relativePath = "(root)";

                // Check if this matches any launcher patterns
                bool isLauncher = LauncherPatterns.Any(pattern =>
                    fileNameLower.Contains(pattern) || pathLower.Contains(pattern));

                // Check if filename matches game name words
                var exeNameWithoutExt = Path.GetFileNameWithoutExtension(exePath).ToLower();
                bool matchesGameName = nameWords.Any(w => exeNameWithoutExt.Contains(w));

                // Check if it's in the root directory
                bool isInRoot = Path.GetDirectoryName(exePath)?.Equals(gamePath, StringComparison.OrdinalIgnoreCase) == true;

                // Determine if this is likely the main game executable
                bool isLikelyGame = !isLauncher && (matchesGameName || isInRoot);

                // Generate a tag for display
                string tag = "";
                if (isLauncher)
                    tag = "Launcher/Utility";
                else if (matchesGameName)
                    tag = "Likely game";
                else if (isInRoot)
                    tag = "Root folder";

                executables.Add(new ExecutablePickerDialog.ExecutableInfo
                {
                    FullPath = exePath,
                    FileName = fileName,
                    RelativePath = relativePath,
                    Tag = tag,
                    IsLikelyGame = isLikelyGame
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Error scanning for executables in {gamePath}: {ex.Message}");
        }

        // Sort: likely games first, then by name
        return executables
            .OrderByDescending(e => e.IsLikelyGame)
            .ThenByDescending(e => e.Tag == "Likely game")
            .ThenByDescending(e => e.Tag == "Root folder")
            .ThenBy(e => e.FileName)
            .ToList();
    }
}

/// <summary>
/// Represents an installed Steam game.
/// </summary>
public class SteamGame
{
    public string Name { get; set; } = "";
    public string AppId { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public List<ExecutablePickerDialog.ExecutableInfo> Executables { get; set; } = new();
}
