using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using XOutputRenew.Core.Games;

namespace XOutputRenew.App;

/// <summary>
/// Dialog for adding or editing a game association.
/// </summary>
public partial class GameEditorDialog : Window
{
    private readonly List<ProfileInfo> _profiles;

    /// <summary>
    /// Gets the resulting game association if the dialog was confirmed.
    /// </summary>
    public GameAssociation? Result { get; private set; }

    /// <summary>
    /// Simple class to hold profile info for the combo box.
    /// </summary>
    public class ProfileInfo
    {
        public string Name { get; set; } = "";
        public string FileName { get; set; } = "";
    }

    /// <summary>
    /// Creates a new game editor dialog.
    /// </summary>
    /// <param name="profiles">List of available profiles.</param>
    /// <param name="existingGame">Optional existing game to edit.</param>
    public GameEditorDialog(IEnumerable<ProfileInfo> profiles, GameAssociation? existingGame = null)
    {
        InitializeComponent();
        DarkModeHelper.EnableDarkTitleBar(this);

        _profiles = profiles.ToList();
        ProfileComboBox.ItemsSource = _profiles;

        if (existingGame != null)
        {
            Title = "Edit Game";
            GameNameTextBox.Text = existingGame.Name;
            ExecutablePathTextBox.Text = existingGame.ExecutablePath;
            LaunchDelayTextBox.Text = existingGame.LaunchDelayMs.ToString();

            // Select the profile
            var profile = _profiles.FirstOrDefault(p =>
                p.Name.Equals(existingGame.ProfileName, StringComparison.OrdinalIgnoreCase));
            ProfileComboBox.SelectedItem = profile;
        }
        else
        {
            Title = "Add Game";
            if (_profiles.Count > 0)
            {
                ProfileComboBox.SelectedIndex = 0;
            }
        }

        GameNameTextBox.Focus();
    }

    private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select Game Executable"
        };

        if (dialog.ShowDialog() == true)
        {
            ExecutablePathTextBox.Text = dialog.FileName;

            // Auto-fill game name if empty
            if (string.IsNullOrWhiteSpace(GameNameTextBox.Text))
            {
                // Try to get a nice name from the file
                var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                GameNameTextBox.Text = MakeNiceName(fileName);
            }
        }
    }

    private void SelectProcess_Click(object sender, RoutedEventArgs e)
    {
        var processes = GetRunningProcesses();
        var dialog = new ProcessPickerDialog(processes)
        {
            Owner = this,
            Title = "Select Game Process"
        };

        if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
        {
            ExecutablePathTextBox.Text = dialog.SelectedProcess.Path;

            // Auto-fill game name if empty
            if (string.IsNullOrWhiteSpace(GameNameTextBox.Text))
            {
                GameNameTextBox.Text = MakeNiceName(dialog.SelectedProcess.Name);
            }
        }
    }

    private void SelectSteamGame_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SteamGamePickerDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedGame != null)
        {
            ExecutablePathTextBox.Text = dialog.SelectedGame.ExecutablePath;
            GameNameTextBox.Text = dialog.SelectedGame.Name;
        }
    }

    private List<ProcessInfo> GetRunningProcesses()
    {
        var processes = new List<ProcessInfo>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                // Skip system processes
                if (process.SessionId == 0) continue;

                var path = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(path)) continue;

                // Skip Windows system paths
                if (path.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)) continue;

                // Skip duplicates
                if (!seenPaths.Add(path)) continue;

                processes.Add(new ProcessInfo
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Path = path
                });
            }
            catch
            {
                // Skip processes we can't access
            }
        }

        return processes.OrderBy(p => p.Name).ToList();
    }

    private static string MakeNiceName(string fileName)
    {
        // Remove common suffixes
        var name = fileName
            .Replace("_", " ")
            .Replace("-", " ");

        // Capitalize words
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            char.ToUpper(w[0]) + (w.Length > 1 ? w[1..] : "")));
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(GameNameTextBox.Text))
        {
            MessageBox.Show("Please enter a game name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            GameNameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text))
        {
            MessageBox.Show("Please select an executable.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(ExecutablePathTextBox.Text))
        {
            MessageBox.Show("The selected executable does not exist.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ProfileComboBox.SelectedItem == null)
        {
            MessageBox.Show("Please select a profile.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(LaunchDelayTextBox.Text, out var delay) || delay < 0)
        {
            MessageBox.Show("Please enter a valid launch delay (0 or greater).", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            LaunchDelayTextBox.Focus();
            return;
        }

        var selectedProfile = (ProfileInfo)ProfileComboBox.SelectedItem;

        Result = new GameAssociation
        {
            Name = GameNameTextBox.Text.Trim(),
            ExecutablePath = ExecutablePathTextBox.Text,
            ProfileName = selectedProfile.Name,
            LaunchDelayMs = delay
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
