using System.Windows;

namespace XOutputRenew.App;

/// <summary>
/// Dark-themed help dialog to replace MessageBox for help content.
/// </summary>
public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
        DarkModeHelper.EnableDarkTitleBar(this);
    }

    /// <summary>
    /// Shows a help dialog with the specified message and title.
    /// </summary>
    public static void Show(string message, string title, Window? owner = null)
    {
        var dialog = new HelpDialog
        {
            Title = title,
            Owner = owner
        };
        dialog.MessageText.Text = message;
        dialog.ShowDialog();
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
