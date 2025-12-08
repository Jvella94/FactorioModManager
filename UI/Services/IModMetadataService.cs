namespace FactorioModManager.Services
{
    public interface IModMetadataService
    {
        string? GetCategory(string modName);

        void UpdateCategory(string modName, string? category);

        string? GetSourceUrl(string modName);

        void UpdateSourceUrl(string modName, string? sourceUrl, bool wasChecked = true);

        bool GetHasUpdate(string modName);

        string? GetLatestVersion(string modName);

        void UpdateLatestVersion(string modName, string version, bool hasUpdate);

        void ClearUpdate(string modName);

        bool NeedsCategoryCheck(string modName);

        bool NeedsSourceUrlCheck(string modName);

        void MarkAsChecked(string modName);
    }
}