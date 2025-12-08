using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels;
using FactorioModManager.Views.Base;
using System.Collections.Generic;

namespace FactorioModManager.Views
{
    public partial class VersionHistoryWindow : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
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

            // ✅ CRITICAL: Bind DataGrid to Releases collection
            VersionGrid.ItemsSource = vm.Releases;

            // Update title
            ModTitle.Text = vm.ModTitle;
        }
    }
}