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
    }
}
