using System.Windows;

namespace NRE.WpfEditor;

public partial class StartupSplashWindow : Window
{
    public StartupSplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string summary, string detail)
    {
        StatusSummaryText.Text = summary;
        StatusDetailText.Text = detail;
    }
}
