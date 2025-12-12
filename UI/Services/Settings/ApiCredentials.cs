namespace FactorioModManager.Services.Settings
{
    public interface IApiCredentials
    {
        (string? Username, string? Token) GetCredentials();

        void SetCredentials(string? username, string? token);

        string? GetApiKey();

        void SetApiKey(string? apiKey);
    }

    public class ApiCredentials(ISettingsService settingsService) : IApiCredentials
    {
        private readonly ISettingsService _settingsService = settingsService;

        public (string? Username, string? Token) GetCredentials()
        {
            return (_settingsService.GetUsername(), _settingsService.GetToken());
        }

        public void SetCredentials(string? username, string? token)
        {
            _settingsService.SetUsername(username);
            _settingsService.SetToken(token);
        }

        public string? GetApiKey() => _settingsService.GetApiKey();

        public void SetApiKey(string? apiKey) => _settingsService.SetApiKey(apiKey);
    }
}