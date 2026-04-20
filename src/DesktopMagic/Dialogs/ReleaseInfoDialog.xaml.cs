using System.Windows;

namespace DesktopMagic.Dialogs;

public partial class ReleaseInfoDialog : Wpf.Ui.Controls.FluentWindow
{
    public bool OpenReleaseRequested { get; private set; }

    public ReleaseInfoDialog(
        string title,
        string releaseName,
        string publishedAt,
        string markdown,
        string openButtonText,
        string okButtonText)
    {
        InitializeComponent();

        Resources.MergedDictionaries.Add(App.LanguageDictionary);

        titleBar.Title = title;
        Title = title;
        releaseNameTextBlock.Text = releaseName;
        publishedAtTextBlock.Text = publishedAt;
        releaseMarkdownViewer.Markdown = string.IsNullOrWhiteSpace(markdown) ? "-" : markdown;
        openButton.Content = openButtonText;
        okButton.Content = okButtonText;
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        OpenReleaseRequested = true;
        DialogResult = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
