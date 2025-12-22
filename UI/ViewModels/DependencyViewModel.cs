using FactorioModManager.Views.Converters;

namespace FactorioModManager.ViewModels
{
    public class DependencyViewModel(string name, DependencyStatus status, string? versionOperator = null, string? version = null, bool isInstalled = false, bool isVersionSatisfied = true, string? prefix = null)
    {
        public string Name { get; } = name;
        public DependencyStatus Status { get; } = status;
        public string? VersionOperator { get; } = versionOperator;
        public string? Version { get; } = version;

        // Whether the dependency is currently installed (mod present in mods folder)
        public bool IsInstalled { get; } = isInstalled;

        // Whether the installed version satisfies the requested constraint (if known)
        public bool IsVersionSatisfied { get; } = isVersionSatisfied;

        // The parsed prefix from the raw dependency string (e.g. "?", "(!)", "(!)" )
        public string? Prefix { get; } = prefix;

        // Whether this optional dependency uses the hidden form '(?)'
        public bool IsHiddenOptional => string.Equals(Prefix, "(?)", System.StringComparison.Ordinal);

        // Display text used in the UI (includes version constraint if present)
        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(Version))
                {
                    var op = string.IsNullOrEmpty(VersionOperator) ? "=" : VersionOperator;
                    return $"{Name} {op} {Version}";
                }
                return Name;
            }
        }
    }
}