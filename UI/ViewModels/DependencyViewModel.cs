using FactorioModManager.Views.Converters;

namespace FactorioModManager.ViewModels
{
    public class DependencyViewModel(string name, DependencyStatus status)
    {
        public string Name { get; } = name;
        public DependencyStatus Status { get; } = status;
    }
}