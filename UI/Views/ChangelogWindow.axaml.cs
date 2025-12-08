using Avalonia.Controls;
using FactorioModManager.Views.Base;

namespace FactorioModManager.Views
{
    public partial class ChangelogWindow : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
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
