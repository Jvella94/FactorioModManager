using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels
{
    public class ModGroupViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private int _enabledCount;
        private int _totalCount;
        private bool _isEditing;
        private string _editName = string.Empty;

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

        public ModGroupViewModel()
        {
            this.WhenAnyValue(
                x => x.EnabledCount,
                x => x.TotalCount)
                .Select(_ => Unit.Default)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(StatusText)));
        }
    }
}