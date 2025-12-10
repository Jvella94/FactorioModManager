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

        DateTime? GetLastModUpdateCheck();

        void SetLastModUpdateCheck(DateTime dateTime);

        string? GetFactorioExecutablePath();

        void SetFactorioExecutablePath(string path);

        DateTime? GetLastAppUpdateCheck();

        void SetLastAppUpdateCheck(DateTime timestamp);

        bool GetCheckForAppUpdates();

        void SetCheckForAppUpdates(bool enabled);

        // Factorio version & DLC info
        string? GetFactorioVersion();

        void SetFactorioVersion(string? version);

        bool GetHasSpaceAgeDlc();

        void SetHasSpaceAgeDlc(bool value);
    }
}