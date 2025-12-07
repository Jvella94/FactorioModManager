using ReactiveUI;
using System.Collections.Generic;

namespace FactorioModManager.ViewModels
{
    public class ModGroupViewModel : ViewModelBase
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private int _enabledCount;
        private int _totalCount;
        private bool _isEditing;
        private string _editName = string.Empty;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => this.RaiseAndSetIfChanged(ref _description, value);
        }

        public List<string> ModNames { get; set; } = [];

        public int EnabledCount
        {
            get => _enabledCount;
            set
            {
                this.RaiseAndSetIfChanged(ref _enabledCount, value);
                this.RaisePropertyChanged(nameof(StatusText));
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            set
            {
                this.RaiseAndSetIfChanged(ref _totalCount, value);
                this.RaisePropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText => $"{EnabledCount}/{TotalCount} enabled";

        public bool IsEditing
        {
            get => _isEditing;
            set => this.RaiseAndSetIfChanged(ref _isEditing, value);
        }

        public string EditName
        {
            get => _editName;
            set => this.RaiseAndSetIfChanged(ref _editName, value);
        }
    }
}
