using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    public class CustomModList : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string? _description;
        private bool _isRenaming;
        private string _editedName = string.Empty;

        [JsonPropertyName("name")]
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        [JsonPropertyName("description")]
        public string? Description
        {
            get => _description;
            set => SetField(ref _description, value);
        }

        // Reuse ModListEntry for name/enabled/version if needed
        [JsonPropertyName("entries")]
        public List<ModListEntry> Entries { get; set; } = [];

        // UI-only: inline rename state - not persisted
        [JsonIgnore]
        public bool IsRenaming
        {
            get => _isRenaming;
            set => SetField(ref _isRenaming, value);
        }

        [JsonIgnore]
        public string EditedName
        {
            get => _editedName;
            set => SetField(ref _editedName, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propName);
            return true;
        }
    }

    public class CustomModListCollection
    {
        [JsonPropertyName("lists")]
        public List<CustomModList> Lists { get; set; } = [];
    }
}