using FactorioModManager.Models;
using System.Collections.Generic;

namespace FactorioModManager.Services
{
    public interface IModListService
    {
        List<CustomModList> LoadLists();

        void SaveLists(List<CustomModList> lists);

        void AddList(CustomModList list);

        void UpdateList(string oldName, CustomModList updated);

        void DeleteList(string name);

        // Export/import helpers
        bool ExportList(string name, string destinationPath, bool overwrite = false);

        CustomModList? ImportListFromFile(string sourcePath);

        // Returns the on-disk file path for a named list, or null if not found
        string? GetListFilePath(string name);
    }
}
