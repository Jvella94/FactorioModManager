using FactorioModManager.Views.Base;

namespace FactorioModManager.Views
{
    public partial class ChangelogWindow : DialogWindowBase<bool>
    {
        public ChangelogWindow()
        {
            CanResize = true;
            InitializeComponent();
        }

        public ChangelogWindow(string modTitle, string changelog) : this()
        {
            Title = $"Changelog - {modTitle}";
            ModTitle.Text = $"{modTitle} - Changelog";
            ChangelogText.Text = changelog;
        }
    }
}