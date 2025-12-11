using FactorioModManager.Models.DTO;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels.Dialogs;
using FactorioModManager.Views.Base;
using System.Collections.Generic;

namespace FactorioModManager.Views
{
    public partial class VersionHistoryWindow : DialogWindowBase<bool>
    {
        public VersionHistoryWindow()
        {
            InitializeComponent();
        }

        public VersionHistoryWindow(string modTitle, string modName, List<ShortReleaseDTO> releases) : this()
        {
            var vm = new VersionHistoryViewModel(
               ServiceContainer.Instance.Resolve<IModVersionManager>(),
               ServiceContainer.Instance.Resolve<ISettingsService>(),
               ServiceContainer.Instance.Resolve<ILogService>(),
               ServiceContainer.Instance.Resolve<IUIService>(),
               ServiceContainer.Instance.Resolve<IModService>(), // pass IModService so view-model can persist active version
               modTitle, modName, releases, this);
            DataContext = vm;

            // Update title
            ModTitle.Text = vm.ModTitle;
        }
    }
}