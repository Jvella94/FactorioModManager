using Avalonia.Controls;
using FactorioModManager.Models.API;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.ViewModels;
using FactorioModManager.Views.Base;
using System.Collections.Generic;
using System.Linq;

namespace FactorioModManager.Views
{
    public partial class VersionHistoryWindow : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
    {
        public VersionHistoryWindow()
        {
            InitializeComponent();
        }

        public VersionHistoryWindow(string modTitle, string modName, List<ModReleaseDto> releases) : this()
        {
            var vm = new VersionHistoryViewModel(
               ServiceContainer.Instance.Resolve<IModService>(),
               ServiceContainer.Instance.Resolve<IFactorioApiService>(),
               modTitle, modName, releases);  // ✅ Pass modName
            DataContext = vm;

            // ✅ CRITICAL: Bind DataGrid to Releases collection
            VersionGrid.ItemsSource = vm.Releases;

            // Update title
            ModTitle.Text = vm.ModTitle;
        }
    }
}
