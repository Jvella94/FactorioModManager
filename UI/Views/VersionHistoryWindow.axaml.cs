using FactorioModManager.Models.DTO;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels.Dialogs;
using FactorioModManager.Views.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System;

namespace FactorioModManager.Views
{
    public partial class VersionHistoryWindow : DialogWindowBase<bool>
    {
        private VersionHistoryViewModel? _attachedVm;

        public VersionHistoryWindow()
        {
            InitializeComponent();

            // Prevent closing while a download/delete is in progress
            Closing += OnClosingPreventIfDownloading;

            // React to DataContext changes so we can attach/detach event handlers
            DataContextChanged += OnDataContextChanged;

            // If DataContext already set by XAML or caller, attach now
            if (DataContext is VersionHistoryViewModel existingVm)
            {
                AttachToVm(existingVm);
            }
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // Detach previous, attach new
            if (_attachedVm != null)
            {
                DetachFromVm(_attachedVm);
                _attachedVm = null;
            }

            if (DataContext is VersionHistoryViewModel vm)
            {
                AttachToVm(vm);
            }
        }

        private void AttachToVm(VersionHistoryViewModel vm)
        {
            _attachedVm = vm;
        }

        private void DetachFromVm(VersionHistoryViewModel vm)
        {
            _attachedVm = null;
        }

        private void OnClosingPreventIfDownloading(object? sender, CancelEventArgs e)
        {
            if (DataContext is VersionHistoryViewModel vm)
            {
                try
                {
                    if (vm.IsOperationInProgress)
                    {
                        e.Cancel = true;
                        _ = vm.NotifyCloseBlockedAsync();
                    }
                }
                catch
                {
                    // fallback: cancel and show message via UI service
                    e.Cancel = true;
                    var ui = ServiceContainer.Instance.Resolve<IUIService>();
                    _ = ui.ShowMessageAsync("Operation in progress", "A download or deletion is in progress. Please cancel it before closing this window.", this);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Detach event handlers
            try
            {
                if (_attachedVm != null)
                    DetachFromVm(_attachedVm);
            }
            catch { }

            base.OnClosed(e);
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