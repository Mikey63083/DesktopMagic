using System.Windows;

namespace DesktopMagic.Dialogs;

public partial class CreatePluginDialog : Wpf.Ui.Controls.FluentWindow
{
    public string ResponseText
    {
        get => textBox.Text;
        set => textBox.Text = value;
    }

    public bool IsWebPlugin { get; private set; }

    public CreatePluginDialog()
    {
        InitializeComponent();

        Resources.MergedDictionaries.Add(App.LanguageDictionary);
    }

    private void DotNetButton_Click(object sender, RoutedEventArgs e)
    {
        IsWebPlugin = false;
        DialogResult = true;
    }

    private void WebPluginButton_Click(object sender, RoutedEventArgs e)
    {
        IsWebPlugin = true;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}