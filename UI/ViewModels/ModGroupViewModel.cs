using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels
{
    public class ModGroupViewModel : ViewModelBase
    {
        private readonly CompositeDisposable _disposables = [];

        private string _name = string.Empty;
        private int _enabledCount;
        private int _totalCount;
        private bool _isEditing;
        private string _editName = string.Empty;
        private bool _isActiveFilter = false;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public List<string> ModNames { get; set; } = [];

        public int EnabledCount
        {
            get => _enabledCount;
            set => this.RaiseAndSetIfChanged(ref _enabledCount, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => this.RaiseAndSetIfChanged(ref _totalCount, value);
        }

        public string StatusText => $"{EnabledCount}/{TotalCount} enabled";

        public bool IsRenaming
        {
            get => _isEditing;
            set => this.RaiseAndSetIfChanged(ref _isEditing, value);
        }

        public string EditedName
        {
            get => _editName;
            set => this.RaiseAndSetIfChanged(ref _editName, value);
        }

        // Indicates whether this group is currently used as the active filter
        public bool IsActiveFilter
        {
            get => _isActiveFilter;
            set => this.RaiseAndSetIfChanged(ref _isActiveFilter, value);
        }

        public ModGroupViewModel()
        {
            // ✅ Dispose subscription properly
            this.WhenAnyValue(x => x.EnabledCount, x => x.TotalCount)
                .Select(_ => Unit.Default)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(StatusText)))
                .DisposeWith(_disposables);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposables?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}