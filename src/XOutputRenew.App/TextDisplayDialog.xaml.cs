using System.Windows;

namespace XOutputRenew.App;

public partial class TextDisplayDialog : Window
{
    public TextDisplayDialog(string title, string content)
    {
        InitializeComponent();
        Title = title;
        ContentTextBox.Text = content;
        ContentTextBox.SelectAll();
        ContentTextBox.Focus();
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        ContentTextBox.SelectAll();
        ContentTextBox.Copy();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
