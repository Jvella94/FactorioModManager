using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FactorioModManager.Models;
using System.Linq;

namespace FactorioModManager.Views.Dialogs
{
    public partial class ImportBehaviorDialog : Window
    {
        private readonly Button? _okBtn;
        private readonly Button? _cancelBtn;
        private readonly RadioButton? _keepRadio;
        private readonly RadioButton? _overwriteRadio;
        private readonly RadioButton? _mergeRadio;
        private readonly ListBox? _previewList;

        public ImportBehaviorDialog()
        {
            InitializeComponent();

            _okBtn = this.FindControl<Button>("OkBtn");
            _cancelBtn = this.FindControl<Button>("CancelBtn");
            _keepRadio = this.FindControl<RadioButton>("KeepRadio");
            _overwriteRadio = this.FindControl<RadioButton>("OverwriteRadio");
            _mergeRadio = this.FindControl<RadioButton>("MergeRadio");
            _previewList = this.FindControl<ListBox>("PreviewList");

            if (_okBtn != null) _okBtn.Click += OkBtn_Click;
            if (_cancelBtn != null) _cancelBtn.Click += CancelBtn_Click;
        }

        public ImportBehaviorDialog(CustomModList candidate) : this()
        {
            // Populate preview with entire entries
            if (candidate?.Entries != null && _previewList != null)
            {
                var items = candidate.Entries.Select(e => $"{e.Name} {(e.Enabled ? "(enabled)" : "(disabled)")}").ToList();
                _previewList.ItemsSource = items;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OkBtn_Click(object? sender, RoutedEventArgs e)
        {
            Close(GetSelected());
        }

        private void CancelBtn_Click(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private ImportBehavior GetSelected()
        {
            if (_overwriteRadio?.IsChecked == true) return ImportBehavior.Overwrite;
            if (_mergeRadio?.IsChecked == true) return ImportBehavior.Merge;
            if (_keepRadio?.IsChecked == true) return ImportBehavior.Keep;
            return ImportBehavior.Keep; // Default fallback
        }
    }
}