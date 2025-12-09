using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
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

        public VersionHistoryWindow(string modTitle, string modName, List<ReleaseDTO> releases) : this()
        {
            var vm = new VersionHistoryViewModel(
               ServiceContainer.Instance.Resolve<IModService>(),
               ServiceContainer.Instance.Resolve<ISettingsService>(),
               ServiceContainer.Instance.Resolve<ILogService>(),
               modTitle, modName, releases);  // ✅ Pass modName
            DataContext = vm;

            // Update title
            ModTitle.Text = vm.ModTitle;
        }
    }
}