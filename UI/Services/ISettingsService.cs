using System;

namespace FactorioModManager.Services
{
    public interface ISettingsService
    {
        string GetModsPath();

        void SetModsPath(string path);

        string? GetApiKey();

        void SetApiKey(string? apiKey);

        string? GetUsername();

        void SetUsername(string? username);

        string? GetToken();

        void SetToken(string? token);

        bool GetKeepOldModFiles();

        void SetKeepOldModFiles(bool keep);

        DateTime? GetLastUpdateCheck();

        void SetLastUpdateCheck(DateTime dateTime);
    }
}