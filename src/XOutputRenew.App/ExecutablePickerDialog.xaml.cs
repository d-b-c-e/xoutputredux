using System.IO;
using System.Windows;

namespace XOutputRenew.App;

/// <summary>
/// Dialog for selecting a game executable from a list of candidates.
/// </summary>
public partial class ExecutablePickerDialog : Window
{
    /// <summary>
    /// Gets the selected executable path.
    /// </summary>
    public string? SelectedExecutable { get; private set; }

    /// <summary>
    /// Represents an executable candidate.
    /// </summary>
    public class ExecutableInfo
    {
        public string FullPath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string Tag { get; set; } = "";
        public bool IsLikelyGame { get; set; }
    }

    public ExecutablePickerDialog(string gameName, string gamePath, IEnumerable<ExecutableInfo> executables)
    {
        InitializeComponent();
        DarkModeHelper.EnableDarkTitleBar(this);

        GameNameText.Text = gameName;
        GamePathText.Text = gamePath;

        var exeList = executables.ToList();
        ExecutableListBox.ItemsSource = exeList;

        // Auto-select the most likely game executable
        var likely = exeList.FirstOrDefault(e => e.IsLikelyGame) ?? exeList.FirstOrDefault();
        if (likely != null)
        {
            ExecutableListBox.SelectedItem = likely;
        }
    }

    private void ExecutableListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ExecutableListBox.SelectedItem != null)
        {
            OK_Click(sender, e);
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (ExecutableListBox.SelectedItem is ExecutableInfo selected)
        {
            SelectedExecutable = selected.FullPath;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Please select an executable.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
