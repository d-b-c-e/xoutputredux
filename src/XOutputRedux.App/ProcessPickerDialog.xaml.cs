using System.Windows;
using System.Windows.Controls;

namespace XOutputRedux.App;

/// <summary>
/// Dialog for selecting a running process to whitelist.
/// </summary>
public partial class ProcessPickerDialog : Window
{
    private readonly List<ProcessInfo> _allProcesses;
    public ProcessInfo? SelectedProcess { get; private set; }

    public ProcessPickerDialog(List<ProcessInfo> processes)
    {
        InitializeComponent();

        _allProcesses = processes;
        ProcessListBox.ItemsSource = _allProcesses;

        FilterTextBox.Focus();

        Loaded += (s, e) => DarkModeHelper.EnableDarkTitleBar(this);
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = FilterTextBox.Text.Trim();

        if (string.IsNullOrEmpty(filter))
        {
            ProcessListBox.ItemsSource = _allProcesses;
        }
        else
        {
            ProcessListBox.ItemsSource = _allProcesses
                .Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            p.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void ProcessListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProcessListBox.SelectedItem is ProcessInfo selected)
        {
            SelectedProcess = selected;
            DialogResult = true;
            Close();
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessListBox.SelectedItem is ProcessInfo selected)
        {
            SelectedProcess = selected;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Please select a process.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.None);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
