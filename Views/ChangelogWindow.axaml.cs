using Avalonia.Controls;

namespace FactorioModManager.Views
{
    public partial class ChangelogWindow : Window
    {
        public ChangelogWindow()
        {
            InitializeComponent();
        }

        public ChangelogWindow(string modTitle, string changelog) : this()
        {
            this.FindControl<TextBlock>("ModTitle")!.Text = $"{modTitle} - Changelog";
            this.FindControl<TextBlock>("ChangelogText")!.Text = changelog;
        }
    }
}
