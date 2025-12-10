namespace FactorioModManager.Services.Settings
{
    public interface IModPathSettings
    {
        string GetModsPath();
        void SetModsPath(string path);
    }

    public class ModPathSettings(ISettingsService settingsService) : IModPathSettings
    {
        private readonly ISettingsService _settingsService = settingsService;

        public string GetModsPath() => _settingsService.GetModsPath();

        public void SetModsPath(string path) => _settingsService.SetModsPath(path);
    }
}