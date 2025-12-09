using Avalonia.Controls;
using FactorioModManager.Views.Base;

namespace FactorioModManager.Views
{
    public partial class ChangelogWindow : DialogWindowBase<bool>
    {
        public ChangelogWindow()
        {
            InitializeComponent();
        }

        public ChangelogWindow(string modTitle, string changelog) : this()
        {
            ModTitle.Text = $"{modTitle} - Changelog";
            ChangelogText.Text = changelog;
        }
    }
}